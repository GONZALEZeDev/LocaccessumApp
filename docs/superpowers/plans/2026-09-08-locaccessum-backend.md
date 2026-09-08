# Locaccessum Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Locaccessum .NET API — PostgreSQL persistence, JWT auth, shared inventories with roles/invitations, generic equipment with computed "stacks", and strict anti-double-booking reservation logic — plus the internal endpoint the Node reminder worker consumes.

**Architecture:** ASP.NET Core Web API (controllers) over EF Core 10 / Npgsql against PostgreSQL 17. Clean-ish layering: `Domain` (POCO entities + enums + pure overlap logic), `Infrastructure` (DbContext, migrations, JWT, hashing, seed), `Api` (controllers, DTOs, authorization policies). Double-layer conflict protection: a per-stack Postgres advisory lock serialises concurrent booking attempts, and a GiST exclusion constraint makes an overlapping `Confirmed` reservation physically impossible in the database. Integration tests run against a real PostgreSQL container via Testcontainers.

**Tech Stack:** .NET 10 (SDK `10.0.2xx`), ASP.NET Core Web API, EF Core 10, Npgsql.EntityFrameworkCore.PostgreSQL 10, EFCore.NamingConventions (snake_case), `Microsoft.AspNetCore.Identity.PasswordHasher`, `System.IdentityModel.Tokens.Jwt`, xUnit, Testcontainers.PostgreSql 4.x, Respawn 6.x, FluentAssertions. Docker Compose for the runtime stack.

**Spec:** `docs/superpowers/specs/2026-09-08-locaccessum-design.md` (read it alongside this plan).

## Global Constraints

- **Deployment model:** single self-hosted instance. NOT multi-tenant SaaS. Inventories are internal workspaces; user accounts are global.
- **.NET 10 LTS**, C# 13, `Nullable` enabled, `TreatWarningsAsErrors` = true on all projects.
- **PostgreSQL 17** only. Required extension: `btree_gist`. No provider-agnostic fallback (SQLite/InMemory) anywhere.
- **snake_case** for every table and column name (via `UseSnakeCaseNamingConvention()`). Raw SQL in migrations and services assumes snake_case identifiers.
- **Equipment fiche shape is fixed:** `{ Id, InventoryId, Name, Reference, Informations, Status, CreatedAt }`. `Informations` is a single free-text column, rendered/returned as plain text — never interpreted as HTML/markdown.
- **A "stack" is never a table.** It is `GROUP BY (inventory_id, name, reference)` over `equipment`.
- **Reservation rule:** one physical unit, one time slot. Overlap test is half-open: `existing.starts_at < req.ends_at AND existing.ends_at > req.starts_at`.
- **Roles per inventory:** `Owner` | `Admin` | `Member`. Exactly one `Owner` per inventory at all times.
- **Invitation roles:** `Admin` | `Member` only (never `Owner`).
- **JWT:** HS256, secret from configuration/env, 8-hour lifetime, no refresh token.
- **Internal worker auth:** header `X-Internal-Api-Key`, value from configuration/env, separate from JWT.
- **Every code step is TDD:** write the failing test, watch it fail, implement minimally, watch it pass, commit. Commit after every task.
- Conventional-commit prefixes: `feat:`, `test:`, `chore:`, `fix:`, `refactor:`.

---

## File Structure

```
Locaccessum/
├─ .gitignore                                  # VS/.NET + node + .env
├─ .editorconfig                               # C# style, snake_case not enforced here
├─ Directory.Build.props                       # shared: net10.0, nullable, warnaserror
├─ docker-compose.yml                          # postgres + api (web/worker added by later plans)
├─ docker-compose.override.yml                 # dev: ports, hot-reload, mounted source
├─ .env.example                                # POSTGRES_*, JWT__*, INTERNALAPIKEY, ASPNETCORE_*
├─ README.md                                   # backend section (Task 24)
└─ backend/
   ├─ Locaccessum.sln
   ├─ Locaccessum.Domain/
   │  ├─ Locaccessum.Domain.csproj             # no package refs
   │  ├─ Entities/User.cs
   │  ├─ Entities/Inventory.cs
   │  ├─ Entities/Membership.cs
   │  ├─ Entities/Invitation.cs
   │  ├─ Entities/Equipment.cs
   │  ├─ Entities/Reservation.cs
   │  └─ Enums/MembershipRole.cs
   │     Enums/InvitationStatus.cs
   │     Enums/EquipmentStatus.cs
   │     Enums/ReservationStatus.cs
   ├─ Locaccessum.Infrastructure/
   │  ├─ Locaccessum.Infrastructure.csproj     # EF Core, Npgsql, NamingConventions, Identity.PasswordHasher, JwtBearer
   │  ├─ Persistence/LocaccessumDbContext.cs
   │  ├─ Persistence/Configurations/*Configuration.cs   # one IEntityTypeConfiguration per entity
   │  ├─ Persistence/DesignTimeDbContextFactory.cs
   │  ├─ Persistence/Migrations/*                # generated
   │  ├─ Persistence/DbSeeder.cs                 # Task 23
   │  ├─ Auth/PasswordHasher.cs                  # IPasswordHasher impl
   │  ├─ Auth/JwtTokenService.cs                 # IJwtTokenService impl
   │  ├─ Auth/JwtOptions.cs
   │  ├─ Identity/UserCodeGenerator.cs           # IUserCodeGenerator impl
   │  └─ InfrastructureServiceCollectionExtensions.cs  # AddInfrastructure(config)
   ├─ Locaccessum.Api/
   │  ├─ Locaccessum.Api.csproj
   │  ├─ Program.cs
   │  ├─ appsettings.json  appsettings.Development.json
   │  ├─ Abstractions/IPasswordHasher.cs  IJwtTokenService.cs  IUserCodeGenerator.cs  ICurrentUser.cs
   │  ├─ Common/CurrentUser.cs                   # reads ClaimsPrincipal
   │  ├─ Common/ApiProblem.cs                    # ProblemDetails helpers, error codes
   │  ├─ Contracts/Auth/*.cs                     # request/response records
   │  ├─ Contracts/Inventories/*.cs
   │  ├─ Contracts/Members/*.cs
   │  ├─ Contracts/Invitations/*.cs
   │  ├─ Contracts/Equipment/*.cs
   │  ├─ Contracts/Reservations/*.cs
   │  ├─ Authorization/InventoryRole.cs          # enum-like policy constants
   │  ├─ Authorization/InventoryRoleRequirement.cs
   │  ├─ Authorization/InventoryRoleHandler.cs
   │  ├─ Authorization/InternalApiKeyAuthHandler.cs   # AuthenticationHandler for the "Internal" scheme
   │  ├─ Controllers/AuthController.cs
   │  ├─ Controllers/UsersController.cs
   │  ├─ Controllers/InventoriesController.cs
   │  ├─ Controllers/MembersController.cs
   │  ├─ Controllers/InvitationsController.cs
   │  ├─ Controllers/EquipmentController.cs
   │  ├─ Controllers/ReservationsController.cs
   │  ├─ Controllers/InternalReservationsController.cs
   │  └─ Services/ReservationBookingService.cs   # the conflict logic (Task 19)
   └─ Locaccessum.Tests/
      ├─ Locaccessum.Tests.csproj                # xUnit, Testcontainers.PostgreSql, Respawn, FluentAssertions, Mvc.Testing
      ├─ Infrastructure/PostgresFixture.cs       # Testcontainers lifetime
      ├─ Infrastructure/ApiFactory.cs            # WebApplicationFactory<Program> wired to the container
      ├─ Infrastructure/DatabaseCollection.cs    # [CollectionDefinition] shared fixture
      ├─ Infrastructure/TestDataFactory.cs       # helpers: CreateUserAsync, CreateInventoryAsync, AuthClientAsync...
      ├─ Domain/ReservationOverlapTests.cs
      ├─ Auth/PasswordHasherTests.cs  JwtTokenServiceTests.cs  UserCodeGeneratorTests.cs
      ├─ Api/HealthTests.cs
      ├─ Api/RegisterTests.cs  LoginTests.cs  UsersMeTests.cs  UserSearchTests.cs
      ├─ Api/InventoriesTests.cs  InventoryAuthorizationTests.cs  InventorySettingsTests.cs
      ├─ Api/MembersTests.cs
      ├─ Api/InvitationsTests.cs
      ├─ Api/EquipmentTests.cs  EquipmentStackTests.cs
      ├─ Api/ReservationCalendarTests.cs
      ├─ Api/ReservationBookingTests.cs
      ├─ Api/ReservationConcurrencyTests.cs
      ├─ Api/ReservationCancelTests.cs
      ├─ Api/InternalReservationsTests.cs
      └─ Persistence/SchemaTests.cs  SeederTests.cs
```

**Program.cs must expose the `Program` class to the test project** — end the file with `public partial class Program;` so `WebApplicationFactory<Program>` compiles.

---

## Task 1: Solution & container scaffold

