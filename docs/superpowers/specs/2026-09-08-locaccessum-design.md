# Locaccessum — Design

**Date :** 2026-09-08
**Statut :** validé pour passage au plan d'implémentation

## 1. Objectif

Locaccessum est un système de réservation de matériel avec rappels automatiques,
conçu comme projet vitrine full-stack. Il doit s'adapter à n'importe quel secteur
(location de camions frigorifiques, outillage d'usinage, parc informatique, objets
partagés entre amis…) grâce à un modèle de matériel générique.

Déploiement visé : **une instance auto-hébergée unique** (pas de SaaS commercial,
pas de facturation). À l'intérieur de cette instance, plusieurs **inventaires
partagés** (espaces de travail) coexistent ; les comptes utilisateurs sont
globaux et peuvent appartenir à plusieurs inventaires.

## 2. Décisions structurantes

| Sujet | Décision |
|---|---|
| Portée produit | Mono-instance, modèle de matériel générique. Multi-inventaires internes, pas multi-tenant SaaS. |
| Fiche matériel | `{ Id, Name, Reference, Informations }` où `Informations` est **un unique champ texte libre** rempli par l'admin. Pas d'EAV, pas d'attributs typés. |
| Stacking | Une fiche = **une unité physique** avec son `Id`. Le « stack » est un **regroupement calculé** des fiches partageant `(InventoryId, Name, Reference)`. Pas de table dédiée. |
| Réservation d'un stack | L'utilisateur réserve le groupe ; le système **attribue automatiquement** une unité libre sur le créneau, ou refuse (`409`) si toutes sont prises. Règle stricte « 1 unité = 1 créneau ». |
| Base de données | **PostgreSQL 17** + extension `btree_gist`. |
| Comptes / rôles | Inscription libre → rôle global implicite « User ». Chaque compte a un **code utilisateur** unique, court, immuable, servant à la recherche pour inviter. Rôles **par inventaire** : `Owner` / `Admin` / `Member`. |
| Invitations | L'admin saisit un code utilisateur → invitation `Pending` → l'invité **accepte ou refuse**. Il rejoint l'inventaire seulement après acceptation. |
| Environnement de dev | **Docker Compose pour toute la stack** (Postgres + API .NET + worker Node + web). |
| Modèle de créneau | **Plage libre** : `startsAt` / `endsAt` en date+heure arbitraires, granularité 15 min. Détection de conflit par chevauchement d'intervalles. |
| JWT | HS256, secret en variable d'environnement, durée 8 h, **pas de refresh token** (reconnexion). |

## 3. Architecture & structure du dépôt

Monorepo unique orchestré par Docker Compose.

```
Locaccessum/
├── backend/                          # Solution ASP.NET Core (.NET 10 LTS)
│   ├── Locaccessum.Api/              # Contrôleurs, DTOs, policies d'autorisation, DI
│   ├── Locaccessum.Domain/           # Entités, enums, service de détection de conflit
│   ├── Locaccessum.Infrastructure/   # DbContext EF Core, migrations, JWT, hashing
│   ├── Locaccessum.Tests/            # xUnit + Testcontainers (Postgres réel)
│   └── Locaccessum.sln
├── frontend/                         # React 19 + Vite + TypeScript
├── worker/                           # Service Node.js (rappels)
├── docker-compose.yml                # postgres + api + worker + web
├── docker-compose.override.yml       # surcharge dev (hot reload, ports)
├── .env.example
└── README.md
```

### Briques techniques

