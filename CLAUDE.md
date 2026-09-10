# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Solution overview

**DIGER — Trámites Estado** is an internal portal for DIGER (Honduras) to manage institutional processes (expedientes), meetings (reuniones), contacts (contactos), and support tickets. Target: .NET 9, SQL Server, xUnit.

The solution (`Diger.TramitesEstado.sln`) contains two runnable hosts and four shared libraries:

| Project | Role |
|---|---|
| `src/Web` | Razor Pages web app — the primary UI used in production |
| `src/Presentation` | Minimal API / Swagger host — alternative API surface |
| `src/Application` | CQRS handlers (MediatR), FluentValidation, pipeline behaviors |
| `src/Domain` | Entities, enums, domain events, `DomainException` |
| `src/Infrastructure` | EF Core (SQL Server), repositories, `IPasswordHasher`, `ICurrentUserService` |
| `tests/Application.Tests` | xUnit integration tests using EF In-Memory |
| `tests/Domain.Tests` | xUnit unit tests for domain logic |

## Common commands

All paths are relative to the repo root (which contains `Diger.TramitesEstado.sln`, `src/`, `tests/`).

```powershell
# Build the entire solution
dotnet build Diger.TramitesEstado.sln

# Run the web app (primary UI)
dotnet run --project src\Web

# Run the API host
dotnet run --project src\Presentation

# Run all tests
dotnet test Diger.TramitesEstado.sln

# Run one test project / a single test by name
dotnet test tests\Application.Tests
dotnet test tests\Application.Tests --filter "FullyQualifiedName~PersonasCapacitadas"

# Add a new EF migration (startup-project is Web, which owns the design-time DbContext)
dotnet ef migrations add <NombreMigracion> --project src\Infrastructure --startup-project src\Web --output-dir Persistence\Migrations
```

**Two migrations folders, one history**: `src/Infrastructure/Migrations/` (holds `AppDbContextModelSnapshot.cs`, migrations up to 2026-07-31) and `src/Infrastructure/Persistence/Migrations/` (every migration since 2026-08-05, SIGER and otherwise) both feed the same `__EFMigrationsHistory` table. Current convention: pass `--output-dir Persistence\Migrations` explicitly — `dotnet ef migrations add` with no `--output-dir` writes to the first folder instead (where the snapshot lives), which is now the stale location.

## Local development

- **Database**: SQL Server LocalDB. The dev connection string (`src/Web/appsettings.Development.json`) targets `(localdb)\MSSQLLocalDB`. On startup, in Development, the app auto-migrates and calls `DbSeeder.SeedUsuariosAsync` (`Program.cs`) **only if** `Datos:AplicarMigracionesAlArrancar` is `true` — off by default, on purpose: a `Development` connection string that happens to point at a shared/production-named database must not silently migrate it just because someone pressed F5. Enable it per machine, never in a committed appsettings file — e.g. `dotnet user-secrets set "Datos:AplicarMigracionesAlArrancar" true --project src\Web` (and the same for `src\Presentation` if you run that host), or the `Datos__AplicarMigracionesAlArrancar=true` environment variable. Tests never touch this — they use EF In-Memory.
- **Seeded logins** (from `DbSeeder`, all password-hashed): `admin@diger.gob.hn` / `Admin#2026` (Administrador), plus `jefe.inst@`, `jefe.area@`, `jefe.uni@`, `empleado@`, `consultor@` with passwords `JefeInst#2026`, `JefeArea#2026`, `JefeUni#2026`, `Empleado#2026`, `Consultor#2026`.
- **Ports**: `launchSettings.json` binds https `49175`/`49176` + http `49177`; `.claude/launch.json` ("web") runs http `5011`. The certificate-login flow hard-codes `https://localhost:49176/Cuenta/LoginCertificado` in Development, so keep 49176 free when testing cert login.
- **Secrets**: Supabase import credentials live in User Secrets (UserSecretsId `diger-tramites-estado-web`) under `Supabase:Url` / `Supabase:AnonKey` — not in appsettings.
- **LocalDB gotcha**: the instance sleeps on inactivity and occasionally leaves a stuck `sqlservr` process, so app startup can fail on `MigrateAsync()` with "SQL Server process failed to start". Fix: `sqllocaldb start MSSQLLocalDB` (or kill the zombie `sqlservr` and restart). The DbContext registration uses `EnableRetryOnFailure`, so avoid explicit EF transactions (they are incompatible with the retry strategy).
- **Build lock**: if `dotnet build` fails only with `MSB3027`/`MSB3021` copy errors, a running app (or Visual Studio debug session) is holding the DLLs — stop it before building. `.cshtml` edits require a rebuild/restart; they are not hot-reloaded by a running instance.

## Architecture

### Clean Architecture layers

```
Web / Presentation  →  Application  →  Domain
                    →  Infrastructure (registered via DI)
```

- **Domain** has zero dependencies. `BaseAuditableEntity` is the auditable root; `BaseEntity` holds the domain events list. Aggregate roots (`Expediente`, `Reunion`, `Ticket`) expose factory methods (`Crear(...)`) and mutation methods instead of public setters.
- **Application** knows only the Domain and the interfaces it defines in `Common/Interfaces/IRepositories.cs`. Commands and Queries each live in their own folder under the feature name (e.g., `Expedientes/Commands/CrearExpediente/`). Command records, handlers, and validators are co-located in the same file. FluentValidation validators are auto-registered and run through `ValidationBehavior`.
- **Infrastructure** implements every interface from Application: `AppDbContext`, repositories, `PasswordHasher`, `CurrentUserService`, and the Supabase HTTP import sources.
- **Web** is the only project that knows about all four layers. Razor Pages call MediatR `ISender`; they never call repositories directly.

