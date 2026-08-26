# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Solution overview

**DIGER — Trámites Estado** is an internal portal for DIGER (Honduras) covering institutional
processes (expedientes), projects (proyectos), meetings (reuniones), contacts, support tickets and
the SIGER catalogue of national procedures. Target: .NET 9, SQL Server, xUnit.

`Diger.TramitesEstado.sln` holds two runnable hosts, four shared libraries and three test projects:

| Project | Role |
|---|---|
| `src/Web` | Razor Pages app — the portal, and the host that owns the design-time DbContext |
| `src/Presentation` | **Public read-only API v1** — not an "alternative UI". `X-Api-Key` auth, rate limiting, Swagger; serves the SIGER catalogue to the external PortalDigital site. Contract in `docs/api-v1/openapi-v1.yaml` |
| `src/Application` | CQRS handlers (MediatR), FluentValidation, pipeline behaviors |
| `src/Domain` | Entities, enums, domain events, `DomainException` — zero dependencies |
| `src/Infrastructure` | EF Core (SQL Server), repositories, security, PDF/Excel, SMTP, AI agent, importers |
| `tests/Domain.Tests` | Unit tests, plain instantiation (24 tests) |
| `tests/Application.Tests` | Handler tests on EF In-Memory (203 tests) |
| `tests/Web.Tests` | End-to-end permission gating via `WebApplicationFactory` + SQLite (15 tests) |

## Common commands

All paths are relative to the repo root (which contains the `.sln`, `src/`, `tests/`).

```powershell
# Build the entire solution
dotnet build Diger.TramitesEstado.sln

# Run the portal (primary UI)
dotnet run --project src\Web

# Run the public API host
dotnet run --project src\Presentation

# Run all tests (242 today, all green)
dotnet test Diger.TramitesEstado.sln

# Run one test project / a single test by name
dotnet test tests\Application.Tests
dotnet test tests\Application.Tests --filter "FullyQualifiedName~PersonasCapacitadas"
dotnet test tests\Web.Tests --filter "FullyQualifiedName~GateoDePermisos"

# Add a new EF migration (startup-project is Web, which owns the design-time DbContext)
dotnet ef migrations add <NombreMigracion> --project src\Infrastructure --startup-project src\Web --output-dir Persistence\Migrations
```

**Two migrations folders, one history**: `src/Infrastructure/Migrations/` (holds
`AppDbContextModelSnapshot.cs` plus migrations up to 2026-07-31) and
`src/Infrastructure/Persistence/Migrations/` (everything since 2026-08-05) both feed the same
`__EFMigrationsHistory` table. Always pass `--output-dir Persistence\Migrations` explicitly —
without it `dotnet ef` writes to the first folder, which is now the stale location.

**Mandatory DB change log** (`.agents/AGENTS.md`): every schema change — EF migration, `ALTER`,
index, seed script — must be appended to `Contextos/script_cambios_bd.md` with the date, a short
description, the EF migration name, and raw SQL another developer can run by hand.

## Local development

- **Database**: SQL Server LocalDB; the dev connection string (`src/Web/appsettings.Development.json`)
  targets `(localdb)\MSSQLLocalDB`. On startup, in Development, the app auto-migrates and calls
  `DbSeeder.SeedUsuariosAsync` **only if** `Datos:AplicarMigracionesAlArrancar` is `true` — off by
  default on purpose: a `Development` connection string that happens to point at a shared database
  must not silently migrate it because someone pressed F5. Enable it per machine, never in a
  committed appsettings (`dotnet user-secrets set "Datos:AplicarMigracionesAlArrancar" true --project src\Web`,
  or `Datos__AplicarMigracionesAlArrancar=true`). Tests never touch this.
- **Seeded logins** (all password-hashed): `admin@diger.gob.hn` / `Admin#2026` (Administrador), plus
  `jefe.inst@`, `jefe.area@`, `jefe.uni@`, `empleado@`, `consultor@` with `JefeInst#2026`,
  `JefeArea#2026`, `JefeUni#2026`, `Empleado#2026`, `Consultor#2026`.