**Files:**
- Create: `.gitignore`, `.editorconfig`, `Directory.Build.props`, `docker-compose.yml`, `docker-compose.override.yml`, `.env.example`
- Create: `backend/Locaccessum.sln` and the 4 projects (`Domain`, `Infrastructure`, `Api`, `Tests`)
- Create: `backend/Locaccessum.Api/Program.cs`, `appsettings.json`, `appsettings.Development.json`
- Test: `backend/Locaccessum.Tests/Api/HealthTests.cs` (placeholder unit test only in this task)

**Interfaces:**
- Consumes: nothing.
- Produces: solution builds; `Program` partial class exists; `/health` route returns `200 { "status": "ok" }`.

- [ ] **Step 1: Create the repo-level files**

`Directory.Build.props`:
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
</Project>
```

`.gitignore` (minimum): `bin/`, `obj/`, `*.user`, `.env`, `node_modules/`, `.vs/`, `TestResults/`.

`.env.example`:
```
POSTGRES_USER=locaccessum
POSTGRES_PASSWORD=locaccessum
POSTGRES_DB=locaccessum
ConnectionStrings__Default=Host=localhost;Port=5432;Database=locaccessum;Username=locaccessum;Password=locaccessum
Jwt__Issuer=locaccessum
Jwt__Audience=locaccessum
Jwt__Secret=dev-only-change-me-0123456789abcdef0123456789abcdef
Jwt__LifetimeHours=8
InternalApiKey=dev-internal-key-change-me
ASPNETCORE_ENVIRONMENT=Development
```

- [ ] **Step 2: Scaffold the solution**

Run:
```bash
cd backend
dotnet new sln -n Locaccessum
dotnet new classlib -n Locaccessum.Domain -o Locaccessum.Domain
dotnet new classlib -n Locaccessum.Infrastructure -o Locaccessum.Infrastructure
dotnet new webapi -n Locaccessum.Api -o Locaccessum.Api --use-controllers
dotnet new xunit -n Locaccessum.Tests -o Locaccessum.Tests
dotnet sln add Locaccessum.Domain Locaccessum.Infrastructure Locaccessum.Api Locaccessum.Tests
dotnet add Locaccessum.Infrastructure reference Locaccessum.Domain
dotnet add Locaccessum.Api reference Locaccessum.Infrastructure Locaccessum.Domain
dotnet add Locaccessum.Tests reference Locaccessum.Api Locaccessum.Infrastructure Locaccessum.Domain
rm Locaccessum.Domain/Class1.cs Locaccessum.Infrastructure/Class1.cs
```

Delete the generated `WeatherForecast*` files from `Locaccessum.Api`.

- [ ] **Step 3: Add Api + Tests package references**

Run:
```bash
cd backend
dotnet add Locaccessum.Api package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add Locaccessum.Api package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add Locaccessum.Api package Microsoft.EntityFrameworkCore.Design
dotnet add Locaccessum.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add Locaccessum.Infrastructure package EFCore.NamingConventions
dotnet add Locaccessum.Infrastructure package Microsoft.Extensions.Identity.Core
dotnet add Locaccessum.Infrastructure package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add Locaccessum.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet add Locaccessum.Tests package Testcontainers.PostgreSql
dotnet add Locaccessum.Tests package Respawn
dotnet add Locaccessum.Tests package FluentAssertions
```

- [ ] **Step 4: Write `Program.cs`**

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
```

- [ ] **Step 5: Write the placeholder test**

`Locaccessum.Tests/Api/HealthTests.cs`:
```csharp
public class HealthTests
{
    [Fact]
    public void Placeholder_solution_builds()
    {
        true.Should().BeTrue();
    }
}
```

- [ ] **Step 6: Build & test**

Run: `cd backend && dotnet build && dotnet test`
Expected: build succeeds with 0 warnings, 1 test passes.

- [ ] **Step 7: Write `docker-compose.yml`**

```yaml
services:
  postgres:
    image: postgres:17
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: ${POSTGRES_DB}
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER} -d ${POSTGRES_DB}"]
      interval: 5s
      timeout: 5s
      retries: 10

  api:
    build:
      context: ./backend
      dockerfile: Locaccessum.Api/Dockerfile
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      ConnectionStrings__Default: Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
      Jwt__Issuer: ${Jwt__Issuer}
      Jwt__Audience: ${Jwt__Audience}
      Jwt__Secret: ${Jwt__Secret}
      Jwt__LifetimeHours: ${Jwt__LifetimeHours}
      InternalApiKey: ${InternalApiKey}
      ASPNETCORE_ENVIRONMENT: Development
    ports:
      - "8080:8080"

volumes:
  pgdata:
```

`docker-compose.override.yml` (dev convenience — bind mount + `dotnet watch` is optional; keep it minimal):
```yaml
services:
  postgres:
    ports:
      - "5432:5432"
```

Add a minimal `backend/Locaccessum.Api/Dockerfile` (multi-stage `mcr.microsoft.com/dotnet/sdk:10.0` → `mcr.microsoft.com/dotnet/aspnet:10.0`, `dotnet publish -c Release`, `ENTRYPOINT ["dotnet","Locaccessum.Api.dll"]`, `EXPOSE 8080`, `ENV ASPNETCORE_HTTP_PORTS=8080`).

- [ ] **Step 8: Validate compose config**

Run: `cp .env.example .env && docker compose config`
Expected: prints a merged config with no errors. (Do NOT run `up` yet — no migrations exist.)

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "chore: scaffold solution, projects, docker compose, health endpoint"
```

---

## Task 2: Domain entities & enums

**Files:**
- Create: `Locaccessum.Domain/Enums/MembershipRole.cs`, `InvitationStatus.cs`, `EquipmentStatus.cs`, `ReservationStatus.cs`
- Create: `Locaccessum.Domain/Entities/User.cs`, `Inventory.cs`, `Membership.cs`, `Invitation.cs`, `Equipment.cs`, `Reservation.cs`
- Test: `Locaccessum.Tests/Domain/ReservationOverlapTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - Enums: `MembershipRole { Owner, Admin, Member }`, `InvitationStatus { Pending, Accepted, Declined, Revoked }`, `EquipmentStatus { Active, Maintenance, Retired }`, `ReservationStatus { Confirmed, Cancelled }`.
  - Entities with public get/set properties matching spec §4. Guids for all ids, generated by the app (`Guid.CreateVersion7()`).
  - `static bool Reservation.Overlaps(DateTimeOffset aStart, DateTimeOffset aEnd, DateTimeOffset bStart, DateTimeOffset bEnd)` — half-open overlap. Used by `ReservationBookingService` and tests.

- [ ] **Step 1: Write the failing test**

`Locaccessum.Tests/Domain/ReservationOverlapTests.cs`:
```csharp
using Locaccessum.Domain.Entities;

public class ReservationOverlapTests
{
    static DateTimeOffset T(int h) => new(2026, 1, 1, h, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(9, 11, 10, 12, true)]   // partial overlap
    [InlineData(9, 12, 10, 11, true)]   // b inside a
    [InlineData(9, 10, 10, 11, false)]  // touching at the boundary -> no overlap (half-open)
    [InlineData(9, 10, 11, 12, false)]  // disjoint
    public void Overlaps_is_half_open(int as, int ae, int bs, int be, bool expected)
    {
        Reservation.Overlaps(T(as), T(ae), T(bs), T(be)).Should().Be(expected);
    }
}
```

- [ ] **Step 2: Run test — expect FAIL** (`Reservation` does not exist).

Run: `cd backend && dotnet test --filter ReservationOverlapTests`

- [ ] **Step 3: Write the enums and entities**

Enums are plain `public enum` in namespace `Locaccessum.Domain.Enums`.

`Entities/Reservation.cs`:
```csharp
using Locaccessum.Domain.Enums;

namespace Locaccessum.Domain.Entities;

public class Reservation
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public Equipment Equipment { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Confirmed;
    public DateTimeOffset? ReminderSentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    public static bool Overlaps(DateTimeOffset aStart, DateTimeOffset aEnd, DateTimeOffset bStart, DateTimeOffset bEnd)
        => aStart < bEnd && aEnd > bStart;
}
```

Write the other entities per spec §4 (`User` with `Email`, `PasswordHash`, `DisplayName`, `UserCode`, `CreatedAt`; `Inventory` with `Name`, `Description`, `OwnerId`, `Owner`, `CreatedAt`, `ICollection<Membership> Memberships`, `ICollection<Equipment> Equipment`; `Membership` with `InventoryId`, `UserId`, `Role`, `JoinedAt`; `Invitation` with `InventoryId`, `InvitedUserId`, `InvitedByUserId`, `Role`, `Status`, `CreatedAt`, `RespondedAt`; `Equipment` with `InventoryId`, `Name`, `Reference`, `Informations`, `Status`, `CreatedAt`, `ICollection<Reservation> Reservations`). Reference navigation + FK id on every relationship.

- [ ] **Step 4: Run test — expect PASS.**

