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

```powershell
# Build the entire solution
dotnet build Portal-Informacion-Institucional\Diger.TramitesEstado.sln

# Run the web app (primary UI)
dotnet run --project Portal-Informacion-Institucional\src\Web

# Run the API host
dotnet run --project Portal-Informacion-Institucional\src\Presentation

# Run all tests
dotnet test Portal-Informacion-Institucional\Diger.TramitesEstado.sln

# Run a single test project
dotnet test Portal-Informacion-Institucional\tests\Application.Tests

# Add a new EF migration (run from the Web project which owns the DbContext design-time reference)
dotnet ef migrations add <NombreMigracion> --project Portal-Informacion-Institucional\src\Infrastructure --startup-project Portal-Informacion-Institucional\src\Web
```

In development the web app auto-migrates and seeds seed usuarios on startup (`Program.cs`). Supabase import credentials go in User Secrets (UserSecretsId: `diger-tramites-estado-web`) under keys `Supabase:Url` and `Supabase:AnonKey`.

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

`AppDbContext` applies global EF query filters on `Expediente`, `Contacto`, `Ticket`, and `Reunion` so non-admin users only see records belonging to their assigned institutions. Filters are built from `ICurrentUserService.EsGlobal` / `InstitucionesAsignadas` at DbContext creation time. Queries that need to bypass scope (e.g., unique-code generation) call `.IgnoreQueryFilters()` explicitly.

### Authentication

Cookie-based auth (`CookieAuthenticationDefaults`). Roles: `Administrador`, `Coordinador`, `Tecnico`. Authorization policies (e.g., `PuedeGestionarExpedientes`) are defined in `Web/Program.cs`. All Razor Pages require authentication; `/Cuenta/*` is the anonymous exception.

### Data import

Two one-shot idempotent import paths exist:
- **Reuniones**: `SupabaseReunionImportSource` → `ImportarReunionesCommand` (Application) → `/Admin/ImportarReuniones` page.
- **Expedientes**: `SupabaseExpedienteImporter` (Web-only `HttpClient` wrapper) → `/Admin/ImportarExpedientes` page.

Both are idempotent via `OrigenExternoId` (unique index with `IS NOT NULL` filter).

### Expediente aggregate

`Expediente` is the most complex aggregate (7 sections, 10 child collections). Child collections are always replaced in bulk: call `LimpiarHijos()` then `Agregar(...)` for each item. The command handler (`ActualizarExpedienteCommand`) and `ExpedienteMapper.Aplicar()` implement this pattern — use the same pattern when adding new child types.

### Testing approach

Application tests use EF In-Memory with a `FakeCurrentUser` (global scope). Domain tests use plain instantiation. Tests instantiate handlers directly (no mocking framework needed for the happy path); NSubstitute is available for edge cases.
