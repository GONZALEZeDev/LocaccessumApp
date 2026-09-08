# Locaccessum

Système d'inventaire et de réservation de matériel partagé : s'inscrire, rejoindre un
inventaire, et réserver des unités de matériel sans double réservation.

Ce dépôt ne contient actuellement **que le backend** — une API Web .NET et sa couche de
données. Il n'y a pas encore de frontend ni de worker de rappels ; les deux sont des plans
séparés, pas encore construits.

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

---

<!-- English version below -->

# Locaccessum

Shared-equipment inventory and booking system: register, join an inventory, and reserve
equipment stacks without double-booking.

This repository currently contains **the backend only** — a .NET Web API and its data layer.
There is no frontend and no reminder worker yet; both are separate, not-yet-built plans.

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