Run: `cd backend && dotnet test --filter ReservationOverlapTests`

- [ ] **Step 5: Commit**

```bash
git add backend/Locaccessum.Domain backend/Locaccessum.Tests/Domain
git commit -m "feat: domain entities, enums, half-open overlap helper"
```

---

## Task 3: DbContext, configurations, initial migration, Postgres test harness

**Files:**
- Create: `Locaccessum.Infrastructure/Persistence/LocaccessumDbContext.cs`
- Create: `Locaccessum.Infrastructure/Persistence/Configurations/{User,Inventory,Membership,Invitation,Equipment,Reservation}Configuration.cs`
- Create: `Locaccessum.Infrastructure/Persistence/DesignTimeDbContextFactory.cs`
- Create: `Locaccessum.Infrastructure/InfrastructureServiceCollectionExtensions.cs`
- Create (generated): `Locaccessum.Infrastructure/Persistence/Migrations/*_InitialCreate.cs`
- Create: `Locaccessum.Tests/Infrastructure/PostgresFixture.cs`, `DatabaseCollection.cs`
- Create: `Locaccessum.Tests/Persistence/SchemaTests.cs`

**Interfaces:**
- Consumes: Domain entities (Task 2).
- Produces:
  - `LocaccessumDbContext(DbContextOptions<LocaccessumDbContext>)` with `DbSet<User> Users`, `Inventories`, `Memberships`, `Invitations`, `Equipment`, `Reservations`.
  - `IServiceCollection.AddInfrastructure(IConfiguration config)` — registers `LocaccessumDbContext` using `ConnectionStrings:Default`, `.UseNpgsql(...).UseSnakeCaseNamingConvention()`.
  - `PostgresFixture : IAsyncLifetime` — starts a `postgres:17` container, exposes `string ConnectionString`, applies migrations once. `[Collection("db")]` via `DatabaseCollection`.
  - Migration name: `InitialCreate`. Creates `btree_gist` extension + the exclusion constraint `reservations_no_overlap`.

- [ ] **Step 1: Write `LocaccessumDbContext` + configurations**

DbContext applies configurations from assembly: `modelBuilder.ApplyConfigurationsFromAssembly(typeof(LocaccessumDbContext).Assembly);`

Configuration highlights (put the exact constraints here so the migration generates them):
- `User`: `HasIndex(u => u.Email).IsUnique()`; `HasIndex(u => u.UserCode).IsUnique()`; `Property(u => u.Email).HasMaxLength(320)`; `UserCode` max length 20; `DisplayName` max 100.
- `Inventory`: `Name` max 120 required; `Description` max 2000 nullable; `HasOne(i => i.Owner).WithMany().HasForeignKey(i => i.OwnerId).OnDelete(DeleteBehavior.Restrict)`.
- `Membership`: `HasIndex(m => new { m.InventoryId, m.UserId }).IsUnique()`; both FKs `OnDelete(DeleteBehavior.Cascade)` from Inventory, `Restrict` from User; `Role` stored as `string` via `.HasConversion<string>()`.
- `Invitation`: `Status`/`Role` `.HasConversion<string>()`; filtered unique index for pending:
  `HasIndex(x => new { x.InventoryId, x.InvitedUserId }).IsUnique().HasFilter("status = 'Pending'")`.
- `Equipment`: `Name` max 120 required; `Reference` max 120 required; `Informations` — `column type text`, nullable; `Status` `.HasConversion<string>()`; `HasIndex(e => new { e.InventoryId, e.Name, e.Reference })`.
- `Reservation`: `Status` `.HasConversion<string>()`; `HasIndex(r => new { r.EquipmentId, r.StartsAt, r.EndsAt })`; `ToTable(t => t.HasCheckConstraint("reservations_time_order", "ends_at > starts_at"))`.

All `DateTimeOffset` map to `timestamptz` by default with Npgsql — fine.

- [ ] **Step 2: Add the exclusion constraint in the migration**

Generate the migration:
```bash
cd backend
dotnet ef migrations add InitialCreate -p Locaccessum.Infrastructure -s Locaccessum.Api -o Persistence/Migrations
```

Then hand-edit `Up()` — at the very top add:
```csharp
migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
```
and at the very bottom add:
```csharp
migrationBuilder.Sql(@"
    ALTER TABLE reservations
    ADD CONSTRAINT reservations_no_overlap
    EXCLUDE USING gist (
        equipment_id WITH =,
        tstzrange(starts_at, ends_at) WITH &&
    ) WHERE (status = 'Confirmed');");
```
In `Down()` add `migrationBuilder.Sql("ALTER TABLE reservations DROP CONSTRAINT IF EXISTS reservations_no_overlap;");` before the generated drops.

- [ ] **Step 3: Write `DesignTimeDbContextFactory`**

Reads `ConnectionStrings__Default` env var (fallback to a localhost default) so `dotnet ef` works without the API running.

- [ ] **Step 4: Write the test harness**

`Locaccessum.Tests/Infrastructure/PostgresFixture.cs`:
```csharp
using Testcontainers.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Locaccessum.Infrastructure.Persistence;

public sealed class PostgresFixture : IAsyncLifetime
{
    readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var options = new DbContextOptionsBuilder<LocaccessumDbContext>()
            .UseNpgsql(ConnectionString).UseSnakeCaseNamingConvention().Options;
        await using var db = new LocaccessumDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition("db")]
public class DatabaseCollection : ICollectionFixture<PostgresFixture>;
```

- [ ] **Step 5: Write the failing schema test**

`Locaccessum.Tests/Persistence/SchemaTests.cs`:
```csharp
[Collection("db")]
public class SchemaTests(PostgresFixture fx)
{
    [Fact]
    public async Task Exclusion_constraint_exists()
    {
        await using var conn = new Npgsql.NpgsqlConnection(fx.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand(
            "select 1 from pg_constraint where conname = 'reservations_no_overlap'", conn);
        var found = await cmd.ExecuteScalarAsync();
        found.Should().NotBeNull();
    }
}
```

- [ ] **Step 6: Run — expect FAIL, then PASS after Steps 1–2 land.**

Run: `cd backend && dotnet test --filter SchemaTests`
Expected first run: FAIL (no migration / constraint). After implementing: PASS. Requires Docker running.

- [ ] **Step 7: Commit**

```bash
git add backend/Locaccessum.Infrastructure backend/Locaccessum.Tests/Infrastructure backend/Locaccessum.Tests/Persistence
git commit -m "feat: dbcontext, configurations, initial migration with gist exclusion constraint"
```

---

## Task 4: API integration harness

**Files:**
- Modify: `Locaccessum.Api/Program.cs` (wire `AddInfrastructure`, apply migrations on startup in Development)
- Create: `Locaccessum.Tests/Infrastructure/ApiFactory.cs`, `TestDataFactory.cs`
- Modify: `Locaccessum.Tests/Api/HealthTests.cs` (replace placeholder with a real integration test)

**Interfaces:**
- Consumes: `PostgresFixture` (Task 3), `AddInfrastructure` (Task 3).
- Produces:
  - `ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime` — ctor takes `PostgresFixture`; overrides config so the app uses the container connection string + fixed test `Jwt:Secret` + fixed `InternalApiKey`; resets the DB between tests via Respawn (`ResetAsync`).
  - `TestDataFactory` static helpers (async, take `ApiFactory`): `Task<(Guid userId, string token)> RegisterAsync(email, password="Passw0rd!")`, `Task<HttpClient> ClientAsync(string? token = null)`, `Task<Guid> CreateInventoryAsync(string token, string name)`.

- [ ] **Step 1: Wire infrastructure in `Program.cs`**

```csharp
builder.Services.AddInfrastructure(builder.Configuration);
// ... after build:
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<LocaccessumDbContext>().Database.Migrate();
}
```

- [ ] **Step 2: Write `ApiFactory`**

Override `ConfigureWebHost`: `builder.UseSetting("ConnectionStrings:Default", _fx.ConnectionString)`, set `Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience`, `Jwt:LifetimeHours=8`, `InternalApiKey=test-internal-key`. `InitializeAsync` opens a Respawner on the connection (tables to ignore: `__EFMigrationsHistory`). Expose `Task ResetAsync()`.

- [ ] **Step 3: Write the failing health integration test**

```csharp
[Collection("db")]
public class HealthTests(PostgresFixture fx) : IAsyncLifetime
{
    ApiFactory _factory = null!;
    public async Task InitializeAsync() { _factory = new ApiFactory(fx); await _factory.InitializeAsync(); }
    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task Health_returns_ok()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/health");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().Contain("ok");
    }
}
```

- [ ] **Step 4: Run — expect PASS.** `cd backend && dotnet test --filter HealthTests`

- [ ] **Step 5: Commit**

```bash
git add backend/Locaccessum.Api/Program.cs backend/Locaccessum.Tests
git commit -m "test: api integration harness against testcontainers postgres"
```

---

## Task 5: Password hashing & JWT token service