- **Ports** come from the `Ports` config section (`DevMain` 49175 / `DevCert` 49176 / `DevHttp` 49177),
  not from constants — `launchSettings.json` mirrors them and `.claude/launch.json` ("web") runs
  http 5011. Keep 49176 free when testing certificate login.
- **Secrets**: Supabase import credentials live in User Secrets (UserSecretsId
  `diger-tramites-estado-web`) under `Supabase:Url` / `Supabase:AnonKey`. The Anthropic key for the
  chat agent goes in `Ai:ApiKey`, the API-v1 key in `PortalDigitalApi:ApiKey` — none of them in a
  committed appsettings.
- **LocalDB gotcha**: the instance sleeps on inactivity and occasionally leaves a stuck `sqlservr`
  process, so startup can fail on `MigrateAsync()` with "SQL Server process failed to start". Fix:
  `sqllocaldb start MSSQLLocalDB`. The DbContext registration uses `EnableRetryOnFailure`, so avoid
  explicit EF transactions — they are incompatible with the retry strategy.
- **Build lock**: if `dotnet build` fails only with `MSB3027`/`MSB3021` copy errors, a running app or
  a Visual Studio debug session is holding the DLLs. `.cshtml` edits need a rebuild/restart; they are
  not hot-reloaded.
- Unhandled exceptions are appended to `crash_log.txt` next to the built binary.

## Architecture

### Clean Architecture layers

```
Web / Presentation  →  Application  →  Domain
                    →  Infrastructure (registered via DI)
```

- **Domain** has zero dependencies. `BaseAuditableEntity` is the auditable root; `BaseEntity` holds
  the domain events list. Aggregate roots (`Expediente`, `Proyecto`, `Reunion`, `Ticket`) expose
  factory methods (`Crear(...)`) and mutation methods instead of public setters.
- **Application** knows only the Domain and the interfaces it declares in
  `Common/Interfaces/IRepositories.cs`. Commands and Queries each get a folder under the feature
  name (`Expedientes/Commands/CrearExpediente/`); the command record, handler and validator are
  co-located in one file. Validators are auto-registered and run in `ValidationBehavior`.
- **Infrastructure** implements every Application interface: `AppDbContext`, repositories,
  `PasswordHasher`, `CurrentUserService`, PDF/Excel reports, SMTP, the AI agent, Supabase importers.
- **Web** is the only project that knows all four layers. Razor Pages call MediatR `ISender`; they
  never touch repositories directly.

### MediatR pipeline

`LoggingBehavior` → `ValidationBehavior` (fail-fast) → `CachingBehavior` → handler. Handlers receive
their repository and `IUnitOfWork` (both implemented by `AppDbContext`) by constructor injection.

`CachingBehavior` only engages for requests implementing `ICacheableQuery` (generic constraint, so
commands can never be cached) and stores in `IMemoryCache`, default TTL 30 min. **A cacheable query
whose results are scope-sensitive must fold the user context into `CacheKey`** — otherwise one
institution is served another's rows straight out of the cache, bypassing the query filters entirely.

### Institutional scope (data isolation)

`AppDbContext` applies global EF query filters so users only see records inside their scope. Each
filter is `!IsDeleted && (<scope>)` — soft-delete is AND-ed into the same filter, so ordinary queries
never see soft-deleted rows.

Scope comes from **`NivelAlcance`**, a property of the user's role row (`Global` / `Institucion` /
`Area` / `Unidad`), never from the role's *name*. Every non-global branch is anchored on
`InstitucionId == _activeInst`; without that anchor the area/unit branches leaked records across
institutions. A role that cannot be resolved falls back to `Unidad`, the most restrictive.

Scope is captured once per request into readonly fields when the context is constructed. Queries that
must bypass it (unique-code generation, importers checking existing `OrigenExternoId`s) call
`.IgnoreQueryFilters()` explicitly. `Usuario` and `AsignacionUsuario` are **not** filtered — user
administration is global today.

