# Locaccessum

Système d'inventaire et de réservation de matériel partagé : s'inscrire, rejoindre un
inventaire, et réserver des unités de matériel sans double réservation.

Ce dépôt contient **le backend et le worker de rappels** — une API Web .NET et sa couche de
données, ainsi qu'un service Node.js qui envoie des rappels par e-mail. Il n'y a pas encore de
frontend ; c'est un plan séparé, pas encore construit.

## Prérequis

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (pour `docker compose up`,
  et pour les tests d'intégration de la suite de tests, basés sur Testcontainers)
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (pour compiler/tester en dehors de Docker)

## Démarrage rapide

```bash
cp .env.example .env
docker compose up --build
```

Cette commande construit l'image de l'API, démarre Postgres, attend que Postgres soit signalé
comme sain, applique les migrations EF Core et charge des données de démonstration au démarrage
(environnement de développement uniquement), puis expose l'API sur `http://localhost:8080`.
Vérifiez `GET http://localhost:8080/health` pour obtenir `{"status":"ok"}`.

## Identifiants de démonstration

Deux utilisateurs sont créés dans l'environnement de développement au premier démarrage :

| E-mail | Mot de passe |
|---|---|
| `alice@locaccessum.dev` | `Passw0rd!` |
| `bob@locaccessum.dev` | `Passw0rd!` |

## Lancer la suite de tests

```bash
cd backend
dotnet test
```

Docker doit être en cours d'exécution : la suite de tests d'intégration démarre une vraie
instance Postgres à chaque lancement via [Testcontainers](https://testcontainers.com/).

## Protection anti-double-réservation

La réservation est protégée sur deux niveaux. Lorsqu'une demande de réservation arrive, le
service de réservation ouvre une transaction en base et prend un verrou avisoire Postgres
(`pg_advisory_xact_lock`) basé sur l'identité du stack de matériel (inventaire + nom +
référence), ce qui sérialise les tentatives de réservation concurrentes sur le même stack afin
qu'une seule requête à la fois évalue quelle unité est libre. En filet de sécurité au niveau de
la base — au cas où cette sérialisation serait un jour contournée — une contrainte d'exclusion
GiST sur la table des réservations rejette d'office toute réservation confirmée qui chevauche
une autre sur la même unité de matériel (erreur Postgres `23P01`), que le service de réservation
intercepte et retraduit en "stack complet" plutôt que de laisser remonter une erreur de base de
données.

## Configuration

Variables d'environnement (voir `.env.example`) :

| Variable | Rôle |
|---|---|
| `POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_DB` | Identifiants/base du conteneur Postgres |
| `ConnectionStrings__Default` | Chaîne de connexion EF Core (utilisée en dehors de Docker) |
| `Jwt__Issuer` / `Jwt__Audience` / `Jwt__Secret` / `Jwt__LifetimeHours` | Paramètres JWT (durée de vie 8h, pas de rafraîchissement) |
| `InternalApiKey` | Clé d'API pour les endpoints internes du worker de rappels |
| `Cors__FrontOrigin` | Origine CORS autorisée pour le (futur) frontend, par défaut `http://localhost:5173` |

## Worker de rappels

Le worker de rappels (`worker/`) est un service Node.js/TypeScript qui tourne indépendamment de
l'API. Selon une planification horaire (configurable), il interroge l'API pour les réservations
qui commencent dans la fenêtre à venir (24h par défaut) et envoie un e-mail de rappel à
l'utilisateur concerné, puis marque la réservation comme « rappel envoyé » pour ne pas la
renvoyer. Si un envoi échoue, le worker le journalise et continue sans marquer la réservation,
qui sera automatiquement retentée au passage suivant.

Pour le lancer seul, en dehors de `docker compose up` :

```bash
cd worker
cp .env.example .env
npm install
npm run build && npm start
```

Avec `docker compose up`, le worker lit directement les variables du `.env` racine du dépôt (voir
`.env.example`).

Point important : `INTERNAL_API_KEY` (worker) doit être **identique** à `InternalApiKey` (API
backend), sinon les appels du worker vers l'API échoueront avec une erreur d'authentification.

Par défaut, le SMTP pointe vers un bac à sable Mailtrap (`sandbox.smtp.mailtrap.io`) — aucun
e-mail réel n'est envoyé aux utilisateurs pendant le développement ; les e-mails sont capturés par
Mailtrap pour inspection.

---

<!-- English version below -->

# Locaccessum

Shared-equipment inventory and booking system: register, join an inventory, and reserve
equipment stacks without double-booking.

This repository contains **the backend and the reminder worker** — a .NET Web API and its data
layer, plus a Node.js service that sends e-mail reminders. There is no frontend yet; that's a
separate, not-yet-built plan.

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for `docker compose up`, and for
  the test suite's Testcontainers-based integration tests)
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (for building/testing outside of Docker)

## Quick start

```bash
cp .env.example .env
docker compose up --build
```

This builds the API image, starts Postgres, waits for Postgres to report healthy, applies EF Core
migrations and seeds demo data on startup (development environment only), and exposes the API on
`http://localhost:8080`. Check `GET http://localhost:8080/health` for `{"status":"ok"}`.

## Seeded demo credentials

Two users are seeded in the Development environment on first run:

| Email | Password |
|---|---|
| `alice@locaccessum.dev` | `Passw0rd!` |
| `bob@locaccessum.dev` | `Passw0rd!` |

## Running the test suite

```bash
cd backend
dotnet test
```

Docker must be running: the integration test suite spins up a real Postgres instance per test run
via [Testcontainers](https://testcontainers.com/).

## Anti-double-booking protection

Booking is protected in two layers. When a reservation request comes in, the booking service opens
a database transaction and takes a Postgres advisory lock (`pg_advisory_xact_lock`) keyed on the
equipment stack's identity (inventory + name + reference), which serializes concurrent booking
attempts against the same stack so only one request at a time evaluates which unit is free. As a
database-level backstop — in case that serialization is ever bypassed — a GiST exclusion constraint
on the reservations table rejects any overlapping, confirmed reservation for the same equipment unit
outright (Postgres error `23P01`), which the booking service catches and reports back as the stack
being full rather than surfacing a database error.

## Configuration

Environment variables (see `.env.example`):

| Variable | Purpose |
|---|---|
| `POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_DB` | Postgres container credentials/database |
| `ConnectionStrings__Default` | EF Core connection string (used when running the API outside Docker) |
| `Jwt__Issuer` / `Jwt__Audience` / `Jwt__Secret` / `Jwt__LifetimeHours` | JWT auth settings (8h lifetime, no refresh) |
| `InternalApiKey` | API key for the internal reminder-worker endpoints |
| `Cors__FrontOrigin` | Allowed CORS origin for the (future) frontend, default `http://localhost:5173` |

## Reminder worker

The reminder worker (`worker/`) is a standalone Node.js/TypeScript service. On an hourly schedule
(configurable), it polls the API for reservations starting within the upcoming window (24h by
default) and sends a reminder e-mail to the corresponding user, then marks the reservation as
"reminder sent" so it isn't resent. If a send fails, the worker logs it and moves on without
marking the reservation, which is retried automatically on the next run.

To run it standalone, outside of `docker compose up`:

```bash
cd worker
cp .env.example .env
npm install
npm run build && npm start
```

With `docker compose up`, the worker reads its variables directly from the repo root's `.env`
(see `.env.example`).

Important: `INTERNAL_API_KEY` (worker) must **match** `InternalApiKey` (backend API) exactly, or
the worker's calls to the API will fail authentication.

By default, SMTP points at a Mailtrap sandbox (`sandbox.smtp.mailtrap.io`) — no real e-mails are
sent to users during development; e-mails are captured by Mailtrap for inspection.