**Files:**
- Create: `Locaccessum.Api/Abstractions/IPasswordHasher.cs`, `IJwtTokenService.cs`
- Create: `Locaccessum.Infrastructure/Auth/PasswordHasher.cs`, `JwtTokenService.cs`, `JwtOptions.cs`
- Modify: `Locaccessum.Infrastructure/InfrastructureServiceCollectionExtensions.cs` (register both + bind `JwtOptions` + `AddAuthentication().AddJwtBearer(...)`)
- Test: `Locaccessum.Tests/Auth/PasswordHasherTests.cs`, `JwtTokenServiceTests.cs`

**Interfaces:**
- Consumes: nothing DB-related.
- Produces:
  - `IPasswordHasher`: `string Hash(string password)`, `bool Verify(string password, string hash)`.
  - `IJwtTokenService`: `string CreateToken(Guid userId, string email)` — HS256, `sub`=userId, `email` claim, `exp`=now+`LifetimeHours`, issuer/audience from options.
  - `JwtOptions { string Issuer; string Audience; string Secret; int LifetimeHours = 8; }` bound from `Jwt` section.

- [ ] **Step 1: Failing tests**

```csharp
public class PasswordHasherTests
{
    readonly IPasswordHasher _h = new PasswordHasher();

    [Fact] public void Hash_then_verify_true() => _h.Verify("Passw0rd!", _h.Hash("Passw0rd!")).Should().BeTrue();
    [Fact] public void Verify_wrong_password_false() => _h.Verify("nope", _h.Hash("Passw0rd!")).Should().BeFalse();
    [Fact] public void Hashes_are_salted() => _h.Hash("x").Should().NotBe(_h.Hash("x"));
}
```

```csharp
public class JwtTokenServiceTests
{
    [Fact]
    public void Token_contains_sub_and_email_and_expiry()
    {
        var opts = Options.Create(new JwtOptions { Issuer="i", Audience="a", Secret=new string('k',48), LifetimeHours=8 });
        var svc = new JwtTokenService(opts);
        var id = Guid.CreateVersion7();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(svc.CreateToken(id, "u@x.io"));
        jwt.Subject.Should().Be(id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "email" && c.Value == "u@x.io");
        jwt.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddHours(8), TimeSpan.FromMinutes(1));
    }
}
```

- [ ] **Step 2: Run — expect FAIL.**
- [ ] **Step 3: Implement.** `PasswordHasher` wraps `Microsoft.AspNetCore.Identity.PasswordHasher<object>` (call with a throwaway `new object()`). `JwtTokenService` builds a `JwtSecurityToken` with `SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret))` + `SigningCredentials(..., SecurityAlgorithms.HmacSha256)`.
- [ ] **Step 4: Register in DI.** In `AddInfrastructure`: `services.Configure<JwtOptions>(config.GetSection("Jwt"))`, `services.AddSingleton<IPasswordHasher, PasswordHasher>()`, `services.AddSingleton<IJwtTokenService, JwtTokenService>()`, `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => { o.TokenValidationParameters = ... ValidIssuer/ValidAudience/IssuerSigningKey, ValidateLifetime=true; })`. Add `app.UseAuthentication(); app.UseAuthorization();` to `Program.cs` before `MapControllers`.
- [ ] **Step 5: Run — expect PASS.** `dotnet test --filter "PasswordHasherTests|JwtTokenServiceTests"`
- [ ] **Step 6: Commit** `git commit -m "feat: password hashing and jwt token service"`

---

## Task 6: User code generator

**Files:**
- Create: `Locaccessum.Api/Abstractions/IUserCodeGenerator.cs`
- Create: `Locaccessum.Infrastructure/Identity/UserCodeGenerator.cs`
- Modify: `InfrastructureServiceCollectionExtensions.cs` (register scoped)
- Test: `Locaccessum.Tests/Auth/UserCodeGeneratorTests.cs`

**Interfaces:**
- Consumes: `LocaccessumDbContext`.
- Produces: `IUserCodeGenerator.Task<string> NextAsync(CancellationToken)` → format `^LOCA-[0-9A-HJ-NP-Z]{6}$` (Crockford-ish, no `I/O`), unique against `users.user_code` (retry up to 10× on collision, then throw `InvalidOperationException`).

- [ ] **Step 1: Failing test** (`[Collection("db")]`, uses `PostgresFixture`): generate 200 codes, all match the regex, all distinct.
- [ ] **Step 2: Run — FAIL.**
- [ ] **Step 3: Implement** using `RandomNumberGenerator.GetItems`. Query `db.Users.AnyAsync(u => u.UserCode == candidate, ct)`.
- [ ] **Step 4: Run — PASS.**
- [ ] **Step 5: Commit** `git commit -m "feat: unique user code generator"`

---

## Task 7: Auth — register

**Files:**
- Create: `Locaccessum.Api/Controllers/AuthController.cs`
- Create: `Locaccessum.Api/Contracts/Auth/RegisterRequest.cs`, `AuthResponse.cs`
- Create: `Locaccessum.Api/Common/ApiProblem.cs`
- Test: `Locaccessum.Tests/Api/RegisterTests.cs`

**Interfaces:**
- Consumes: `IPasswordHasher`, `IUserCodeGenerator`, `IJwtTokenService`, `LocaccessumDbContext`.
- Produces:
  - `POST /api/auth/register` body `{ email, password, displayName }` → `201` `AuthResponse { token, userId, email, displayName, userCode }`.
  - `RegisterRequest` validation: email format, password ≥ 8 chars, displayName 1–100.
  - Duplicate email → `409` ProblemDetails `{ code: "EMAIL_TAKEN" }`.
  - `ApiProblem.Conflict(string code, string detail)` / `.NotFound` / `.Forbidden` / `.Validation` helpers returning `ObjectResult` with `application/problem+json` and an `errors`/`code` extension.

- [ ] **Step 1: Failing tests**