### `SaveChangesAsync` is load-bearing

The override in `AppDbContext` does six things in order, and features rely on all of them:

1. **Hard read-only block** — any mutation under a role with `EsSoloLectura` throws `UnauthorizedAccessException`.
2. **Cross-institution guard** — writes to `Area`/`Unidad` outside the active institution throw.
3. **Hierarchy auto-stamping** — inserts get `InstitucionId`/`AreaId`/`UnidadId` filled from the
   active context when left empty, so handlers don't set them by hand.
4. **Soft-delete conversion** — `Remove()` on an `ISoftDeletable` becomes `IsDeleted = true`; physical
   deletes never reach the database.
5. **Audit stamping** — `CreatedAt/By`, `UpdatedAt/By` on `BaseAuditableEntity`.
6. **Domain-event dispatch** — *after* the save, so Ids exist and only events from a write that
   actually happened are published.

`AppDbContext.cs` is ~1,750 lines: it also holds **every** `IEntityTypeConfiguration` inline after the
context class. New entity ⇒ add its `DbSet`, its configuration class in that same file, and its query
filter if it is scoped.

The `Modern_Spanish_CI_AI` collation (accent-insensitive search on `TramiteSiger` and `Institucion`)
is applied **only** under `if (Database.IsSqlServer())` — SQLite, used by `Web.Tests`, does not know
that collation and fails with "no such collation sequence". Keep provider-specific model config
behind that guard.

### Startup wiring (order matters)

`IRolCatalogo` is a **Singleton** because `AppDbContext` reads it *synchronously* while building the
RLS filters. Hosted services run in registration order and that order is deliberate:

`RolCatalogoLoader` (fills the role catalogue before the first request) →
`PermissionCatalogSyncService` (reflects over PageModels, syncs the `Permisos` table) →
`PermisosSeedService` (translates the legacy per-module matrix into per-action grants, first run only).

`Program.cs` also copies configuration into static holders at boot — `UploadsConfig`,
`SemaforoAvance` thresholds, `Paginacion` defaults, ports. Tune those through appsettings
(`Uploads`, `Expedientes:Semaforo`, `Paginacion`, `Ports`), not by editing code.

### Authentication

Cookie-based (`CookieAuthenticationDefaults`). A user holds one or more assignments
(`AsignacionUsuario` = institution + optional area/unit + role); login makes the first one the active
context and stores the rest in the `AsignacionesJson` claim so the user can switch context in the UI.

**A user with no assignment has no role.** Login emits no role claim, `CurrentUserService` fails
closed (minimum scope, no capabilities), every module is denied and the user is sent to their profile
with a notice. Do not reintroduce a default role here — an earlier `?? "Empleado"` silently granted
32 permission keys to unconfigured accounts.

Razor Pages require authentication except the folders `/Cuenta` (login), `/Asistencia` (public
self-registration for meetings) and the `/Error` page. Pages under those folders that *do* need a
session carry their own `[Authorize]`.

**Certificate login** runs on its own Kestrel port (`Ports:DevCert`, 49176 in dev) with
`ClientCertificateMode.AllowCertificate`, pinned to TLS 1.2 with revocation checking off — physical
tokens (Bit4Id and similar) fail under TLS 1.3 or without internet for CRL. Behind IIS the cert
arrives in the `X-ARR-ClientCert` header, so `UseCertificateForwarding()` must stay **before**
`UseAuthentication()`. Sessions are shared between the `cert.*` subdomain and the main domain by
setting the cookie domain to `.<domain>` (skipped for `localhost` and bare IPs, which browsers
reject), which is also why Data Protection keys are persisted to a shared path
(`Storage:DataProtectionKeysPath`) under a fixed application name.

### Authorization

Roles are **rows in the `Roles` table**, administered at `/Accesos/Roles` — not an enum. The
`RolUsuario` enum survives only as the documented source of the six seeded roles; it is not dead
code. A role carries `NivelAlcance` plus four capabilities that replaced checks formerly hardcoded by
role name: `EsAdministrador`, `EsSoloLectura`, `EsSupervisor`, `EsTecnicoSoporte`.