| Composant | Stack |
|---|---|
| API | ASP.NET Core Web API, EF Core 10, Npgsql, JWT HS256, `PasswordHasher<User>` (PBKDF2) |
| DB | PostgreSQL 17, extension `btree_gist` (contrainte d'exclusion anti-chevauchement) |
| Front | React 19, Vite, TypeScript, React Router, `@tanstack/react-query`, `axios`, `react-big-calendar` (localizer `date-fns`, locale `fr`), `date-fns` |
| Worker | Node.js 22, `node-cron`, `nodemailer`, `axios`, SMTP Mailtrap (sandbox) |
| Tests | Backend : xUnit + Testcontainers-Postgres ; Worker : Vitest ; Front : Vitest + React Testing Library |

### Flux global

React → (JWT) → API .NET → PostgreSQL.
Le worker Node tourne indépendamment, sans accès direct à la base : il appelle une
route interne de l'API protégée par une clé d'API dédiée pour récupérer les
réservations à J-1 et envoyer les e-mails.

## 4. Modèle de données

Sept tables. Le **stack** n'est pas une table : c'est un `GROUP BY (InventoryId, Name, Reference)` sur `Equipment`.

### `User` — compte global
- `Id` (guid, PK)
- `Email` (unique)
- `PasswordHash`
- `DisplayName`
- `UserCode` (unique, court, format type `LOCA-8F3K2D`, généré à l'inscription, **immuable**, indexé pour la recherche)
- `CreatedAt`

### `Inventory` — espace de travail partagé
- `Id` (PK)
- `Name`
- `Description`
- `OwnerId` → `User`
- `CreatedAt`

### `Membership` — appartenance d'un user à un inventaire
- `Id` (PK)
- `InventoryId` → `Inventory`
- `UserId` → `User`
- `Role` : enum `Owner` | `Admin` | `Member`
- `JoinedAt`
- Unicité `(InventoryId, UserId)`
- Invariant : **exactement un `Owner`** par inventaire

### `Invitation`
- `Id` (PK)
- `InventoryId` → `Inventory`
- `InvitedUserId` → `User` (résolu depuis le code utilisateur saisi)
- `InvitedByUserId` → `User`
- `Role` : enum `Admin` | `Member`
- `Status` : enum `Pending` | `Accepted` | `Declined` | `Revoked`
- `CreatedAt`, `RespondedAt` (nullable)
- Unicité d'une invitation `Pending` par `(InventoryId, InvitedUserId)`

### `Equipment` — une fiche = une unité physique
- `Id` (PK)
- `InventoryId` → `Inventory`
- `Name`
- `Reference`
- `Informations` (texte libre)
- `Status` : enum `Active` | `Maintenance` | `Retired`
- `CreatedAt`
- Index `(InventoryId, Name, Reference)` pour le regroupement en stacks

### `Reservation`
- `Id` (PK)
- `EquipmentId` → `Equipment`
- `UserId` → `User` (auteur de la réservation)
- `StartsAt` (timestamptz)
- `EndsAt` (timestamptz)
- `Status` : enum `Confirmed` | `Cancelled`
- `ReminderSentAt` (nullable) — posé par la route interne quand le worker a envoyé le rappel
- `CreatedAt`, `CancelledAt` (nullable)
- Contrainte `EndsAt > StartsAt`
- **Contrainte d'exclusion PostgreSQL** :
  `EXCLUDE USING gist (EquipmentId WITH =, tstzrange(StartsAt, EndsAt) WITH &&) WHERE (Status = 'Confirmed')`
  → le double-booking sur une même unité est **physiquement impossible en base**, même en cas de course.

### Portée des données
Tout `Equipment` et toute `Reservation` appartiennent à un `Inventory`. Chaque
requête vérifie l'appartenance (`Membership`) et le rôle de l'utilisateur sur
l'inventaire ciblé.

## 5. API .NET

### Autorisation
Le JWT porte `userId`. Un `AuthorizationHandler` custom résout la `Membership` de
l'utilisateur sur l'inventaire ciblé (id pris dans la route) et applique des
policies à rôle minimum :

- `InventoryMember` : lecture matériel + calendrier, création/annulation de **ses propres** réservations
- `InventoryAdmin` : + CRUD fiches matériel, gestion des membres, invitations, édition nom/description de l'inventaire
- `InventoryOwner` : + suppression de l'inventaire, transfert de propriété

### Endpoints

| Zone | Routes |
|---|---|
| Auth | `POST /api/auth/register` · `POST /api/auth/login` → JWT · `GET /api/users/me` · `GET /api/users/search?code=` |
| Inventaires | `GET /api/inventories` (les miens) · `POST` · `GET /{id}` · `PATCH /{id}` (nom/description, Admin+) · `DELETE /{id}` (Owner) · `POST /{id}/transfer-ownership` (Owner) |
| Membres | `GET /api/inventories/{id}/members` · `PATCH /api/inventories/{id}/members/{userId}` (rôle) · `DELETE /api/inventories/{id}/members/{userId}` |
| Invitations | `POST /api/inventories/{id}/invitations` `{userCode, role}` · `GET /api/inventories/{id}/invitations` (Admin+) · `GET /api/invitations` (les miennes en attente) · `POST /api/invitations/{id}/accept` · `POST /api/invitations/{id}/decline` · `DELETE /api/invitations/{id}` (révoquer, Admin+) |
| Matériel | `GET /api/inventories/{id}/equipment?grouped=true` (renvoie les stacks) · `POST` · `GET /api/equipment/{id}` · `PATCH /api/equipment/{id}` · `DELETE /api/equipment/{id}` |
| Réservations | `GET /api/inventories/{id}/reservations?from=&to=&equipmentId=` (flux calendrier) · `POST /api/inventories/{id}/reservations` · `POST /api/reservations/{id}/cancel` |
| Interne (worker) | `GET /api/internal/reservations/upcoming?windowHours=24` · `POST /api/internal/reservations/{id}/reminder-sent` — auth par en-tête `X-Internal-Api-Key` |

### Création de réservation — logique anti-double-réservation

Corps de requête : `{ name, reference, startsAt, endsAt }` (cible un stack) **ou**
`{ equipmentId, startsAt, endsAt }` (unité précise choisie par l'utilisateur).

Traitement dans **une transaction** :

1. `pg_advisory_xact_lock(hashtext(inventoryId || name || reference))` — sérialise
   les demandes concurrentes sur le **même stack** ; les autres stacks ne sont pas bloqués.
2. Sélection des unités `Active` du stack sans réservation `Confirmed` chevauchant
   `[startsAt, endsAt)`. Test de chevauchement :
   `existing.StartsAt < req.EndsAt AND existing.EndsAt > req.StartsAt`.
3. Aucune unité libre → `409 Conflict`, corps `{ code: "STACK_FULL", unitsTotal, unitsBusy }`.
4. Sinon : attribution de la première unité libre, `INSERT`, `COMMIT`.

**Double filet de sécurité :** si deux transactions passaient malgré tout
l'étape 2 simultanément, la contrainte d'exclusion GiST rejette le second
`INSERT` ; l'API attrape la violation et la retraduit en `409`.

### Validation
- `startsAt` dans le futur
- `endsAt > startsAt`
- unité non `Retired` / `Maintenance`
- durée max configurable (désactivée par défaut)

### Annulation
Un `Member` annule **ses** réservations futures ; `Admin` / `Owner` annulent
n'importe quelle réservation de leur inventaire. Passage en `Cancelled` → le
créneau se libère (la contrainte d'exclusion ne filtre que `Confirmed`).

### Sécurité transverse
- Hash mot de passe PBKDF2 (`PasswordHasher<User>`)
- JWT HS256, secret en variable d'environnement, 8 h, pas de refresh token
- CORS restreint à l'origine du front
- `X-Internal-Api-Key` distincte pour le worker

## 6. Frontend React

### Pile
Vite + TypeScript, React Router, `@tanstack/react-query` (cache + invalidation),
`axios` avec intercepteur injectant le JWT et redirigeant vers `/login` sur `401`.
`react-big-calendar` + localizer `date-fns` (locale `fr`). Choix
CSS Modules vs Tailwind tranché au scaffold, sans impact archi.

### Contexte applicatif
Après login, un `AuthProvider` conserve `user` + token (mémoire + `localStorage`).
Un sélecteur d'inventaire actif en tête d'application (switcher d'espace de travail).

### Pages

| Route | Contenu | Accès |
|---|---|---|
| `/login`, `/register` | formulaires, gestion erreurs | public |
| `/` | liste « mes inventaires » + créer ; badge invitations en attente | connecté |
| `/invitations` | invitations reçues : accepter / refuser | connecté |
| `/inventory/:id` | tableau de bord : fiches **regroupées en stacks** (`Frigo 19T ×3`), badges statut | Membre+ |
| `/inventory/:id/equipment/:eqId` | détail fiche + édition du champ `Informations` | Admin+ pour l'édition |
| `/inventory/:id/calendar` | `react-big-calendar` : réservations `Confirmed` ; clic créneau libre → dialogue de réservation | Membre+ |
| `/inventory/:id/members` | membres + rôles ; champ « inviter par code utilisateur » avec recherche | Admin+ |
| `/inventory/:id/settings` | modifier nom + description ; zone danger : transfert de propriété, suppression | Admin+ pour nom/description ; **Owner uniquement** pour transfert et suppression |
| `/profile` | affiche **mon code utilisateur** (copiable), déconnexion | connecté |

Accès `Membres` et `⚙ Paramètres` depuis l'en-tête de l'inventaire.

### Dialogue de nouvelle réservation
Choix du stack (nom + référence), début / fin (inputs datetime, pas de 15 min),
résumé « une unité sur N sera attribuée automatiquement ». À la soumission :
`POST` → succès = toast + rafraîchissement du calendrier ; `409 STACK_FULL` =
message « Toutes les unités sont réservées sur ce créneau » avec `unitsBusy/unitsTotal`.

### Affichage calendrier
Les créneaux occupés du stack sélectionné apparaissent grisés / non cliquables.
Filtre en haut : vue par stack ou vue « tout l'inventaire ».

### Tests
Vitest + React Testing Library : dialogue de réservation (succès + conflit) et
rendu du calendrier depuis un flux simulé.

## 7. Worker Node.js (rappels)

Dossier `worker/` autonome (`package.json` séparé). **Aucun accès direct à la
base** : il ne parle qu'à l'API .NET.

### Fonctionnement
1. `node-cron` déclenche la tâche toutes les heures (`CRON_SCHEDULE`, défaut `0 * * * *`).
2. `GET /api/internal/reservations/upcoming?windowHours=24` avec l'en-tête
   `X-Internal-Api-Key`. L'API renvoie les réservations `Confirmed` démarrant dans
   `[maintenant+23 h, maintenant+25 h]` **et** dont `ReminderSentAt` est nul, avec
   `userEmail`, `userDisplayName`, `equipmentName`, `reference`, `inventoryName`,
   `startsAt`, `endsAt`.
3. Pour chaque réservation : `nodemailer` envoie l'e-mail via SMTP Mailtrap
   (sandbox). Template texte + HTML simple.
4. Après envoi réussi : `POST /api/internal/reservations/{id}/reminder-sent` →
   l'API pose `ReminderSentAt`. **Idempotent** : un rappel déjà marqué n'est jamais
   renvoyé.
5. Échec d'un e-mail → log d'erreur structuré, la boucle continue (pas de crash) ;
   `ReminderSentAt` reste nul → réessai au passage suivant.

### Config (`.env`)
`API_BASE_URL`, `INTERNAL_API_KEY`, `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`,
`SMTP_PASS`, `MAIL_FROM`, `REMINDER_WINDOW_HOURS=24`, `CRON_SCHEDULE`.

### Tests
Vitest : API simulée + transport `nodemailer` en mode capture. Vérifie le
filtrage de fenêtre, l'appel `reminder-sent`, la résilience à un échec unitaire.

## 8. Découpage en phases d'implémentation

Chaque phase = un lot cohérent, vérifié (`dotnet build` + `dotnet test` /
`npm test` + `npx tsc --noEmit`) avant de passer à la suivante.

| # | Phase | Contenu |
|---|---|---|
| 1 | Fondations | Monorepo, `docker-compose` (Postgres 17 + `btree_gist`), squelette solution .NET, `DbContext` EF Core, 1re migration (7 tables + contrainte d'exclusion), seed de démo |
| 2 | Auth | register / login / JWT, génération `UserCode`, `GET users/me`, `users/search?code=`, hash PBKDF2 + tests |
| 3 | Inventaires & membres | CRUD inventaires (dont `PATCH` nom/description), `Membership`, policies Member/Admin/Owner, transfert de propriété + tests |
| 4 | Invitations | création par code utilisateur, accept / decline / revoke, unicité des invitations `Pending` + tests |
| 5 | Matériel | CRUD `Equipment`, regroupement en stacks (`?grouped=true`), statuts + tests |
| 6 | Réservations | création avec advisory lock + test de chevauchement + contrainte d'exclusion, flux calendrier, annulation, **tests de concurrence** (Testcontainers) |
| 7 | Route interne | `reservations/upcoming`, colonne `ReminderSentAt`, `reminder-sent`, auth par clé d'API |
| 8 | Front — socle | scaffold Vite, `AuthProvider`, axios+JWT, pages login/register, liste inventaires, switcher, `/profile`, `/invitations` |
| 9 | Front — matériel | tableau de bord inventaire, stacks, détail/édition fiche |
| 10 | Front — calendrier | `react-big-calendar`, dialogue de réservation, UX conflit `409` |
| 11 | Front — membres & paramètres | gestion membres + rôles, invitation par code + recherche, page `/inventory/:id/settings` |
| 12 | Worker Node | `node-cron` + `nodemailer` + Mailtrap + `reminder-sent` + tests |
| 13 | Finition | `README`, `.env.example`, validation `docker compose up` de bout en bout |

## 9. Hypothèses (à ajuster si besoin en cours de route)

- `.NET 10 LTS` et `React 19` sont les versions cibles ; ajustables au scaffold.
- Pas de refresh token : session de 8 h puis reconnexion.
- Durée max de réservation désactivée par défaut (paramètre présent mais inactif).
- Une fiche `Equipment` supprimée alors qu'elle porte des réservations : à trancher
  en phase 5 (soft-delete via `Status = Retired` recommandé plutôt que suppression dure).
- Le champ `Informations` est affiché en texte brut (pas de markdown/HTML) pour éviter tout risque d'injection.