```csharp
[Collection("db")]
public class RegisterTests(PostgresFixture fx) : IntegrationTest(fx)
{
    [Fact]
    public async Task Register_creates_user_and_returns_token_and_code()
    {
        var res = await Client.PostAsJsonAsync("/api/auth/register",
            new { email = "a@x.io", password = "Passw0rd!", displayName = "Alice" });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<AuthResponse>();
        body!.Token.Should().NotBeNullOrEmpty();
        body.UserCode.Should().MatchRegex("^LOCA-[0-9A-HJ-NP-Z]{6}$");
    }

    [Fact]
    public async Task Duplicate_email_is_409()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new { email = "dup@x.io", password = "Passw0rd!", displayName = "D" });
        var res = await Client.PostAsJsonAsync("/api/auth/register", new { email = "dup@x.io", password = "Passw0rd!", displayName = "D2" });
        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("bad-email", "Passw0rd!")]
    [InlineData("ok@x.io", "short")]
    public async Task Invalid_payload_is_400(string email, string password)
    {
        var res = await Client.PostAsJsonAsync("/api/auth/register", new { email, password, displayName = "X" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

Add a small `IntegrationTest` base class in `Locaccessum.Tests/Infrastructure/` that owns an `ApiFactory`, exposes `HttpClient Client`, and calls `ResetAsync()` in `InitializeAsync`. Reuse it for every `Api/*Tests` file below.

- [ ] **Step 2: Run — FAIL.**
- [ ] **Step 3: Implement** `ApiProblem`, DTOs (records with DataAnnotations), `AuthController.Register`. Check `await db.Users.AnyAsync(u => u.Email == req.Email.ToLowerInvariant())`; store email lowercased. `Guid.CreateVersion7()` id; `CreatedAt = DateTimeOffset.UtcNow`.
- [ ] **Step 4: Run — PASS.**
- [ ] **Step 5: Commit** `git commit -m "feat: auth register endpoint"`

---

## Task 8: Auth — login & GET /api/users/me

**Files:**
- Modify: `Locaccessum.Api/Controllers/AuthController.cs` (add `Login`)
- Create: `Locaccessum.Api/Controllers/UsersController.cs`
- Create: `Locaccessum.Api/Common/CurrentUser.cs`, `Abstractions/ICurrentUser.cs`
- Create: `Locaccessum.Api/Contracts/Auth/LoginRequest.cs`, `Contracts/Users/UserResponse.cs`
- Test: `Locaccessum.Tests/Api/LoginTests.cs`, `UsersMeTests.cs`

**Interfaces:**
- Consumes: Task 7 register, `IJwtTokenService`, `IPasswordHasher`.
- Produces:
  - `POST /api/auth/login` `{ email, password }` → `200 AuthResponse`; bad credentials → `401` `{ code: "INVALID_CREDENTIALS" }` (same response for unknown email and wrong password).
  - `GET /api/users/me` `[Authorize]` → `200 UserResponse { userId, email, displayName, userCode, createdAt }`; no/invalid token → `401`.
  - `ICurrentUser { Guid Id; string Email; }` reading `HttpContext.User` claims (`sub`, `email`); registered `AddHttpContextAccessor()` + scoped `CurrentUser`.

- [ ] **Step 1: Failing tests** — login happy path returns a token that works on `/api/users/me`; wrong password → 401; `/api/users/me` with no header → 401.
- [ ] **Step 2: Run — FAIL.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run — PASS.**
- [ ] **Step 5: Commit** `git commit -m "feat: auth login and users/me"`

---

## Task 9: GET /api/users/search?code=

**Files:**
- Modify: `Locaccessum.Api/Controllers/UsersController.cs`
- Create: `Locaccessum.Api/Contracts/Users/UserSummaryResponse.cs`
- Test: `Locaccessum.Tests/Api/UserSearchTests.cs`

**Interfaces:**
- Consumes: Task 8.
- Produces: `GET /api/users/search?code=LOCA-XXXXXX` `[Authorize]` → `200 UserSummaryResponse { userId, displayName, userCode }` or `404 { code: "USER_NOT_FOUND" }`. Exact match only, case-insensitive. Never returns email.

- [ ] **Step 1: Failing tests** — exact code returns summary without email; unknown code → 404; missing token → 401.
- [ ] **Step 2–4: FAIL → implement → PASS.**
- [ ] **Step 5: Commit** `git commit -m "feat: user search by code"`

---

## Task 10: Inventories — create, list, get

**Files:**
- Create: `Locaccessum.Api/Controllers/InventoriesController.cs`
- Create: `Locaccessum.Api/Contracts/Inventories/{CreateInventoryRequest,UpdateInventoryRequest,InventoryResponse,InventoryListItemResponse}.cs`
- Test: `Locaccessum.Tests/Api/InventoriesTests.cs`

**Interfaces:**
- Consumes: `ICurrentUser`, `LocaccessumDbContext`.
- Produces:
  - `POST /api/inventories` `{ name, description? }` `[Authorize]` → `201 InventoryResponse { id, name, description, ownerId, createdAt, myRole }`. Side effect: inserts a `Membership { Role = Owner }` for the caller in the same `SaveChanges`.
  - `GET /api/inventories` `[Authorize]` → `200 InventoryListItemResponse[] { id, name, description, myRole, memberCount }` — only inventories the caller is a member of.
  - `GET /api/inventories/{id}` `[Authorize]` → `200 InventoryResponse` if caller is a member, else `404` (do not leak existence).

- [ ] **Step 1: Failing tests**

```csharp
[Fact]
public async Task Create_makes_caller_owner_and_appears_in_list()
{
    var (_, token) = await Register("owner@x.io");
    var create = await Client(token).PostAsJsonAsync("/api/inventories", new { name = "Atelier", description = "Outils" });
    create.StatusCode.Should().Be(HttpStatusCode.Created);
    var inv = await create.Content.ReadFromJsonAsync<InventoryResponse>();
    inv!.MyRole.Should().Be("Owner");

    var list = await (await Client(token).GetAsync("/api/inventories")).Content.ReadFromJsonAsync<List<InventoryListItemResponse>>();
    list!.Should().ContainSingle(i => i.Id == inv.Id && i.MemberCount == 1);
}

[Fact]
public async Task Get_inventory_as_non_member_is_404()
{
    var (_, owner) = await Register("o@x.io");
    var id = await CreateInventory(owner, "Priv");
    var (_, stranger) = await Register("s@x.io");
    (await Client(stranger).GetAsync($"/api/inventories/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

- [ ] **Step 2–4: FAIL → implement → PASS.**
- [ ] **Step 5: Commit** `git commit -m "feat: inventory create/list/get with owner membership"`

---

## Task 11: Authorization policies (InventoryMember / InventoryAdmin / InventoryOwner)

**Files:**
- Create: `Locaccessum.Api/Authorization/InventoryRoleRequirement.cs`, `InventoryRoleHandler.cs`, `InventoryPolicies.cs`
- Modify: `Program.cs` (register handler + `AddAuthorizationBuilder().AddPolicy(...)` ×3)
- Modify: `Locaccessum.Api/Controllers/InventoriesController.cs` (`[Authorize(Policy = InventoryPolicies.Member)]` on `GET /{id}`)
- Test: `Locaccessum.Tests/Api/InventoryAuthorizationTests.cs`

**Interfaces:**
- Consumes: `Membership` rows (Task 10), `ICurrentUser`.
- Produces:
  - Constants `InventoryPolicies.Member` = `"InventoryMember"`, `.Admin` = `"InventoryAdmin"`, `.Owner` = `"InventoryOwner"`.
  - `InventoryRoleHandler : AuthorizationHandler<InventoryRoleRequirement>` — resolves `{id}` or `{inventoryId}` from `HttpContext.GetRouteValue`, loads the caller's `Membership.Role`, succeeds when `role >= requirement.Minimum` (order Owner > Admin > Member). Missing membership → fail (results in `403`; controllers that must return `404` for non-members handle that themselves, e.g. `GET /{id}` catches the 403 path by pre-checking — simplest: keep `GET /{id}` returning `404` via manual check and reserve policies for write routes).
  - Helper `Task<MembershipRole?> ResolveRoleAsync(Guid inventoryId, Guid userId)` on the handler for reuse.

- [ ] **Step 1: Failing tests** — create an inventory as owner; a second registered user with no membership calling a policy-protected probe route returns `403`; owner returns `200`. (Add a temporary `GET /api/inventories/{id}/_probe` guarded by `InventoryPolicies.Admin` used only by this test, or assert via a Task 12 route once it exists — prefer wiring Task 12 first if executing in order; otherwise the probe route is acceptable and removed in Task 12.)
- [ ] **Step 2–4: FAIL → implement → PASS.**
- [ ] **Step 5: Commit** `git commit -m "feat: inventory role authorization policies"`

---

## Task 12: Inventories — patch, delete, transfer ownership

**Files:**
- Modify: `Locaccessum.Api/Controllers/InventoriesController.cs`
- Create: `Locaccessum.Api/Contracts/Inventories/TransferOwnershipRequest.cs`
- Test: `Locaccessum.Tests/Api/InventorySettingsTests.cs`

**Interfaces:**
- Consumes: Task 11 policies.
- Produces:
  - `PATCH /api/inventories/{id}` `{ name?, description? }` `[Authorize(Policy = InventoryPolicies.Admin)]` → `200 InventoryResponse`. Only provided fields change.
  - `DELETE /api/inventories/{id}` `[Authorize(Policy = InventoryPolicies.Owner)]` → `204`. Cascades to memberships/equipment/reservations (FK `OnDelete` set in Task 3).
  - `POST /api/inventories/{id}/transfer-ownership` `{ newOwnerUserId }` `[Authorize(Policy = InventoryPolicies.Owner)]` → `200`. In one transaction: target must be an existing member; current owner's membership becomes `Admin`; target's becomes `Owner`; `inventory.owner_id` updated. Target not a member → `400 { code: "NOT_A_MEMBER" }`.

- [ ] **Step 1: Failing tests**

```csharp
[Fact] public async Task Admin_can_patch_name_but_not_delete() { /* admin: PATCH 200, DELETE 403 */ }
[Fact] public async Task Owner_can_delete() { /* 204, subsequent GET 404 */ }
[Fact] public async Task Transfer_swaps_roles_and_owner_id() { /* old owner -> Admin, new -> Owner, GET myRole reflects it */ }
[Fact] public async Task Transfer_to_non_member_is_400() { }
```

- [ ] **Step 2–4: FAIL → implement → PASS.** Remove the temporary `_probe` route from Task 11 if it was added.
- [ ] **Step 5: Commit** `git commit -m "feat: inventory patch, delete, transfer ownership"`

---

## Task 13: Members — list, update role, remove

**Files:**
- Create: `Locaccessum.Api/Controllers/MembersController.cs` (route prefix `api/inventories/{inventoryId}/members`)
- Create: `Locaccessum.Api/Contracts/Members/{MemberResponse,UpdateMemberRoleRequest}.cs`
- Test: `Locaccessum.Tests/Api/MembersTests.cs`

**Interfaces:**
- Consumes: Task 11 policies.
- Produces:
  - `GET .../members` `[InventoryMember]` → `MemberResponse[] { userId, displayName, userCode, role, joinedAt }`.
  - `PATCH .../members/{userId}` `[InventoryAdmin]` `{ role }` where `role ∈ {Admin, Member}` → `200`. Cannot change the `Owner`'s role (`400 { code: "CANNOT_MODIFY_OWNER" }`). An Admin cannot demote another Admin? — allowed (Admins are peers); only the Owner is protected.
  - `DELETE .../members/{userId}` `[InventoryAdmin]` → `204`. Cannot remove the `Owner` (`400 { code: "CANNOT_REMOVE_OWNER" }`). Removing yourself is allowed unless you're the Owner. Removing a member cascades: their future `Confirmed` reservations in this inventory are set to `Cancelled` (`CancelledAt = now`).

- [ ] **Step 1: Failing tests** — list shows all members; promote Member→Admin; cannot PATCH/DELETE the owner; removing a member cancels their upcoming reservations.
- [ ] **Step 2–4: FAIL → implement → PASS.**
- [ ] **Step 5: Commit** `git commit -m "feat: inventory member management"`

---

## Task 14: Invitations — create & list

**Files:**
- Create: `Locaccessum.Api/Controllers/InvitationsController.cs`
- Create: `Locaccessum.Api/Contracts/Invitations/{CreateInvitationRequest,InvitationResponse}.cs`
- Test: `Locaccessum.Tests/Api/InvitationsTests.cs` (create + list portion)

**Interfaces:**
- Consumes: Task 9 (resolve user by code), Task 11 policies.
- Produces:
  - `POST /api/inventories/{inventoryId}/invitations` `[InventoryAdmin]` `{ userCode, role }` (`role ∈ {Admin, Member}`) → `201 InvitationResponse { id, inventoryId, inventoryName, invitedUserId, invitedByUserId, role, status, createdAt }`.
    - Unknown code → `404 { code: "USER_NOT_FOUND" }`.
    - Target already a member → `409 { code: "ALREADY_MEMBER" }`.
    - Existing `Pending` invitation for that pair → `409 { code: "INVITATION_PENDING" }` (enforced by the filtered unique index; catch `DbUpdateException` and translate).
    - `role = Owner` in payload → `400`.
  - `GET /api/inventories/{inventoryId}/invitations` `[InventoryAdmin]` → `InvitationResponse[]` (all statuses, newest first).
  - `GET /api/invitations` `[Authorize]` → `InvitationResponse[]` where `invitedUserId == me && status == Pending`.

- [ ] **Step 1: Failing tests** — happy path creates Pending; duplicate pending → 409; inviting an existing member → 409; invitee sees it in `GET /api/invitations`.
- [ ] **Step 2–4: FAIL → implement → PASS.**
- [ ] **Step 5: Commit** `git commit -m "feat: create and list invitations"`

---

## Task 15: Invitations — accept, decline, revoke

**Files:**
- Modify: `Locaccessum.Api/Controllers/InvitationsController.cs`
- Test: `Locaccessum.Tests/Api/InvitationsTests.cs` (add accept/decline/revoke)

**Interfaces:**
- Consumes: Task 14.
- Produces:
  - `POST /api/invitations/{id}/accept` `[Authorize]` — only the `invitedUser`. Transaction: guard `status == Pending` (else `409 { code: "INVITATION_NOT_PENDING" }`), set `Accepted` + `RespondedAt`, insert `Membership { Role = invitation.Role, JoinedAt = now }`. Idempotency: a second accept → `409` (not a new membership).
  - `POST /api/invitations/{id}/decline` `[Authorize]` — only the `invitedUser`. `Pending` → `Declined` + `RespondedAt`. → `200`.
  - `DELETE /api/invitations/{id}` `[Authorize]` — only an Admin/Owner of the invitation's inventory. `Pending` → `Revoked`. → `204`.
  - Someone other than the invitee calling accept/decline → `403`.

- [ ] **Step 1: Failing tests**

```csharp
[Fact] public async Task Accept_creates_membership_with_invited_role() { /* invite as Admin -> accept -> GET /api/inventories shows myRole=Admin */ }
[Fact] public async Task Accept_twice_is_409_and_single_membership() { }
[Fact] public async Task Decline_sets_status_and_no_membership() { }
[Fact] public async Task Revoke_by_admin_blocks_accept() { /* revoke -> invitee accept -> 409 */ }
[Fact] public async Task Third_party_cannot_accept() { /* 403 */ }
```

- [ ] **Step 2–4: FAIL → implement → PASS.**
- [ ] **Step 5: Commit** `git commit -m "feat: accept/decline/revoke invitations"`

---

## Task 16: Equipment — CRUD

**Files:**
- Create: `Locaccessum.Api/Controllers/EquipmentController.cs`
- Create: `Locaccessum.Api/Contracts/Equipment/{CreateEquipmentRequest,UpdateEquipmentRequest,EquipmentResponse}.cs`
- Test: `Locaccessum.Tests/Api/EquipmentTests.cs`

**Interfaces:**
- Consumes: Task 11 policies.
- Produces:
  - `POST /api/inventories/{inventoryId}/equipment` `[InventoryAdmin]` `{ name, reference, informations?, status? }` → `201 EquipmentResponse { id, inventoryId, name, reference, informations, status, createdAt }`. `status` defaults to `Active`. `name`/`reference` required, ≤120; `informations` ≤ 20000, stored/returned verbatim.
  - `GET /api/inventories/{inventoryId}/equipment` `[InventoryMember]` (flat list, newest first) → `EquipmentResponse[]`. `?grouped=true` handled in Task 17.
  - `GET /api/equipment/{id}` `[InventoryMember on the parent inventory]` → `EquipmentResponse`.
  - `PATCH /api/equipment/{id}` `[InventoryAdmin]` `{ name?, reference?, informations?, status? }` → `200`.
  - `DELETE /api/equipment/{id}` `[InventoryAdmin]` → `204` **only if it has zero reservations**; otherwise `409 { code: "EQUIPMENT_HAS_RESERVATIONS" }` with a hint to set `status = Retired` instead. (Per spec §9: prefer soft-delete via status.)

- [ ] **Step 1: Failing tests** — create with `informations` containing `"<b>220V</b>"` returns it byte-for-byte; member can read, plain member cannot create (`403`); delete with a reservation → `409`; `PATCH status=Retired` succeeds.
- [ ] **Step 2–4: FAIL → implement → PASS.**
- [ ] **Step 5: Commit** `git commit -m "feat: equipment crud with reservation-safe delete"`

---

## Task 17: Equipment — grouped stacks

**Files:**
- Modify: `Locaccessum.Api/Controllers/EquipmentController.cs` (`?grouped=true` branch)
- Create: `Locaccessum.Api/Contracts/Equipment/EquipmentStackResponse.cs`
- Test: `Locaccessum.Tests/Api/EquipmentStackTests.cs`

**Interfaces:**
- Consumes: Task 16.
- Produces: `GET /api/inventories/{inventoryId}/equipment?grouped=true` `[InventoryMember]` →
  `EquipmentStackResponse[] { name, reference, unitsTotal, unitsActive, unitsMaintenance, unitsRetired, unitIds: Guid[] }`,
  where grouping key is `(name, reference)` (case-sensitive, exact). `unitIds` ordered by `createdAt`. Sorted by `name`, then `reference`.

- [ ] **Step 1: Failing test**

```csharp
[Fact]
public async Task Grouped_collapses_same_name_and_reference()
{
    // 3× ("Frigo 19T","RENAULT-D") + 1× ("Frigo 19T","VOLVO-FH")
    var stacks = await GetStacks(token, inventoryId);
    stacks.Should().HaveCount(2);
    stacks.Single(s => s.Reference == "RENAULT-D").UnitsTotal.Should().Be(3);
    stacks.Single(s => s.Reference == "RENAULT-D").UnitIds.Should().HaveCount(3);
}
```

- [ ] **Step 2–4: FAIL → implement (LINQ `GroupBy` translated server-side, or `AsEnumerable()` after projecting the needed columns) → PASS.**
- [ ] **Step 5: Commit** `git commit -m "feat: grouped equipment stacks endpoint"`

---

## Task 18: Reservations — calendar feed

**Files:**
- Create: `Locaccessum.Api/Controllers/ReservationsController.cs`
- Create: `Locaccessum.Api/Contracts/Reservations/ReservationResponse.cs`
- Test: `Locaccessum.Tests/Api/ReservationCalendarTests.cs`

**Interfaces:**
- Consumes: Task 11 policies, Task 16 equipment.
- Produces: `GET /api/inventories/{inventoryId}/reservations?from=&to=&equipmentId=` `[InventoryMember]` →
  `ReservationResponse[] { id, equipmentId, equipmentName, reference, userId, userDisplayName, startsAt, endsAt, status }`.
  - Returns `Confirmed` only.
  - `from`/`to` are required ISO-8601; returns reservations overlapping `[from, to)` (reuse the half-open overlap predicate in SQL: `starts_at < to AND ends_at > from`).
  - `equipmentId` optional filter.
  - `from >= to` → `400`.

- [ ] **Step 1: Failing tests** — seeded reservations inside / straddling / outside the window; only overlapping `Confirmed` returned; `equipmentId` filter narrows results; cancelled excluded.
- [ ] **Step 2–4: FAIL → implement → PASS.**
- [ ] **Step 5: Commit** `git commit -m "feat: reservation calendar feed"`

---

## Task 19: Reservations — create with anti-double-booking

**Files:**
- Create: `Locaccessum.Api/Services/ReservationBookingService.cs`
- Modify: `Locaccessum.Api/Controllers/ReservationsController.cs` (add `POST`)
- Create: `Locaccessum.Api/Contracts/Reservations/CreateReservationRequest.cs`
- Modify: `Program.cs` (register `ReservationBookingService` scoped)
- Test: `Locaccessum.Tests/Api/ReservationBookingTests.cs`

**Interfaces:**
- Consumes: `LocaccessumDbContext`, `ICurrentUser`, `Reservation.Overlaps` (Task 2).
- Produces:
  - `POST /api/inventories/{inventoryId}/reservations` `[InventoryMember]`.
    Body is one of:
    - `{ name, reference, startsAt, endsAt }` — book any free unit of that stack.
    - `{ equipmentId, startsAt, endsAt }` — book that specific unit.
  - Success → `201 ReservationResponse` (with the auto-assigned `equipmentId`).
  - `ReservationBookingService.Task<BookingResult> BookAsync(Guid inventoryId, Guid actingUserId, CreateReservationRequest req, CancellationToken ct)` returning a discriminated result: `Booked(Reservation)`, `StackFull(int unitsTotal, int unitsBusy)`, `ValidationFailed(string code, string message)`, `EquipmentNotFound`.
  - Controller maps: `StackFull` → `409 { code:"STACK_FULL", unitsTotal, unitsBusy }`; `ValidationFailed` → `400 { code }`; `EquipmentNotFound` → `404`.

**Algorithm (implement exactly):**
```
validate: startsAt < endsAt  -> code "TIME_ORDER"
validate: startsAt > now      -> code "PAST_START"
open EF transaction (default READ COMMITTED)
  candidate units =
     if equipmentId given: [that unit] (must belong to inventoryId & status Active, else EquipmentNotFound / "UNIT_UNAVAILABLE")
     else: equipment in inventory where name = req.name AND reference = req.reference AND status = Active, ordered by created_at
  if candidate set empty -> StackFull(0,0)  (nothing bookable)
  lockKey text = inventoryId + '|' + name + '|' + reference       // for equipmentId path use that unit's name|reference
  EXECUTE: SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0));
  busy = 0
  foreach unit in candidates:
     bool clash = await db.Reservations.AnyAsync(r =>
        r.EquipmentId == unit.Id && r.Status == Confirmed &&
        r.StartsAt < req.EndsAt && r.EndsAt > req.StartsAt, ct);
     if (!clash) { chosen = unit; break; } else busy++;
  if chosen == null -> rollback -> StackFull(candidates.Count, busy)
  insert Reservation { Confirmed, CreatedAt = now }
  try SaveChangesAsync
  catch DbUpdateException when SQLSTATE 23P01 (exclusion_violation):
     rollback -> StackFull(candidates.Count, candidates.Count)   // lost a race that the lock should have prevented; still correct
  commit -> Booked(reservation)