Permissions are `Modulo.Accion` keys with a fixed action vocabulary (`Ver`, `Crear`, `Editar`,
`Eliminar`). When finer granularity is needed use a more specific *module*
(`Usuarios.Contrasenas`, `Contactos.Estado`) rather than inventing verbs, so the admin matrix stays
readable as module × action.

- Declare with `[Permission(modulo, accion)]` on a PageModel or on a single handler.
  `PermissionPageFilter` enforces it **per handler** — `[Authorize(Policy=...)]` cannot, because it
  only applies at class/endpoint level.
- `PermissionPolicyProvider` treats **any policy name it does not recognise as a permission key**, so
  `[Authorize(Policy = "Expedientes.Crear")]` resolves against the role×permission matrix. The old
  static policies are gone; don't add new ones.
- `PermissionCatalogSyncService` discovers keys by reflection at startup. **Any handler without
  `[Permission]`, `[AllowAnonymous]` or `[PermisoNoRequerido]` is logged as a warning** — the goal is
  to fail visibly.
- `[PermisoNoRequerido(razon)]` is the third case: self-service pages (own profile, own password, own
  notifications). Gating those with a grantable key would let one unchecked box stop someone from
  changing their own password.
- The grant cache is **per role and is not baked into the cookie**, so revoking applies to live
  sessions instead of waiting for them to expire.
- In views, ask `AccesoModulosService.PuedeClaveAsync` before rendering an action, and gate each link
  with the key of its *destination*, not of the page it lives on.

Two invariants prevent lock-out: `RolesModule` keeps at least one active role with `EsAdministrador`,
and `AdministradoresInvariante` keeps at least one active user assigned to such a role.
`/Accesos/Permisos` administers the matrix, `/Accesos/Auditoria` reads the append-only change log and
`/Accesos/Diagnostico` answers "why can't this user do X".

URL-level blocking used to live in `ModuloAccesoMiddleware`, a hand-written switch of 9 prefixes that
drifted until 11 of 20 modules were missing from it. It was deleted on purpose — the per-handler
filter plus the reflected catalogue replaces it. Don't bring back a hand-maintained list.

Two page-level attributes complement permissions: `[SoloEnDesarrollo]` returns 404 outside
Development (used for the import/migration tooling) and `[SeccionDeshabilitada]` returns 404
everywhere, parking a section without deleting its code.

### Expediente aggregate

The most complex aggregate (7 sections, 10 child collections). Child collections are always replaced
in bulk: `LimpiarHijos()` then `Agregar(...)` per item. `ActualizarExpedienteCommand` and
`ExpedienteMapper.Aplicar()` implement the pattern — follow it when adding new child types.

### Public API v1 (`src/Presentation`)

A separate host publishing the SIGER catalogue read-only to the external PortalDigital site:
`SaludController` (health), `TramitesPublicosController`, `InstitucionesPublicasController`,
`CategoriasPublicasController` and `CambiosController` (change feed for incremental sync). The
matching queries live in `src/Application/Siger`.

Auth is a single static key in the `X-Api-Key` header, validated against `PortalDigitalApi:ApiKey`
(decision P-02 — adequate for one known consumer; more consumers means moving to OAuth/JWT). A fixed
window rate limiter partitions by that key, falling back to `"anonimo"`. The frozen request/response
contract and the open questions are in `docs/api-v1/` — change the shape there before the code.

### Supporting services

- **Reports**: `InformeService` (Excel via ClosedXML) and `ActaPdfService` (meeting minutes via
  QuestPDF), both in `src/Infrastructure/Reports`.
- **Notifications**: `NotificacionService` plus `RecordatorioBackgroundService`, a hosted service
  sending reminders; tuned by the `Notificaciones` section.
- **Chat + AI**: `SoporteHub` (SignalR, mapped at `/hubs/soporte`) backs the support queue;
  `AgenteService` answers as a virtual assistant through the Anthropic API (`Ai` section — base URL,
  model, max tokens, timeout).