### MediatR pipeline

Every request flows through `LoggingBehavior` → `ValidationBehavior` → handler. Handlers receive their repository and `IUnitOfWork` (both implemented by `AppDbContext`) via constructor injection.

### Institutional scope (data isolation)

`AppDbContext` applies global EF query filters so users only see records within their scope. Each filter is `!IsDeleted && (<scope>)` — soft-delete (`ISoftDeletable`) is AND-ed into the same filter, so ordinary queries never see soft-deleted rows.

The scope comes from **`NivelAlcance`**, a property of the user's role (`Global` / `Institucion` / `Area` / `Unidad`), not from the role's name. Every non-global branch is anchored on `InstitucionId == _activeInst`; without that anchor the area/unit branches leaked records across institutions. A role that can't be resolved falls back to `Unidad`, the most restrictive.

Queries that need to bypass scope (unique-code generation, importers checking existing `OrigenExternoId`s) call `.IgnoreQueryFilters()` explicitly.

`Usuario` and `AsignacionUsuario` are **not** filtered — user administration is global today.

### Authentication

Cookie-based auth (`CookieAuthenticationDefaults`). A user holds one or more assignments (`AsignacionUsuario` = institution + optional area/unit + role); the login makes the first one the active context and stores the rest in the `AsignacionesJson` claim so the user can switch context in the UI.

**A user with no assignment has no role.** The login emits no role claim, which makes `CurrentUserService` fail closed (minimum scope, no capabilities) and denies every module; the user is redirected to their profile with a notice. Don't reintroduce a default role here — an earlier `?? "Empleado"` silently granted 32 permission keys to unconfigured accounts.

All Razor Pages require authentication. `/Cuenta/*` is exempt by convention because login needs it, so pages in that folder that *do* require a session carry their own `[Authorize]`.

### Authorization

Roles are **rows in the `Roles` table**, administered at `/Accesos/Roles` — not an enum. The `RolUsuario` enum survives only as the documented source of the six seeded roles; it is not dead code. A role carries `NivelAlcance` plus four capabilities that replaced checks formerly hardcoded by role name: `EsAdministrador` (approves everything by code), `EsSoloLectura`, `EsSupervisor`, `EsTecnicoSoporte`.

Permissions are `Modulo.Accion` keys with a fixed action vocabulary (`Ver`, `Crear`, `Editar`, `Eliminar`). When finer granularity is needed, use a more specific *module* (`Usuarios.Contrasenas`, `Contactos.Estado`) rather than inventing verbs, so the admin matrix stays readable as module × action.

- Declare with `[Permission(modulo, accion)]` on a PageModel or on a single handler. `PermissionPageFilter` enforces it per handler — `[Authorize(Policy=...)]` can't, because it only applies at class/endpoint level.
- `PermissionCatalogSyncService` discovers the keys by reflection at startup and syncs the `Permisos` table. **Any handler without `[Permission]`, `[AllowAnonymous]` or `[PermisoNoRequerido]` is logged as a warning** — the goal is to fail visibly.
- `[PermisoNoRequerido(razon)]` is the third case: self-service pages (own profile, own password, own notifications). Gating those with a grantable key would let one unchecked box stop someone from changing their own password.
- The grant cache is **per role and is not baked into the cookie**, so revoking applies to live sessions instead of waiting for them to expire.
- In views, ask `AccesoModulosService.PuedeClaveAsync` before rendering an action. Gate each link with the key of its *destination*, not the key of the page it lives on.

Two invariants protect against locking everyone out: `RolesModule` keeps at least one active role with `EsAdministrador`, and `AdministradoresInvariante` keeps at least one active user assigned to such a role.

`/Accesos/Permisos` administers the matrix, `/Accesos/Auditoria` reads the append-only change log, and `/Accesos/Diagnostico` answers "why can't this user do X".

### Data import (from the legacy demo portal on Supabase)

Idempotent import paths pull data from the demo's Supabase project (`diger_tram` is a key/value table of JSON blobs, plus relational `reuniones`/`asistencias`). All import/migration is **Administrator-only and Development-only**:

- **Reuniones**: `SupabaseReunionImportSource` → `ImportarReunionesCommand` (Application) → `/Admin/ImportarReuniones`.
- **Expedientes**: `SupabaseExpedienteImporter` (Web-only `HttpClient` wrapper) → `/Admin/ImportarExpedientes`.
- **Catálogos** (`instituciones`, `levantamientos_estado`, calendar events → `Reunion`): `SupabaseCatalogosImporter`.
- **`/Admin/MigrarSupabase`** is the unified page: `SupabaseMigracionScanner` compares every source table against what's already imported and reports pending counts; the "migrar" action runs the three importers in order (catalogs first, so reuniones/expedientes can resolve their institution).

Idempotency: reuniones/expedientes dedupe on `OrigenExternoId` (unique filtered index; calendar events use a `cal:<id>` prefix); levantamientos dedupe on institution+encargado; instituciones on name (with an alias map for long-name↔sigla duplicates). Institution Ids are derived from the name and must be `A-Z0-9` only — strip accents before filtering (`char.IsLetterOrDigit` wrongly accepts `Í`/`Ó`).

### Expediente aggregate

`Expediente` is the most complex aggregate (7 sections, 10 child collections). Child collections are always replaced in bulk: call `LimpiarHijos()` then `Agregar(...)` for each item. The command handler (`ActualizarExpedienteCommand`) and `ExpedienteMapper.Aplicar()` implement this pattern — use the same pattern when adding new child types.

### Testing approach

Application tests use EF In-Memory with a `FakeCurrentUser` (global scope). Domain tests use plain instantiation. Tests instantiate handlers directly (no mocking framework needed for the happy path); NSubstitute is available for edge cases.