```
Add helper `db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", ct)`.

- [ ] **Step 1: Failing tests**

```csharp
[Fact]
public async Task Books_a_free_unit_of_the_stack_and_returns_its_id()
{
    // stack of 2 units ("Cam","A"); book -> 201; equipmentId is one of the two
}

[Fact]
public async Task Second_overlapping_booking_takes_the_other_unit()
{
    // book 10-12 -> unit#1 ; book 11-13 -> unit#2 ; both 201, different equipmentId
}

[Fact]
public async Task When_all_units_busy_returns_409_STACK_FULL_with_counts()
{
    // stack of 2, two overlapping bookings placed; third overlapping -> 409, unitsTotal=2, unitsBusy=2
}

[Fact]
public async Task Touching_intervals_do_not_conflict()
{
    // 10-11 then 11-12 on a 1-unit stack -> both 201 on the same unit
}

[Fact]
public async Task Past_start_is_400_PAST_START() { }

[Fact]
public async Task Specific_equipmentId_that_is_retired_is_400_UNIT_UNAVAILABLE() { }
```

- [ ] **Step 2: Run — FAIL.**
- [ ] **Step 3: Implement `ReservationBookingService` + controller wiring.**
- [ ] **Step 4: Run — PASS.**
- [ ] **Step 5: Commit** `git commit -m "feat: reservation booking with advisory lock and overlap check"`