- **Email**: `SmtpEmailService` over the `Smtp` section.
- **Uploads** land in `src/Web/App_Data/uploads`, served back at `/uploads`; size and extension
  allow-lists come from the `Uploads` section, held in `UploadsConfig`.

### Data import (from the legacy demo portal on Supabase)

Idempotent import paths pull from the demo's Supabase project (`diger_tram`, a key/value table of
JSON blobs, plus relational `reuniones`/`asistencias`). All of it is **Administrator-only and
Development-only**:

- **Reuniones**: `SupabaseReunionImportSource` → `ImportarReunionesCommand` → `/Admin/ImportarReuniones`.
- **Expedientes**: `SupabaseExpedienteImporter` (Web-only `HttpClient` wrapper) → `/Admin/ImportarExpedientes`.
- **Catálogos** (`instituciones`, `levantamientos_estado`, calendar events → `Reunion`): `SupabaseCatalogosImporter`.
- **`/Admin/MigrarSupabase`** is the unified page: `SupabaseMigracionScanner` compares every source
  table against what is already imported and reports pending counts; "migrar" runs the three
  importers in order (catalogues first, so reuniones/expedientes can resolve their institution).

Idempotency: reuniones/expedientes dedupe on `OrigenExternoId` (unique filtered index; calendar
events use a `cal:<id>` prefix); levantamientos on institution+encargado; instituciones on name (with
an alias map for long-name↔sigla duplicates). Institution Ids are derived from the name and must be
`A-Z0-9` only — strip accents before filtering (`char.IsLetterOrDigit` wrongly accepts `Í`/`Ó`).

Bulk data loads that are not importable this way live as hand-written scripts in `database/`
(project seeding, permission grants, SIGER refresh via `import-siger.ps1`).

## Testing approach

Three tiers, deliberately using three different persistence strategies:

- **`Domain.Tests`** — plain instantiation, no infrastructure.
- **`Application.Tests`** — handlers instantiated directly against EF **In-Memory** with a
  `FakeCurrentUser` (global scope). No mocking framework needed for the happy path; NSubstitute is
  available for edge cases.
- **`Web.Tests`** — the real portal booted through `WebApplicationFactory<Program>` (`Program` is
  `public partial` for exactly this reason). It covers the wiring the other tiers cannot: filter,
  policy provider, authorization handler and per-role cache deciding access together — a gating bug
  once passed 116 green tests and only surfaced on screen.

`PortalFactory` (read its header comment before changing it) makes three choices that are easy to
break:

1. **SQLite in-memory, not the EF In-Memory provider** — the RLS filters use subqueries
   (`Areas.Any(...)`) that In-Memory cannot translate, and the connection must be held open for the
   database to survive.
2. **Environment `"Testing"`** — keeps `Program.cs` from migrating and seeding; the schema comes from
   `EnsureCreated`, since the migrations are SQL Server-specific.
3. **`PermissionCatalogSyncService` is left running** so tests run against the catalogue reflected
   from real PageModels rather than a hand-written list that would rot; `PermisosSeedService` is
   removed, and each test grants exactly the keys it needs. `TestAuthHandler` signs users in from
   request headers and exists **only** in the test host, never in the shipped binary.

## Repo conventions

- `AGENTS.md` at the root is an older Codex-facing copy of this file: its paths are wrong (they
  assume a `Portal-Informacion-Institucional\` prefix) and its scope/pipeline sections predate the
  current `NivelAlcance` filters. Treat this file as the source of truth, and update `AGENTS.md`
  alongside it or expect it to keep drifting.
- `.agents/rules/` and `.agents/workflows/` hold the CQRS/Razor conventions and scaffolding recipes;
  `Contextos/` holds the running analysis notes and the mandatory DB change log; `docs/api-v1/` holds
  the frozen public-API contract.
- Comments and identifiers are in Spanish throughout; match that when adding code.