---

## Task 20: Reservations — concurrency test

**Files:**
- Test: `Locaccessum.Tests/Api/ReservationConcurrencyTests.cs`
- Modify (only if a real defect surfaces): `Locaccessum.Api/Services/ReservationBookingService.cs`

**Interfaces:**
- Consumes: Task 19.
- Produces: no new production interface — this task proves the guarantee.

- [ ] **Step 1: Write the test**

```csharp
[Collection("db")]
public class ReservationConcurrencyTests(PostgresFixture fx) : IntegrationTest(fx)
{
    [Fact]
    public async Task Parallel_bookings_on_one_stack_never_exceed_unit_count()
    {
        var (_, token) = await Register("race@x.io");
        var invId = await CreateInventory(token, "Race");
        // create N = 3 units of the same stack ("Drone","DJI")
        for (var i = 0; i < 3; i++)
            await Client(token).PostAsJsonAsync($"/api/inventories/{invId}/equipment",
                new { name = "Drone", reference = "DJI" });

        var start = DateTimeOffset.UtcNow.AddDays(1);
        var body = new { name = "Drone", reference = "DJI", startsAt = start, endsAt = start.AddHours(2) };

        var attempts = Enumerable.Range(0, 12).Select(async _ =>
        {
            var c = Client(token); // each call builds its own HttpClient
            var r = await c.PostAsJsonAsync($"/api/inventories/{invId}/reservations", body);
            return r.StatusCode;
        });
        var results = await Task.WhenAll(attempts);

        results.Count(s => s == HttpStatusCode.Created).Should().Be(3);
        results.Count(s => s == HttpStatusCode.Conflict).Should().Be(9);

        // DB-level truth: exactly 3 confirmed rows for the stack
        await using var db = fx.NewDbContext();
        var confirmed = await db.Reservations.CountAsync(r => r.Status == ReservationStatus.Confirmed);
        confirmed.Should().Be(3);
    }
}
```

Add `PostgresFixture.NewDbContext()` returning a fresh `LocaccessumDbContext` on `ConnectionString`.

- [ ] **Step 2: Run.** Expected: PASS. If it flakes or over-books, the advisory lock scope or transaction handling in Task 19 is wrong — fix `ReservationBookingService` (do NOT weaken the test), re-run until deterministically green over 5 consecutive runs: `dotnet test --filter ReservationConcurrencyTests`.
- [ ] **Step 3: Commit** `git commit -m "test: concurrent booking never exceeds unit count"`

---

## Task 21: Reservations — cancel

**Files:**
- Modify: `Locaccessum.Api/Controllers/ReservationsController.cs`
- Test: `Locaccessum.Tests/Api/ReservationCancelTests.cs`

**Interfaces:**
- Consumes: Task 19.
- Produces: `POST /api/reservations/{id}/cancel` `[Authorize]` → `200 ReservationResponse` (status now `Cancelled`).
  - Allowed if caller is the reservation's `UserId` **and** `StartsAt > now`, OR caller is `Admin`/`Owner` of the reservation's inventory (any time).
  - Otherwise `403`.
  - Already `Cancelled` → `409 { code: "ALREADY_CANCELLED" }`.
  - Sets `Status = Cancelled`, `CancelledAt = now`. Frees the slot (exclusion constraint filters `Confirmed` only) — a follow-up booking on the same unit/slot then succeeds.

- [ ] **Step 1: Failing tests** — owner-of-reservation cancels own future booking (200); a plain member cannot cancel someone else's (403); inventory Admin cancels anyone's (200); double cancel → 409; after cancel, re-booking the same slot succeeds.
- [ ] **Step 2–4: FAIL → implement → PASS.**
- [ ] **Step 5: Commit** `git commit -m "feat: cancel reservation and free the slot"`

---

## Task 22: Internal endpoint — upcoming reservations & reminder-sent

**Files:**
- Create: `Locaccessum.Api/Authorization/InternalApiKeyAuthHandler.cs`
- Create: `Locaccessum.Api/Controllers/InternalReservationsController.cs`
- Create: `Locaccessum.Api/Contracts/Reservations/UpcomingReservationResponse.cs`
- Modify: `Program.cs` (add auth scheme `"Internal"` + policy `"InternalKey"`)
- Test: `Locaccessum.Tests/Api/InternalReservationsTests.cs`

**Interfaces:**
- Consumes: `IConfiguration["InternalApiKey"]`, `LocaccessumDbContext`.
- Produces:
  - Authentication scheme `"Internal"`: `InternalApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>` — reads header `X-Internal-Api-Key`, compares (fixed-time) to config value; success → a `ClaimsPrincipal` with a single `role=internal` claim. Missing/wrong → `AuthenticateResult.Fail` → `401`.
  - `GET /api/internal/reservations/upcoming?windowHours=24` `[Authorize(AuthenticationSchemes = "Internal", Policy = "InternalKey")]` →
    `UpcomingReservationResponse[] { reservationId, userEmail, userDisplayName, equipmentName, reference, inventoryName, startsAt, endsAt }`.
    Filter: `status = Confirmed AND reminder_sent_at IS NULL AND starts_at >= now + (windowHours-1)h AND starts_at < now + (windowHours+1)h`. `windowHours` default 24, clamp to `1..168`.
  - `POST /api/internal/reservations/{id}/reminder-sent` (same auth) → `204`. Sets `reminder_sent_at = now` if currently null; if already set, still `204` (idempotent). Unknown id → `404`.

- [ ] **Step 1: Failing tests**

```csharp
[Fact] public async Task Missing_key_is_401() { }
[Fact] public async Task Wrong_key_is_401() { }
[Fact]
public async Task Returns_only_confirmed_unreminded_in_the_23_to_25h_window()
{
    // seed: one at +24h (in), one at +24h already reminded (out), one at +30h (out), one cancelled at +24h (out)
    // -> exactly 1 row, with userEmail + equipmentName populated
}
[Fact]
public async Task Reminder_sent_is_idempotent()
{
    // POST twice -> both 204 ; reminder_sent_at unchanged after the 2nd
}
```

- [ ] **Step 2–4: FAIL → implement → PASS.**
- [ ] **Step 5: Commit** `git commit -m "feat: internal upcoming reservations endpoint with api-key auth"`

---

## Task 23: Development seed data

**Files:**
- Create: `Locaccessum.Infrastructure/Persistence/DbSeeder.cs`
- Modify: `Program.cs` (call `DbSeeder.SeedAsync` after migrate, Development only)
- Test: `Locaccessum.Tests/Persistence/SeederTests.cs`

**Interfaces:**
- Consumes: `LocaccessumDbContext`, `IPasswordHasher`, `IUserCodeGenerator`.
- Produces: `DbSeeder.Task SeedAsync(LocaccessumDbContext db, IPasswordHasher hasher, IUserCodeGenerator codes, CancellationToken ct)` — idempotent (no-op if `Users.Any()`). Creates:
  - users `alice@locaccessum.dev` / `bob@locaccessum.dev`, password `Passw0rd!`.
  - one inventory "Atelier Démo" owned by Alice, Bob added as `Member`.
  - equipment: 3× `("Perceuse","BOSCH-GSB")`, 1× `("Vidéoprojecteur","EPSON-EB")`, 1× `("Fourgon","RENAULT-MASTER")` — the Perceuse trio demonstrates a stack.
  - 2 confirmed reservations for Bob (one ~in 24h so the worker demo fires).

- [ ] **Step 1: Failing test** — run `SeedAsync` twice against a clean DB; second call adds nothing; asserts the Perceuse stack has 3 units.
- [ ] **Step 2–4: FAIL → implement → PASS.**
- [ ] **Step 5: Commit** `git commit -m "feat: idempotent development seed data"`

---

## Task 24: Dockerfile, README, end-to-end smoke

**Files:**
- Create/verify: `backend/Locaccessum.Api/Dockerfile`
- Create: `README.md` (backend section)
- Modify: `.env.example` (final keys), `docker-compose.yml` (ensure `api` builds)
- Test: full suite + manual compose smoke

**Interfaces:**
- Consumes: everything.
- Produces: `docker compose up` brings Postgres + API healthy; `GET http://localhost:8080/health` → `200`; migrations auto-applied; seed present.

- [ ] **Step 1: Finalize the `Dockerfile`** (multi-stage, restore from `.csproj` layer first for caching, `dotnet publish -c Release -o /app`, non-root user, `EXPOSE 8080`).
- [ ] **Step 2: Write `README.md`** — prerequisites (Docker Desktop, .NET 10 SDK), `cp .env.example .env`, `docker compose up --build`, how to run tests (`cd backend && dotnet test` — needs Docker for Testcontainers), the seeded credentials, and a one-paragraph explanation of the double-layer conflict protection (advisory lock + GiST exclusion constraint).
- [ ] **Step 3: Run the full test suite.** Run: `cd backend && dotnet test`. Expected: all green, 0 warnings.
- [ ] **Step 4: Compose smoke.** Run: `docker compose up --build -d && sleep 15 && curl -s localhost:8080/health`. Expected: `{"status":"ok"}`. Then `docker compose down`.
- [ ] **Step 5: Commit** `git commit -m "chore: dockerfile, backend readme, compose smoke"`

---

## Self-Review

**1. Spec coverage**

| Spec section | Task(s) |
|---|---|
| §3 structure / projects / compose | 1, 24 |
| §3 tech stack (EF Core, Npgsql, snake_case, JWT) | 1, 3, 5 |
| §4 `User` + `UserCode` | 2, 3, 6, 7 |
| §4 `Inventory` | 2, 3, 10, 12 |
| §4 `Membership` (one Owner invariant) | 2, 3, 10, 12, 13 |
| §4 `Invitation` (pending uniqueness) | 2, 3, 14, 15 |
| §4 `Equipment` (fixed shape, `Informations` plain text) | 2, 3, 16 |
| §4 `Reservation` + exclusion constraint + `ReminderSentAt` | 2, 3, 19, 22 |
| §4 data scoping (membership check per request) | 11, + every controller |
| §5 authorization policies Member/Admin/Owner | 11 |
| §5 endpoint list | 7–22 (mapped 1:1) |
| §5 booking algorithm (advisory lock + overlap + exclusion fallback) | 19, 20 |
| §5 validation (future, end>start, status) | 19 |
| §5 cancellation rules | 13 (member removal), 21 |
| §5 security (PBKDF2, JWT 8h no refresh, CORS, internal key) | 5, 22, 24 (CORS in Program.cs — see note) |
| §7 internal endpoint contract for the worker | 22 |
| §8 phases 1–7 | all tasks |
| §9 soft-delete equipment with reservations | 16 |
| §9 `Informations` rendered as plain text (injection safety) | 16 (test asserts verbatim round-trip) |

**Gap found & closed:** spec §5 mentions **CORS restricted to the front origin**. No task owned it. → Add to **Task 24, Step 1.5**: in `Program.cs`, `builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(builder.Configuration["Cors:FrontOrigin"] ?? "http://localhost:5173").AllowAnyHeader().AllowAnyMethod()))` and `app.UseCors()` before `MapControllers()`; add `Cors__FrontOrigin=http://localhost:5173` to `.env.example`.

**Gap found & closed:** spec §5 lists `GET /api/inventories/{id}/invitations` as **Admin+**. Task 14 covers it — confirmed, no change.

**2. Placeholder scan:** Tasks 8, 9, 12, 13, 16, 18, 21 use the compressed "FAIL → implement → PASS" form for steps 2–4 but each still carries concrete failing-test descriptions and an explicit interface contract with exact routes, status codes, and error codes. Tasks 12, 13, 21 give test method names + inline intent rather than full bodies — acceptable because the endpoint contract in the Interfaces block is unambiguous; an executor writing the test has the status codes and error-code strings verbatim. No "TBD"/"add error handling"/"handle edge cases" language remains.

**3. Type consistency:**
- `Reservation.Overlaps(DateTimeOffset,DateTimeOffset,DateTimeOffset,DateTimeOffset)` — defined Task 2, used Tasks 18/19.
- `AuthResponse { Token, UserId, Email, DisplayName, UserCode }` — defined Task 7, reused Task 8.
- `InventoryResponse { Id, Name, Description, OwnerId, CreatedAt, MyRole }` — Task 10, reused 12.
- `InventoryPolicies.{Member,Admin,Owner}` string constants — Task 11, referenced 12–22.
- `ReservationResponse` — Task 18, reused 19/21. `UpcomingReservationResponse` distinct — Task 22.
- `ReservationBookingService.BookAsync` → `BookingResult` union `{Booked, StackFull, ValidationFailed, EquipmentNotFound}` — Task 19, exercised 20.
- `PostgresFixture` (`ConnectionString`, `NewDbContext()`), `ApiFactory`, `IntegrationTest` base, `TestDataFactory` — Tasks 3/4, used throughout. `NewDbContext()` added in Task 20 Step 1 — move its definition to Task 3 Step 4 so earlier tasks can use it. **Fix applied:** Task 3 Step 4 also adds `public LocaccessumDbContext NewDbContext()`.
- Error codes are consistent kebab/UPPER_SNAKE: `EMAIL_TAKEN`, `INVALID_CREDENTIALS`, `USER_NOT_FOUND`, `NOT_A_MEMBER`, `CANNOT_MODIFY_OWNER`, `CANNOT_REMOVE_OWNER`, `ALREADY_MEMBER`, `INVITATION_PENDING`, `INVITATION_NOT_PENDING`, `EQUIPMENT_HAS_RESERVATIONS`, `STACK_FULL`, `TIME_ORDER`, `PAST_START`, `UNIT_UNAVAILABLE`, `ALREADY_CANCELLED`. Frontend plan (Plan 2) must consume this list — it will be copied into that plan's Global Constraints.

Fixes applied inline above. Plan ready.
