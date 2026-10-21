# Tableros de Proyectos en tres niveles — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give Proyectos tres vistas (Unidad, Área, Institución) respaldadas por un mecanismo real de acceso — interesados automáticos y bloqueados para dos capacidades nuevas de rol (`EsJefeDeArea`, `EsPmo`) — sin agregar un filtro de alcance nuevo en `AppDbContext`.

**Architecture:** El acceso sigue mediado por `InteresadoProyecto` (ya existente). Un servicio de aplicación nuevo (`IInteresadosAutomaticosSync`) mantiene sincronizadas, en dos direcciones, las filas automáticas: por proyecto (al crearlo o cambiarle área/unidad) y por usuario (al cambiar su asignación jerárquica). Las vistas Unidad y Área comparten una sola consulta liviana (`GetMisProyectosDashboardQuery`, filtrada por "soy interesado o responsable"); Institución reutiliza el tablero denso que ya existe, con un filtro de área agregado.

**Tech Stack:** .NET 9, EF Core 9 (SQL Server), MediatR, Razor Pages, Chart.js (ya cargado), xUnit + NSubstitute + FluentAssertions + EF In-Memory (Application.Tests).

**Spec:** `docs/superpowers/specs/2026-09-02-tableros-proyectos-tres-niveles-design.md`

## Global Constraints

- No se agrega ningún filtro de alcance nuevo en `AppDbContext` — el acceso sigue siendo por `InteresadoProyecto`/`Responsable` (spec, sección "Mecanismo de acceso").
- Las capacidades nuevas de rol se identifican por bandera booleana en `Roles`, nunca por nombre del rol (spec + principio ya establecido en `RolesModule`/`PermissionCatalogSyncService`).
- `RolInteresado` no gana un valor nuevo: jefe de área = `Patrocinador`, PMO = `Ejecutor` (spec).
- Todas las migraciones EF se generan con `--output-dir Persistence\Migrations` (CLAUDE.md) — nunca se omite esa bandera.
- Cada `dotnet test` se corre contra `tests\Application.Tests` (EF In-Memory) o `tests\Domain.Tests`; nunca contra una base real.

---

## Task 1: Capacidades `EsJefeDeArea` / `EsPmo` en `Rol` — de dominio a `ICurrentUserService`

**Files:**
- Modify: `src/Domain/Entities/Rol.cs`
- Modify: `src/Application/Common/Interfaces/IRolCatalogo.cs`
- Modify: `src/Infrastructure/Security/RolCatalogo.cs:35-41`
- Modify: `src/Application/Common/Interfaces/IRepositories.cs:110-143` (interfaz `ICurrentUserService`)
- Modify: `src/Infrastructure/Security/CurrentUserService.cs`
- Create: `src/Infrastructure/Persistence/Migrations/Persistence/Migrations/<timestamp>_AgregarJefeDeAreaYPmo.cs` (generada por CLI, no se escribe a mano)
- Test: `tests/Domain.Tests/Entidades/RolTests.cs` (crear si no existe un archivo de tests de `Rol`; si existe uno, agregar ahí)

**Interfaces:**
- Produces: `Rol.EsJefeDeArea`/`Rol.EsPmo` (bool, público, setter privado). `RolInfo.EsJefeDeArea`/`RolInfo.EsPmo`. `ICurrentUserService.EsJefeDeArea`/`ICurrentUserService.EsPmo` (bool). `Rol.Crear(...)` y `Rol.Actualizar(...)` ganan dos parámetros bool al final: `esJefeDeArea = false, esPmo = false`.

- [ ] **Step 1: Escribir el test que falla, para `Rol.Crear`/`Rol.Actualizar` con las dos capacidades nuevas**

En `tests/Domain.Tests/Entidades/RolTests.cs` (crear el archivo si no existe ninguno para `Rol`; si ya existe, agregar estos dos `[Fact]` al final de la clase existente):

```csharp
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Domain.Tests.Entidades;

public class RolTests
{
    [Fact]
    public void Crear_ConEsJefeDeAreaYEsPmo_LasPersiste()
    {
        var rol = Rol.Crear(
            "JefeArea", "Jefe de Área", NivelAlcance.Area,
            esJefeDeArea: true, esPmo: false);

        rol.EsJefeDeArea.Should().BeTrue();
        rol.EsPmo.Should().BeFalse();
    }

    [Fact]
    public void Actualizar_CambiaEsJefeDeAreaYEsPmo()
    {
        var rol = Rol.Crear("Pmo", "PMO", NivelAlcance.Unidad);

        rol.Actualizar(
            "PMO", NivelAlcance.Unidad, null, null,
            esAdministrador: false, esSoloLectura: false, esSupervisor: false, esTecnicoSoporte: false,
            esJefeDeArea: false, esPmo: true);

        rol.EsPmo.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Correr los tests y confirmar que fallan (no compila — faltan los parámetros)**

Run: `dotnet test tests\Domain.Tests --filter "FullyQualifiedName~RolTests"`
Expected: error de compilación — `Rol.Crear`/`Rol.Actualizar` no tienen `esJefeDeArea`/`esPmo`.

- [ ] **Step 3: Agregar las propiedades y parámetros en `Rol` (dominio)**

En `src/Domain/Entities/Rol.cs`, agregar las dos propiedades junto a las otras cuatro capacidades (después de línea 25, antes de `Activo`):

```csharp
    public bool         EsTecnicoSoporte { get; private set; }
    public bool         EsJefeDeArea     { get; private set; }
    public bool         EsPmo            { get; private set; }
    public bool         Activo           { get; private set; } = true;
```

`Crear` (firma actual en líneas 33-43) gana dos parámetros al final, con default `false` para no romper los call-sites existentes que no los pasan:

```csharp
    public static Rol Crear(
        string codigo,
        string nombre,
        NivelAlcance nivelAlcance,
        string? descripcion = null,
        string? color = null,
        bool esAdministrador = false,
        bool esSoloLectura = false,
        bool esSupervisor = false,
        bool esTecnicoSoporte = false,
        bool esSistema = false,
        bool esJefeDeArea = false,
        bool esPmo = false)
```

Y dentro del `new Rol { ... }` (líneas 51-64), agregar:

```csharp
            EsTecnicoSoporte = esTecnicoSoporte,
            EsJefeDeArea = esJefeDeArea,
            EsPmo = esPmo,
            EsSistema = esSistema,
```

`Actualizar` (firma actual en líneas 81-89) gana los mismos dos parámetros al final:

```csharp
    public void Actualizar(
        string nombre,
        NivelAlcance nivelAlcance,
        string? descripcion,
        string? color,
        bool esAdministrador,
        bool esSoloLectura,
        bool esSupervisor,
        bool esTecnicoSoporte,
        bool esJefeDeArea,
        bool esPmo)
```

Y dentro del cuerpo (líneas 96-103), agregar:

```csharp
        EsTecnicoSoporte = esTecnicoSoporte;
        EsJefeDeArea = esJefeDeArea;
        EsPmo = esPmo;
```

- [ ] **Step 4: Correr los tests de dominio y confirmar que pasan**

Run: `dotnet test tests\Domain.Tests --filter "FullyQualifiedName~RolTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Propagar las capacidades a `RolInfo` y `RolCatalogo`**

En `src/Application/Common/Interfaces/IRolCatalogo.cs`, el record `RolInfo` (líneas 4-12) gana dos campos al final:

```csharp
public sealed record RolInfo(
    string       Codigo,
    string       Nombre,
    NivelAlcance NivelAlcance,
    bool         EsAdministrador,
    bool         EsSoloLectura,
    bool         EsSupervisor,
    bool         EsTecnicoSoporte,
    bool         EsJefeDeArea,
    bool         EsPmo,
    string?      Color);
```

En `src/Infrastructure/Security/RolCatalogo.cs`, dentro de `RecargarAsync` (líneas 35-41), el `Select` que construye `RolInfo` pasa los dos campos nuevos, respetando el orden posicional del record:

```csharp
            var roles = await ctx.Roles
                .Where(r => r.Activo)
                .Select(r => new RolInfo(
                    r.Id, r.Nombre, r.NivelAlcance,
                    r.EsAdministrador, r.EsSoloLectura, r.EsSupervisor, r.EsTecnicoSoporte,
                    r.EsJefeDeArea, r.EsPmo,
                    r.Color))
                .ToListAsync(ct);
```

- [ ] **Step 6: Exponer las dos capacidades en `ICurrentUserService`**

En `src/Application/Common/Interfaces/IRepositories.cs`, dentro de la interfaz `ICurrentUserService` (líneas 110-143), agregar junto a `EsTecnicoSoporte`:

```csharp
    bool EsTecnicoSoporte { get; }
    bool EsJefeDeArea { get; }
    bool EsPmo { get; }
```

En `src/Infrastructure/Security/CurrentUserService.cs`, junto a la propiedad `EsTecnicoSoporte` existente, agregar:

```csharp
    public bool EsTecnicoSoporte      => RolActual?.EsTecnicoSoporte == true;
    public bool EsJefeDeArea          => RolActual?.EsJefeDeArea == true;
    public bool EsPmo                 => RolActual?.EsPmo == true;
```

- [ ] **Step 7: Compilar Domain, Application e Infrastructure**

Run: `dotnet build src\Domain\Diger.TramitesEstado.Domain.csproj && dotnet build src\Application\Diger.TramitesEstado.Application.csproj && dotnet build src\Infrastructure\Diger.TramitesEstado.Infrastructure.csproj`
Expected: los tres compilan sin errores (Web fallará mientras no se toque en un task posterior si depende de las firmas viejas de `Rol.Crear`/`Actualizar` en tests — no debería, porque los parámetros nuevos tienen default).

- [ ] **Step 8: Generar la migración EF para las dos columnas nuevas en `Roles`**

Run:
```powershell
dotnet ef migrations add AgregarJefeDeAreaYPmo --project src\Infrastructure --startup-project src\Web --output-dir Persistence\Migrations
```

Verificar que el `Up()` generado contenga (y si el generador no produjo exactamente esto, corregirlo a mano en el archivo `<timestamp>_AgregarJefeDeAreaYPmo.cs` para que quede igual — el patrón ya usado en `20260814160739_AgregarCamposVentanilla.cs` para columnas bool):

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<bool>(
        name: "EsJefeDeArea", table: "Roles", type: "bit",
        nullable: false, defaultValue: false);

    migrationBuilder.AddColumn<bool>(
        name: "EsPmo", table: "Roles", type: "bit",
        nullable: false, defaultValue: false);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(name: "EsJefeDeArea", table: "Roles");
    migrationBuilder.DropColumn(name: "EsPmo", table: "Roles");
}
```

- [ ] **Step 9: Commit**

```bash
git add src/Domain/Entities/Rol.cs src/Application/Common/Interfaces/IRolCatalogo.cs src/Application/Common/Interfaces/IRepositories.cs src/Infrastructure/Security/RolCatalogo.cs src/Infrastructure/Security/CurrentUserService.cs src/Infrastructure/Persistence/Migrations/Persistence/Migrations/*AgregarJefeDeAreaYPmo* tests/Domain.Tests/Entidades/RolTests.cs
git commit -m "feat: agrega capacidades EsJefeDeArea/EsPmo a Rol, RolInfo e ICurrentUserService"
```

---

## Task 2: `CrearRolCommand`/`ActualizarRolCommand` + UI de `/Accesos/Roles`

**Files:**
- Modify: `src/Application/Roles/RolesModule.cs:36-100`
- Modify: `src/Web/Pages/Accesos/Roles.cshtml.cs`
- Modify: `src/Web/Pages/Accesos/Roles.cshtml:128-148`
- Test: `tests/Application.Tests/Permisos/RolesModuleTests.cs`

**Interfaces:**
- Consumes: `Rol.Crear`/`Rol.Actualizar` (Task 1).
- Produces: `CrearRolCommand`/`ActualizarRolCommand` con dos parámetros bool nuevos al final (`EsJefeDeArea`, `EsPmo`).

- [ ] **Step 1: Escribir el test que falla para `CrearRolCommand` con las capacidades nuevas**

Agregar a `tests/Application.Tests/Permisos/RolesModuleTests.cs` (mismo estilo que `Crear_RolNuevo_PersisteYRecargaElCatalogo`, ya existente en el archivo):

```csharp
    [Fact]
    public async Task Crear_RolConJefeDeAreaYPmo_LasPersiste()
    {
        var handler = new CrearRolCommandHandler(_ctx, _catalogo);

        await handler.Handle(
            new CrearRolCommand("JefeGobDigital", "Jefe Gobierno Digital", NivelAlcance.Area, null, null,
                false, false, false, false, EsJefeDeArea: true, EsPmo: false),
            CancellationToken.None);

        var guardado = await _ctx.Roles.SingleAsync(r => r.Id == "JefeGobDigital");
        guardado.EsJefeDeArea.Should().BeTrue();
        guardado.EsPmo.Should().BeFalse();
    }
```

- [ ] **Step 2: Correr y confirmar que falla (no compila)**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~RolesModuleTests"`
Expected: error de compilación — `CrearRolCommand` no acepta `EsJefeDeArea`/`EsPmo`.

- [ ] **Step 3: Agregar los parámetros a `CrearRolCommand`/`ActualizarRolCommand` y pasarlos al dominio**

En `src/Application/Roles/RolesModule.cs`, el record `CrearRolCommand` (líneas 36-38) queda:

```csharp
public sealed record CrearRolCommand(
    string Codigo, string Nombre, NivelAlcance NivelAlcance, string? Descripcion, string? Color,
    bool EsAdministrador, bool EsSoloLectura, bool EsSupervisor, bool EsTecnicoSoporte,
    bool EsJefeDeArea = false, bool EsPmo = false) : IRequest<string>;
```

Dentro de `CrearRolCommandHandler.Handle` (líneas 44-48), la llamada a `Rol.Crear` pasa los dos nuevos:

```csharp
        var rol = Rol.Crear(
            codigo, cmd.Nombre, cmd.NivelAlcance, cmd.Descripcion, cmd.Color,
            cmd.EsAdministrador, cmd.EsSoloLectura, cmd.EsSupervisor, cmd.EsTecnicoSoporte,
            esJefeDeArea: cmd.EsJefeDeArea, esPmo: cmd.EsPmo);
```

El record `ActualizarRolCommand` (líneas 62-64) queda:

```csharp
public sealed record ActualizarRolCommand(
    string Codigo, string Nombre, NivelAlcance NivelAlcance, string? Descripcion, string? Color,
    bool EsAdministrador, bool EsSoloLectura, bool EsSupervisor, bool EsTecnicoSoporte,
    bool Activo, bool EsJefeDeArea = false, bool EsPmo = false) : IRequest<Unit>;
```

Dentro de `ActualizarRolCommandHandler.Handle`, la llamada a `rol.Actualizar` pasa los dos nuevos:

```csharp
        rol.Actualizar(
            cmd.Nombre, cmd.NivelAlcance, cmd.Descripcion, cmd.Color,
            cmd.EsAdministrador, cmd.EsSoloLectura, cmd.EsSupervisor, cmd.EsTecnicoSoporte,
            cmd.EsJefeDeArea, cmd.EsPmo);
```

- [ ] **Step 4: Correr los tests de Application y confirmar que pasan**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~RolesModuleTests"`
Expected: PASS (todos los tests de la clase, incluido el nuevo).

- [ ] **Step 5: Agregar los checkboxes en `/Accesos/Roles`**

En `src/Web/Pages/Accesos/Roles.cshtml`, dentro del `<div>` de "Capacidades" (líneas 128-148), agregar después del checkbox de `EsTecnicoSoporte` y antes del `</div>` de cierre:

```html
        <label class="chk">
            <input type="checkbox" name="EsJefeDeArea" value="true" checked="@Model.EsJefeDeArea" />
            <span><strong>Jefe de área</strong> — queda como interesado automático (no removible) en todos los proyectos de su área</span>
        </label>
        <label class="chk">
            <input type="checkbox" name="EsPmo" value="true" checked="@Model.EsPmo" />
            <span><strong>PMO</strong> — queda como interesado automático (no removible) en todos los proyectos de su unidad, con permiso para accionar sobre ellos</span>
        </label>
```

En `src/Web/Pages/Accesos/Roles.cshtml.cs`, agregar las dos propiedades enlazadas junto a las otras cuatro (mismo bloque de `[BindProperty]` que ya trae `EsAdministrador`/`EsSoloLectura`/`EsSupervisor`/`EsTecnicoSoporte`):

```csharp
    [BindProperty] public bool EsJefeDeArea { get; set; }
    [BindProperty] public bool EsPmo { get; set; }
```

En `OnGetAsync`, junto a donde se leen las otras cuatro desde el rol existente al editar, agregar:

```csharp
        EsJefeDeArea = rol.EsJefeDeArea;
        EsPmo = rol.EsPmo;
```

En `OnPostGuardarAsync`, en las construcciones de `CrearRolCommand`/`ActualizarRolCommand`, agregar `EsJefeDeArea, EsPmo` al final de los argumentos posicionales existentes.

- [ ] **Step 6: Compilar Web**

Run: `dotnet build src\Web\Diger.TramitesEstado.Web.csproj`
Expected: compila sin errores (si Visual Studio tiene el proceso corriendo y bloquea el DLL, es el problema de candado ya conocido en este repo — parar el debugger antes de correr esto).

- [ ] **Step 7: Commit**

```bash
git add src/Application/Roles/RolesModule.cs src/Web/Pages/Accesos/Roles.cshtml src/Web/Pages/Accesos/Roles.cshtml.cs tests/Application.Tests/Permisos/RolesModuleTests.cs
git commit -m "feat: EsJefeDeArea/EsPmo administrables desde /Accesos/Roles"
```

---

## Task 3: `InteresadoProyecto.Automatico` — no removible desde la ficha

**Files:**
- Modify: `src/Domain/Entities/InteresadoProyecto.cs`
- Modify: `src/Infrastructure/Persistence/AppDbContext.cs` (`InteresadoProyectoConfiguration`, ver sección `ProyectoInteresados`)
- Modify: `src/Application/Proyectos/Commands/InteresadoCommands.cs:95-119` (`QuitarInteresadoCommandHandler`)
- Test: `tests/Application.Tests/Proyectos/InteresadoCommandsTests.cs` (crear si no existe)

**Interfaces:**
- Produces: `InteresadoProyecto.Automatico` (bool, público, setter privado). `InteresadoProyecto.CrearAutomatico(int proyectoId, Guid usuarioId, string nombre, RolInteresado rol, string? correo)` — factory nueva, además de `Crear` (que sigue existiendo, sin tocar, para altas manuales).

- [ ] **Step 1: Escribir el test que falla para el rechazo de `QuitarInteresadoCommand` sobre una fila automática**

Crear `tests/Application.Tests/Proyectos/InteresadoCommandsTests.cs`:

```csharp
using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Proyectos;

public class InteresadoCommandsTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public InteresadoCommandsTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.EsGlobal.Returns(true);
        currentUser.Nombre.Returns("Prueba");
        _ctx = new AppDbContext(opts, currentUser, Substitute.For<MediatR.IPublisher>());
    }

    [Fact]
    public async Task Quitar_InteresadoAutomatico_SeRechaza()
    {
        var proyecto = Proyecto.Crear("PRY-2026-99", "Proyecto de prueba");
        _ctx.Proyectos.Add(proyecto);
        await _ctx.SaveChangesAsync();

        var interesado = InteresadoProyecto.CrearAutomatico(
            proyecto.Id, Guid.NewGuid(), "Jefe de Área", RolInteresado.Patrocinador, null);
        _ctx.ProyectoInteresados.Add(interesado);
        await _ctx.SaveChangesAsync();

        var handler = new QuitarInteresadoCommandHandler(_ctx, Substitute.For<ICurrentUserService>());

        var accion = async () => await handler.Handle(new QuitarInteresadoCommand(interesado.Id), CancellationToken.None);

        await accion.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Quitar_InteresadoManual_SePermite()
    {
        var proyecto = Proyecto.Crear("PRY-2026-98", "Proyecto de prueba");
        _ctx.Proyectos.Add(proyecto);
        await _ctx.SaveChangesAsync();

        var interesado = InteresadoProyecto.Crear(
            proyecto.Id, Guid.NewGuid(), "Interesado manual", RolInteresado.Beneficiario, "Prueba");
        _ctx.ProyectoInteresados.Add(interesado);
        await _ctx.SaveChangesAsync();

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.Nombre.Returns("Prueba");
        var handler = new QuitarInteresadoCommandHandler(_ctx, currentUser);

        await handler.Handle(new QuitarInteresadoCommand(interesado.Id), CancellationToken.None);

        (await _ctx.ProyectoInteresados.AnyAsync(i => i.Id == interesado.Id)).Should().BeFalse();
    }

    public void Dispose() => _ctx.Dispose();
}
```

- [ ] **Step 2: Correr y confirmar que falla (no compila — falta `Automatico`/`CrearAutomatico`)**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~InteresadoCommandsTests"`
Expected: error de compilación.

- [ ] **Step 3: Agregar `Automatico` y `CrearAutomatico` al dominio**

En `src/Domain/Entities/InteresadoProyecto.cs`, agregar la propiedad junto a `Notas` (antes de `RegistradoPor`):

```csharp
    public string? Notas { get; private set; }

    /// <summary>True si esta fila la creó la sincronización automática (EsJefeDeArea/EsPmo), no una
    /// persona a mano. Estas filas no se pueden quitar desde la ficha — ver QuitarInteresadoCommand
    /// — porque son la forma en que el jefe de área/PMO conserva acceso mientras tenga ese rol.</summary>
    public bool Automatico { get; private set; }
```

Agregar la factory nueva, después de `Crear` (después de la línea 111, `}`  que cierra `Crear`):

```csharp
    /// <summary>Alta hecha por la sincronización automática (EsJefeDeArea/EsPmo), no por una
    /// persona. Mismo registro que Crear, pero marcado Automatico — ver QuitarInteresadoCommand,
    /// que rechaza quitar estas filas desde la ficha.</summary>
    public static InteresadoProyecto CrearAutomatico(
        int proyectoId, Guid usuarioId, string nombre, RolInteresado rol, string? correo)
    {
        var interesado = Crear(
            proyectoId, usuarioId, nombre, rol,
            registradoPor: "Sistema (sincronización automática)",
            correo: correo);
        interesado.Automatico = true;
        return interesado;
    }
```

- [ ] **Step 4: Mapear la columna nueva en `AppDbContext`**

En `src/Infrastructure/Persistence/AppDbContext.cs`, dentro de `InteresadoProyectoConfiguration.Configure` (la clase ya vista con `b.ToTable("ProyectoInteresados")`), agregar:

```csharp
        b.Property(x => x.Automatico).HasDefaultValue(false);
```

- [ ] **Step 5: Rechazar la eliminación en `QuitarInteresadoCommandHandler`**

En `src/Application/Proyectos/Commands/InteresadoCommands.cs`, dentro de `QuitarInteresadoCommandHandler.Handle` (líneas 102-118), agregar la guarda justo después de encontrar `interesado` y antes de armar la entrada de bitácora:

```csharp
        var interesado = await ctx.ProyectoInteresados
            .FirstOrDefaultAsync(i => i.Id == cmd.InteresadoId, ct)
            ?? throw new NotFoundException(nameof(InteresadoProyecto), cmd.InteresadoId);

        if (interesado.Automatico)
            throw new DomainException(
                $"«{interesado.Nombre}» quedó como interesado automáticamente por su rol de área o " +
                "unidad. Se quita solo cuando deja de tener ese rol o esa asignación — no se puede " +
                "quitar desde aquí.");
```

- [ ] **Step 6: Correr los tests y confirmar que pasan**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~InteresadoCommandsTests"`
Expected: PASS (2 tests).

- [ ] **Step 7: Generar la migración EF para `ProyectoInteresados.Automatico`**

Run:
```powershell
dotnet ef migrations add AgregarInteresadoAutomatico --project src\Infrastructure --startup-project src\Web --output-dir Persistence\Migrations
```

Confirmar que el `Up()` sea equivalente a:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<bool>(
        name: "Automatico", table: "ProyectoInteresados", type: "bit",
        nullable: false, defaultValue: false);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(name: "Automatico", table: "ProyectoInteresados");
}
```

- [ ] **Step 8: Commit**

```bash
git add src/Domain/Entities/InteresadoProyecto.cs src/Infrastructure/Persistence/AppDbContext.cs src/Application/Proyectos/Commands/InteresadoCommands.cs src/Infrastructure/Persistence/Migrations/Persistence/Migrations/*AgregarInteresadoAutomatico* tests/Application.Tests/Proyectos/InteresadoCommandsTests.cs
git commit -m "feat: InteresadoProyecto.Automatico — no se puede quitar desde la ficha"
```

---

## Task 4: Servicio de sincronización `IInteresadosAutomaticosSync`

**Files:**
- Create: `src/Application/Proyectos/Services/InteresadosAutomaticosSync.cs`
- Modify: `src/Application/DependencyInjection.cs`
- Test: `tests/Application.Tests/Proyectos/InteresadosAutomaticosSyncTests.cs`

**Interfaces:**
- Consumes: `Rol.EsJefeDeArea`/`EsPmo` vía `IRolCatalogo.Obtener` (Task 1), `InteresadoProyecto.CrearAutomatico`/`.Automatico` (Task 3), `IApplicationDbContext.AsignacionesUsuario`/`.Proyectos`/`.ProyectoInteresados`/`.Usuarios` (ya existentes).
- Produces:
  ```csharp
  public interface IInteresadosAutomaticosSync
  {
      Task SincronizarProyectoAsync(int proyectoId, CancellationToken ct = default);
      Task SincronizarUsuarioAsync(Guid usuarioId, CancellationToken ct = default);
  }
  ```
  Usado por Task 5 desde `CrearProyectoCommandHandler`, `ActualizarProyectoCommandHandler` y `AsignarJerarquiaUsuarioCommandHandler`.

- [ ] **Step 1: Escribir el test que falla para `SincronizarProyectoAsync`**

Crear `tests/Application.Tests/Proyectos/InteresadosAutomaticosSyncTests.cs`:

```csharp
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Services;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Proyectos;

public class InteresadosAutomaticosSyncTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly IRolCatalogo _catalogo = Substitute.For<IRolCatalogo>();
    private readonly InteresadosAutomaticosSyncService _sync;

    public InteresadosAutomaticosSyncTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.EsGlobal.Returns(true);
        _ctx = new AppDbContext(opts, currentUser, Substitute.For<MediatR.IPublisher>());
        _sync = new InteresadosAutomaticosSyncService(_ctx, _catalogo);
    }

    private async Task<Usuario> SembrarUsuarioAsync(string nombre)
    {
        var usuario = Usuario.Crear(nombre, $"{Guid.NewGuid()}@diger.gob.hn", "hash");
        _ctx.Usuarios.Add(usuario);
        await _ctx.SaveChangesAsync();
        return usuario;
    }

    [Fact]
    public async Task SincronizarProyecto_AgregaAlJefeDeAreaComoInteresadoAutomatico()
    {
        var jefe = await SembrarUsuarioAsync("Jefe de Área");
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(jefe.Id, "DIGER", "GOBDIGITAL", null, "JefeArea"));
        _catalogo.Obtener("JefeArea").Returns(new RolInfo(
            "JefeArea", "Jefe de Área", NivelAlcance.Area, false, false, false, false,
            EsJefeDeArea: true, EsPmo: false, Color: null));

        var proyecto = Proyecto.Crear("PRY-2026-01", "Proyecto de prueba");
        proyecto.AreaId = "GOBDIGITAL";
        _ctx.Proyectos.Add(proyecto);
        await _ctx.SaveChangesAsync();

        await _sync.SincronizarProyectoAsync(proyecto.Id);

        var interesados = await _ctx.ProyectoInteresados.Where(i => i.ProyectoId == proyecto.Id).ToListAsync();
        interesados.Should().ContainSingle(i => i.UsuarioId == jefe.Id && i.Automatico && i.Rol == RolInteresado.Patrocinador);
    }

    [Fact]
    public async Task SincronizarProyecto_QuitaAlQueYaNoCalifica()
    {
        var exJefe = await SembrarUsuarioAsync("Ex Jefe");
        var proyecto = Proyecto.Crear("PRY-2026-02", "Proyecto de prueba");
        proyecto.AreaId = "GOBDIGITAL";
        _ctx.Proyectos.Add(proyecto);
        await _ctx.SaveChangesAsync();

        _ctx.ProyectoInteresados.Add(InteresadoProyecto.CrearAutomatico(
            proyecto.Id, exJefe.Id, exJefe.Nombre, RolInteresado.Patrocinador, null));
        await _ctx.SaveChangesAsync();

        // Sin AsignacionUsuario para exJefe en GOBDIGITAL: ya no califica.
        await _sync.SincronizarProyectoAsync(proyecto.Id);

        (await _ctx.ProyectoInteresados.AnyAsync(i => i.ProyectoId == proyecto.Id && i.UsuarioId == exJefe.Id))
            .Should().BeFalse();
    }

    [Fact]
    public async Task SincronizarProyecto_NoTocaUnInteresadoManualDelMismoUsuario()
    {
        var jefe = await SembrarUsuarioAsync("Jefe de Área");
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(jefe.Id, "DIGER", "GOBDIGITAL", null, "JefeArea"));
        _catalogo.Obtener("JefeArea").Returns(new RolInfo(
            "JefeArea", "Jefe de Área", NivelAlcance.Area, false, false, false, false,
            EsJefeDeArea: true, EsPmo: false, Color: null));

        var proyecto = Proyecto.Crear("PRY-2026-03", "Proyecto de prueba");
        proyecto.AreaId = "GOBDIGITAL";
        _ctx.Proyectos.Add(proyecto);
        await _ctx.SaveChangesAsync();

        var manual = InteresadoProyecto.Crear(proyecto.Id, jefe.Id, jefe.Nombre, RolInteresado.Ejecutor, "Alguien");
        _ctx.ProyectoInteresados.Add(manual);
        await _ctx.SaveChangesAsync();

        await _sync.SincronizarProyectoAsync(proyecto.Id);

        var fila = await _ctx.ProyectoInteresados.SingleAsync(i => i.ProyectoId == proyecto.Id && i.UsuarioId == jefe.Id);
        fila.Automatico.Should().BeFalse();
        fila.Rol.Should().Be(RolInteresado.Ejecutor);
    }

    [Fact]
    public async Task SincronizarUsuario_AgregaATodosLosProyectosDeSuArea()
    {
        var jefe = await SembrarUsuarioAsync("Jefe de Área");
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(jefe.Id, "DIGER", "SIGER", null, "JefeArea"));
        _catalogo.Obtener("JefeArea").Returns(new RolInfo(
            "JefeArea", "Jefe de Área", NivelAlcance.Area, false, false, false, false,
            EsJefeDeArea: true, EsPmo: false, Color: null));

        var p1 = Proyecto.Crear("PRY-2026-04", "Uno"); p1.AreaId = "SIGER";
        var p2 = Proyecto.Crear("PRY-2026-05", "Dos"); p2.AreaId = "SIGER";
        var p3 = Proyecto.Crear("PRY-2026-06", "Otra área"); p3.AreaId = "GOBDIGITAL";
        _ctx.Proyectos.AddRange(p1, p2, p3);
        await _ctx.SaveChangesAsync();

        await _sync.SincronizarUsuarioAsync(jefe.Id);

        var proyectosDelJefe = await _ctx.ProyectoInteresados
            .Where(i => i.UsuarioId == jefe.Id).Select(i => i.ProyectoId).ToListAsync();
        proyectosDelJefe.Should().BeEquivalentTo([p1.Id, p2.Id]);
    }

    public void Dispose() => _ctx.Dispose();
}
```

- [ ] **Step 2: Correr y confirmar que falla (no compila — el servicio no existe)**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~InteresadosAutomaticosSyncTests"`
Expected: error de compilación.

- [ ] **Step 3: Implementar el servicio**

Crear `src/Application/Proyectos/Services/InteresadosAutomaticosSync.cs`:

```csharp
namespace Diger.TramitesEstado.Application.Proyectos.Services;

/// <summary>
/// Mantiene sincronizadas las filas automáticas de InteresadoProyecto (ver
/// InteresadoProyecto.CrearAutomatico / Automatico) para las dos capacidades de rol que dan acceso
/// de oficio a un proyecto: EsJefeDeArea (por Proyecto.AreaId) y EsPmo (por Proyecto.UnidadId).
///
/// No toca filas manuales de otro usuario ni pisa una fila manual del mismo usuario — si alguien
/// ya figura como interesado (por el motivo que sea) su fila no se duplica ni se reemplaza.
/// </summary>
public interface IInteresadosAutomaticosSync
{
    /// <summary>Recalcula los interesados automáticos de UN proyecto. Llamar al crearlo o al
    /// cambiarle AreaId/UnidadId.</summary>
    Task SincronizarProyectoAsync(int proyectoId, CancellationToken ct = default);

    /// <summary>Recalcula los proyectos donde UN usuario debe figurar como interesado automático.
    /// Llamar cuando cambia su rol o su área/unidad asignada.</summary>
    Task SincronizarUsuarioAsync(Guid usuarioId, CancellationToken ct = default);
}

public sealed class InteresadosAutomaticosSyncService(IApplicationDbContext ctx, IRolCatalogo catalogo)
    : IInteresadosAutomaticosSync
{
    public async Task SincronizarProyectoAsync(int proyectoId, CancellationToken ct = default)
    {
        var proyecto = await ctx.Proyectos.FirstOrDefaultAsync(p => p.Id == proyectoId, ct);
        if (proyecto is null) return;

        var deseados = await CalcularDeseadosPorProyectoAsync(proyecto.AreaId, proyecto.UnidadId, ct);
        var actuales = await ctx.ProyectoInteresados
            .Where(i => i.ProyectoId == proyectoId)
            .ToListAsync(ct);

        await AplicarAsync(proyectoId, deseados, actuales, ct);
    }

    public async Task SincronizarUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var asignaciones = await ctx.AsignacionesUsuario
            .Where(a => a.UsuarioId == usuarioId)
            .ToListAsync(ct);

        var rolInfo = asignaciones.Count > 0 ? catalogo.Obtener(asignaciones[0].Rol) : null;
        var deseados = new Dictionary<int, RolInteresado>();

        if (rolInfo?.EsJefeDeArea == true)
        {
            var areas = asignaciones.Where(a => a.AreaId != null).Select(a => a.AreaId!).Distinct().ToList();
            if (areas.Count > 0)
            {
                var ids = await ctx.Proyectos
                    .Where(p => p.AreaId != null && areas.Contains(p.AreaId))
                    .Select(p => p.Id).ToListAsync(ct);
                foreach (var id in ids) deseados[id] = RolInteresado.Patrocinador;
            }
        }

        if (rolInfo?.EsPmo == true)
        {
            var unidades = asignaciones.Where(a => a.UnidadId != null).Select(a => a.UnidadId!).Distinct().ToList();
            if (unidades.Count > 0)
            {
                var ids = await ctx.Proyectos
                    .Where(p => p.UnidadId != null && unidades.Contains(p.UnidadId))
                    .Select(p => p.Id).ToListAsync(ct);
                foreach (var id in ids) deseados[id] = RolInteresado.Ejecutor;
            }
        }

        var todosLosActuales = await ctx.ProyectoInteresados
            .Where(i => i.UsuarioId == usuarioId)
            .ToListAsync(ct);

        foreach (var fila in todosLosActuales)
            if (fila.Automatico && !deseados.ContainsKey(fila.ProyectoId))
                ctx.ProyectoInteresados.Remove(fila);

        var yaFiguraEn = todosLosActuales.Select(a => a.ProyectoId).ToHashSet();

        var usuario = await ctx.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct);
        if (usuario is not null && usuario.Activo)
        {
            foreach (var (proyectoId, rol) in deseados)
            {
                if (yaFiguraEn.Contains(proyectoId)) continue;
                ctx.ProyectoInteresados.Add(InteresadoProyecto.CrearAutomatico(
                    proyectoId, usuario.Id, usuario.Nombre, rol, usuario.Correo));
            }
        }

        await ctx.SaveChangesAsync(ct);
    }

    private async Task<Dictionary<Guid, RolInteresado>> CalcularDeseadosPorProyectoAsync(
        string? areaId, string? unidadId, CancellationToken ct)
    {
        var resultado = new Dictionary<Guid, RolInteresado>();

        if (!string.IsNullOrWhiteSpace(areaId))
        {
            var asignados = await ctx.AsignacionesUsuario
                .Where(a => a.AreaId == areaId)
                .Select(a => new { a.UsuarioId, a.Rol })
                .Distinct()
                .ToListAsync(ct);
            foreach (var a in asignados)
                if (catalogo.Obtener(a.Rol)?.EsJefeDeArea == true)
                    resultado[a.UsuarioId] = RolInteresado.Patrocinador;
        }

        if (!string.IsNullOrWhiteSpace(unidadId))
        {
            var asignados = await ctx.AsignacionesUsuario
                .Where(a => a.UnidadId == unidadId)
                .Select(a => new { a.UsuarioId, a.Rol })
                .Distinct()
                .ToListAsync(ct);
            foreach (var a in asignados)
                if (catalogo.Obtener(a.Rol)?.EsPmo == true)
                    resultado[a.UsuarioId] = RolInteresado.Ejecutor;
        }

        return resultado;
    }

    private async Task AplicarAsync(
        int proyectoId, Dictionary<Guid, RolInteresado> deseados,
        List<InteresadoProyecto> actuales, CancellationToken ct)
    {
        foreach (var fila in actuales)
            if (fila.Automatico && !deseados.ContainsKey(fila.UsuarioId))
                ctx.ProyectoInteresados.Remove(fila);

        var yaFiguran = actuales.Select(a => a.UsuarioId).ToHashSet();

        foreach (var (usuarioId, rol) in deseados)
        {
            if (yaFiguran.Contains(usuarioId)) continue;
            var usuario = await ctx.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct);
            if (usuario is null || !usuario.Activo) continue;
            ctx.ProyectoInteresados.Add(InteresadoProyecto.CrearAutomatico(
                proyectoId, usuario.Id, usuario.Nombre, rol, usuario.Correo));
        }

        await ctx.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Registrar el servicio en DI**

En `src/Application/DependencyInjection.cs`, junto a `services.AddMemoryCache();`, agregar:

```csharp
        services.AddScoped<IInteresadosAutomaticosSync, InteresadosAutomaticosSyncService>();
```

(agregar `using Diger.TramitesEstado.Application.Proyectos.Services;` si el archivo no usa `global using` de todo `Application`.)

- [ ] **Step 5: Correr los tests y confirmar que pasan**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~InteresadosAutomaticosSyncTests"`
Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add src/Application/Proyectos/Services/InteresadosAutomaticosSync.cs src/Application/DependencyInjection.cs tests/Application.Tests/Proyectos/InteresadosAutomaticosSyncTests.cs
git commit -m "feat: servicio de sincronización de interesados automáticos (EsJefeDeArea/EsPmo)"
```

---

## Task 5: Disparar la sincronización desde creación/edición de proyecto y cambio de asignación

**Files:**
- Modify: `src/Application/Proyectos/Commands/ProyectoCommands.cs` (`CrearProyectoCommandHandler`, `ActualizarProyectoCommandHandler`)
- Modify: `src/Application/Usuarios/Commands/AsignarInstitucionesUsuario/AsignarInstitucionesUsuarioCommand.cs`
- Test: `tests/Application.Tests/Proyectos/ProyectoCommandsSyncTests.cs`

**Interfaces:**
- Consumes: `IInteresadosAutomaticosSync` (Task 4).

- [ ] **Step 1: Escribir el test que falla — crear un proyecto con AreaId dispara la sincronización**

Crear `tests/Application.Tests/Proyectos/ProyectoCommandsSyncTests.cs`:

```csharp
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Services;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Proyectos;

public class ProyectoCommandsSyncTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly IInteresadosAutomaticosSync _sync = Substitute.For<IInteresadosAutomaticosSync>();

    public ProyectoCommandsSyncTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.EsGlobal.Returns(true);
        currentUser.ActiveInstitucionId.Returns("DIGER");
        _ctx = new AppDbContext(opts, currentUser, Substitute.For<MediatR.IPublisher>());
    }

    [Fact]
    public async Task CrearProyecto_ConArea_DisparaLaSincronizacion()
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.ActiveInstitucionId.Returns("DIGER");
        var handler = new CrearProyectoCommandHandler(_ctx, currentUser, _sync);

        var id = await handler.Handle(
            new CrearProyectoCommand("Proyecto de prueba", AreaId: "GOBDIGITAL"),
            CancellationToken.None);

        await _sync.Received(1).SincronizarProyectoAsync(id, Arg.Any<CancellationToken>());
    }

    public void Dispose() => _ctx.Dispose();
}
```

- [ ] **Step 2: Correr y confirmar que falla (no compila — `CrearProyectoCommandHandler` no toma `IInteresadosAutomaticosSync`)**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~ProyectoCommandsSyncTests"`
Expected: error de compilación.

- [ ] **Step 3: Inyectar el sync en `CrearProyectoCommandHandler` y `ActualizarProyectoCommandHandler`**

En `src/Application/Proyectos/Commands/ProyectoCommands.cs`, cambiar la firma del constructor primario de `CrearProyectoCommandHandler` (línea 71-74):

```csharp
public sealed class CrearProyectoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser,
    IInteresadosAutomaticosSync sync)
    : IRequestHandler<CrearProyectoCommand, int>
```

Y al final de `Handle`, después de `await ctx.SaveChangesAsync(ct);` y antes de `return proyecto.Id;` (línea 96):

```csharp
        ctx.Proyectos.Add(proyecto);
        await ctx.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(proyecto.AreaId) || !string.IsNullOrWhiteSpace(proyecto.UnidadId))
            await sync.SincronizarProyectoAsync(proyecto.Id, ct);

        return proyecto.Id;
```

En `ActualizarProyectoCommandHandler`, agregar el mismo parámetro al constructor primario y, al final de `Handle` (después del `SaveChangesAsync` que persiste los cambios de la ficha), agregar:

```csharp
        if (huboCambioDeAreaOUnidad)
            await sync.SincronizarProyectoAsync(cmd.Id, ct);
```

donde `huboCambioDeAreaOUnidad` se calcula ANTES de mutar `proyecto.AreaId`/`proyecto.UnidadId` (líneas 159-160 según el reporte de exploración), comparando el valor previo contra `cmd.AreaId`/`cmd.UnidadId`:

```csharp
        var areaOUnidadCambio = proyecto.AreaId != (string.IsNullOrWhiteSpace(cmd.AreaId) ? null : cmd.AreaId.Trim())
                              || proyecto.UnidadId != (string.IsNullOrWhiteSpace(cmd.UnidadId) ? null : cmd.UnidadId.Trim());

        proyecto.AreaId          = string.IsNullOrWhiteSpace(cmd.AreaId) ? null : cmd.AreaId.Trim();
        proyecto.UnidadId        = string.IsNullOrWhiteSpace(cmd.UnidadId) ? null : cmd.UnidadId.Trim();
```

y usar `areaOUnidadCambio` en el `if` de más abajo, después del `SaveChangesAsync` final del handler.

- [ ] **Step 4: Disparar la sincronización por usuario tras reemplazar asignaciones**

En `src/Application/Usuarios/Commands/AsignarInstitucionesUsuario/AsignarInstitucionesUsuarioCommand.cs`, agregar `IInteresadosAutomaticosSync sync` al constructor primario de `AsignarJerarquiaUsuarioCommandHandler`:

```csharp
public sealed class AsignarJerarquiaUsuarioCommandHandler(
    IUsuarioRepository repo, IUnitOfWork uow, IApplicationDbContext ctx, IRolCatalogo catalogo,
    IInteresadosAutomaticosSync sync)
    : IRequestHandler<AsignarJerarquiaUsuarioCommand, Unit>
```

Y al final de `Handle`, después de `await uow.SaveChangesAsync(ct);` y antes de `return Unit.Value;`:

```csharp
        await repo.ReemplazarAsignacionesAsync(cmd.UsuarioId, cmd.Rol, cmd.Asignaciones ?? [], ct);
        await uow.SaveChangesAsync(ct);

        await sync.SincronizarUsuarioAsync(cmd.UsuarioId, ct);

        return Unit.Value;
```

- [ ] **Step 5: Actualizar los call-sites de `CrearProyectoCommandHandler`/`ActualizarProyectoCommandHandler`/`AsignarJerarquiaUsuarioCommandHandler` en tests existentes**

MediatR resuelve el handler por DI en producción (Web/Presentation), así que no hace falta tocar esos hosts. Pero **cualquier test existente que construya estos handlers directamente** (`new CrearProyectoCommandHandler(ctx, currentUser)`, etc., sin el tercer/cuarto parámetro) va a dejar de compilar. Buscar y corregir:

Run: `grep -rn "new CrearProyectoCommandHandler(\|new ActualizarProyectoCommandHandler(\|new AsignarJerarquiaUsuarioCommandHandler(" tests\`

Para cada resultado, agregar `Substitute.For<IInteresadosAutomaticosSync>()` como argumento adicional en la posición correspondiente.

- [ ] **Step 6: Correr los tests y confirmar que pasan**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~ProyectoCommandsSyncTests"`
Expected: PASS (1 test).

Run: `dotnet test tests\Application.Tests`
Expected: PASS — ningún test roto por el cambio de firma en los tres handlers.

- [ ] **Step 7: Commit**

```bash
git add src/Application/Proyectos/Commands/ProyectoCommands.cs src/Application/Usuarios/Commands/AsignarInstitucionesUsuario/AsignarInstitucionesUsuarioCommand.cs tests/Application.Tests/Proyectos/ProyectoCommandsSyncTests.cs
git commit -m "feat: dispara la sincronización de interesados automáticos al crear/editar proyectos y al reasignar usuarios"
```

---

## Task 6: `GetMisProyectosDashboardQuery` — consulta compartida por las vistas Unidad y Área

**Files:**
- Create: `src/Application/Dashboards/Queries/GetMisProyectosDashboard/GetMisProyectosDashboardQuery.cs`
- Test: `tests/Application.Tests/Dashboards/GetMisProyectosDashboardQueryTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public sealed record MisProyectosItemDto(
      int ProyectoId, string Codigo, string Nombre, string? UnidadId, string? UnidadNombre,
      EstadoProyecto Estado, int AvancePct, DateOnly? FechaFinPlan, bool Atrasado, bool SinReportar);
  public sealed record MisProyectosDashboardDto(
      int TotalProyectos, int AvancePromedio, int Atrasados, int SinReportar30,
      IReadOnlyList<MisProyectosItemDto> Proyectos);
  public sealed record GetMisProyectosDashboardQuery : IRequest<MisProyectosDashboardDto>;
  ```
  Consumido por las páginas de Task 8 y Task 9.

- [ ] **Step 1: Escribir el test que falla**

Crear `tests/Application.Tests/Dashboards/GetMisProyectosDashboardQueryTests.cs`:

```csharp
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Dashboards.Queries.GetMisProyectosDashboard;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Dashboards;

public class GetMisProyectosDashboardQueryTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly Guid _usuarioId = Guid.NewGuid();

    public GetMisProyectosDashboardQueryTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.EsGlobal.Returns(true);
        currentUser.UserId.Returns(_usuarioId);
        _ctx = new AppDbContext(opts, currentUser, Substitute.For<MediatR.IPublisher>());
    }

    [Fact]
    public async Task SoloTraeProyectosDondeElUsuarioEsInteresadoOResponsable()
    {
        var mio = Proyecto.Crear("PRY-2026-10", "Mío");
        var ajeno = Proyecto.Crear("PRY-2026-11", "Ajeno");
        _ctx.Proyectos.AddRange(mio, ajeno);
        await _ctx.SaveChangesAsync();

        _ctx.ProyectoInteresados.Add(InteresadoProyecto.Crear(
            mio.Id, _usuarioId, "Yo", RolInteresado.Ejecutor, "Prueba"));
        await _ctx.SaveChangesAsync();

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(_usuarioId);
        var handler = new GetMisProyectosDashboardQueryHandler(_ctx, currentUser);

        var resultado = await handler.Handle(new GetMisProyectosDashboardQuery(), CancellationToken.None);

        resultado.TotalProyectos.Should().Be(1);
        resultado.Proyectos.Single().Codigo.Should().Be("PRY-2026-10");
    }

    public void Dispose() => _ctx.Dispose();
}
```

- [ ] **Step 2: Correr y confirmar que falla (no compila — la consulta no existe)**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~GetMisProyectosDashboardQueryTests"`
Expected: error de compilación.

- [ ] **Step 3: Implementar la consulta**

Crear `src/Application/Dashboards/Queries/GetMisProyectosDashboard/GetMisProyectosDashboardQuery.cs`:

```csharp
namespace Diger.TramitesEstado.Application.Dashboards.Queries.GetMisProyectosDashboard;

public sealed record MisProyectosItemDto(
    int ProyectoId, string Codigo, string Nombre, string? UnidadId, string? UnidadNombre,
    EstadoProyecto Estado, int AvancePct, DateOnly? FechaFinPlan, bool Atrasado, bool SinReportar);

public sealed record MisProyectosDashboardDto(
    int TotalProyectos, int AvancePromedio, int Atrasados, int SinReportar30,
    IReadOnlyList<MisProyectosItemDto> Proyectos);

/// <summary>Proyectos donde la persona que consulta es interesado o responsable — la vista
/// «Unidad» tal cual, y la base de la vista «Área» (que además agrupa por UnidadNombre): un jefe
/// de área ve aquí todo su portafolio porque la sincronización automática (ver
/// IInteresadosAutomaticosSync) ya lo dejó como interesado de cada proyecto de su área.</summary>
public sealed record GetMisProyectosDashboardQuery : IRequest<MisProyectosDashboardDto>;

public sealed class GetMisProyectosDashboardQueryHandler(IApplicationDbContext ctx, ICurrentUserService currentUser)
    : IRequestHandler<GetMisProyectosDashboardQuery, MisProyectosDashboardDto>
{
    private const int DiasSinReporte = 30;

    public async Task<MisProyectosDashboardDto> Handle(GetMisProyectosDashboardQuery q, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        if (userId is null) return new MisProyectosDashboardDto(0, 0, 0, 0, []);

        var proyectos = await ctx.Proyectos.AsNoTracking()
            .Where(p => p.ResponsableId == userId
                     || ctx.ProyectoInteresados.Any(i => i.ProyectoId == p.Id && i.UsuarioId == userId))
            .Select(p => new
            {
                p.Id, p.Codigo, p.Nombre, p.UnidadId, p.Estado, p.AvancePct, p.FechaFinPlan,
                UltimoAvance = ctx.ProyectoAvances
                    .Where(a => a.ProyectoId == p.Id)
                    .OrderByDescending(a => a.Fecha)
                    .Select(a => (DateTime?)a.Fecha)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var unidadIds = proyectos.Where(p => p.UnidadId != null).Select(p => p.UnidadId!).Distinct().ToList();
        var nombresUnidad = await ctx.Unidades.AsNoTracking()
            .Where(u => unidadIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Nombre, ct);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var corte = DateTime.UtcNow.AddDays(-DiasSinReporte);

        var items = proyectos.Select(p =>
        {
            var abierto = p.Estado is EstadoProyecto.Planificado or EstadoProyecto.EnEjecucion or EstadoProyecto.Suspendido;
            var atrasado = p.FechaFinPlan is { } fin && fin < hoy && abierto;
            var sinReportar = p.Estado == EstadoProyecto.EnEjecucion && (p.UltimoAvance is null || p.UltimoAvance < corte);
            return new MisProyectosItemDto(
                p.Id, p.Codigo, p.Nombre, p.UnidadId,
                p.UnidadId != null && nombresUnidad.TryGetValue(p.UnidadId, out var n) ? n : null,
                p.Estado, p.AvancePct, p.FechaFinPlan, atrasado, sinReportar);
        }).OrderByDescending(i => i.Atrasado).ThenBy(i => i.FechaFinPlan).ToList();

        var enEjecucion = items.Where(i => i.Estado == EstadoProyecto.EnEjecucion).ToList();

        return new MisProyectosDashboardDto(
            items.Count,
            enEjecucion.Count == 0 ? 0 : (int)Math.Round(enEjecucion.Average(i => i.AvancePct)),
            items.Count(i => i.Atrasado),
            items.Count(i => i.SinReportar),
            items);
    }
}
```

- [ ] **Step 4: Correr los tests y confirmar que pasan**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~GetMisProyectosDashboardQueryTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Application/Dashboards/Queries/GetMisProyectosDashboard/ tests/Application.Tests/Dashboards/GetMisProyectosDashboardQueryTests.cs
git commit -m "feat: consulta GetMisProyectosDashboardQuery (base de las vistas Unidad y Área)"
```

---

## Task 7: Filtro de área en el tablero de Institución

**Files:**
- Modify: `src/Application/Dashboards/Queries/GetProyectosDashboardQuery.cs`
- Test: `tests/Application.Tests/Dashboards/GetProyectosDashboardQueryAreaTests.cs`

**Interfaces:**
- Produces: `GetProyectosDashboardQuery` gana un parámetro `IReadOnlyList<string>? AreaIds = null`.

- [ ] **Step 1: Escribir el test que falla**

Crear `tests/Application.Tests/Dashboards/GetProyectosDashboardQueryAreaTests.cs`:

```csharp
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Dashboards.Queries;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Dashboards;

public class GetProyectosDashboardQueryAreaTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public GetProyectosDashboardQueryAreaTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.EsGlobal.Returns(true);
        _ctx = new AppDbContext(opts, currentUser, Substitute.For<MediatR.IPublisher>());
    }

    [Fact]
    public async Task FiltraPorUnaOVariasAreas()
    {
        var siger = Proyecto.Crear("PRY-2026-20", "SIGER"); siger.AreaId = "SIGER";
        var gobdigital = Proyecto.Crear("PRY-2026-21", "GobDigital"); gobdigital.AreaId = "GOBDIGITAL";
        var otra = Proyecto.Crear("PRY-2026-22", "Otra"); otra.AreaId = "RRHH";
        _ctx.Proyectos.AddRange(siger, gobdigital, otra);
        await _ctx.SaveChangesAsync();

        var handler = new GetProyectosDashboardQueryHandler(_ctx);

        var resultado = await handler.Handle(
            new GetProyectosDashboardQuery(AreaIds: ["SIGER", "GOBDIGITAL"]), CancellationToken.None);

        resultado.Semaforo.Select(s => s.Codigo).Should().BeEquivalentTo("PRY-2026-20", "PRY-2026-21");
    }

    public void Dispose() => _ctx.Dispose();
}
```

- [ ] **Step 2: Correr y confirmar que falla (no compila — falta el parámetro `AreaIds`)**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~GetProyectosDashboardQueryAreaTests"`
Expected: error de compilación.

- [ ] **Step 3: Agregar el filtro**

En `src/Application/Dashboards/Queries/GetProyectosDashboardQuery.cs`, el record de la consulta (líneas 15-18 según el reporte de exploración) queda:

```csharp
public sealed record GetProyectosDashboardQuery(
    EstadoProyecto? Estado = null, Guid? ResponsableId = null, PrioridadProyecto? Prioridad = null,
    IReadOnlyList<string>? AreaIds = null) : IRequest<ProyectosDashboardDto>;
```

Y en `Handle`, junto a los filtros existentes sobre `baseQuery` (mismo patrón `if (q.Estado is { } e) baseQuery = baseQuery.Where(...)`, líneas 37-39), agregar:

```csharp
        if (q.AreaIds is { Count: > 0 } areas)
            baseQuery = baseQuery.Where(p => p.AreaId != null && areas.Contains(p.AreaId));
```

- [ ] **Step 4: Correr los tests y confirmar que pasan**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~GetProyectosDashboardQueryAreaTests"`
Expected: PASS.

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~GetProyectosDashboardQuery"`
Expected: PASS — el parámetro nuevo con default `null` no rompe los tests existentes de esta consulta.

- [ ] **Step 5: Commit**

```bash
git add src/Application/Dashboards/Queries/GetProyectosDashboardQuery.cs tests/Application.Tests/Dashboards/GetProyectosDashboardQueryAreaTests.cs
git commit -m "feat: filtro de una o varias áreas en el tablero de Proyectos (nivel Institución)"
```

---

## Task 8: Página `/Tableros/ProyectosUnidad`

**Files:**
- Create: `src/Web/Pages/Tableros/ProyectosUnidad.cshtml`
- Create: `src/Web/Pages/Tableros/ProyectosUnidad.cshtml.cs`
- Create: `src/Web/Pages/Tableros/_TabsProyectos.cshtml`

**Interfaces:**
- Consumes: `GetMisProyectosDashboardQuery` (Task 6).

- [ ] **Step 1: Crear el partial de pestañas (compartido por las 3 vistas)**

Crear `src/Web/Pages/Tableros/_TabsProyectos.cshtml`:

```html
@using Diger.TramitesEstado.Web.Common
@inject ICurrentUserService CurrentUser
@{
    var activa = ViewData["TabActiva"] as string ?? "Unidad";
    var puedeInstitucion = CurrentUser.EsGlobal
        || CurrentUser.NivelAlcance is NivelAlcance.Institucion or NivelAlcance.Global;
}
<div class="seg-filters" style="margin-bottom:12px">
    <a class="btns @(activa == "Unidad" ? "on" : "")" asp-page="/Tableros/ProyectosUnidad">Mi unidad</a>
    @if (CurrentUser.EsJefeDeArea)
    {
        <a class="btns @(activa == "Area" ? "on" : "")" asp-page="/Tableros/ProyectosArea">Mi área</a>
    }
    @if (puedeInstitucion)
    {
        <a class="btns @(activa == "Institucion" ? "on" : "")" asp-page="/Tableros/Proyectos">Institución</a>
    }
</div>
```

- [ ] **Step 2: Crear el PageModel**

Crear `src/Web/Pages/Tableros/ProyectosUnidad.cshtml.cs`:

```csharp
using Diger.TramitesEstado.Application.Dashboards.Queries.GetMisProyectosDashboard;

namespace Diger.TramitesEstado.Web.Pages.Tableros;

[Authorize]
[Permission("Proyectos", AccionModulo.Ver, "Ver proyectos")]
public sealed class ProyectosUnidadModel(ISender sender) : PageModel
{
    public MisProyectosDashboardDto Data { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken ct)
    {
        Data = await sender.Send(new GetMisProyectosDashboardQuery(), ct);
    }
}
```

- [ ] **Step 3: Crear la vista**

Crear `src/Web/Pages/Tableros/ProyectosUnidad.cshtml`:

```html
@page
@model Diger.TramitesEstado.Web.Pages.Tableros.ProyectosUnidadModel
@{
    ViewData["Title"] = "Mis proyectos";
    ViewData["TabActiva"] = "Unidad";
    var d = Model.Data;
    string Fecha(DateOnly? f) => f?.ToString("dd/MM/yyyy") ?? "—";
}

<div class="container" style="max-width:1180px">
    <div class="hist-header">
        <div>
            <h2>Mis proyectos</h2>
            <p>Proyectos donde eres interesado o responsable.</p>
        </div>
        <a class="btns" asp-page="/Tableros/Index">← Tableros</a>
    </div>

    <partial name="_TabsProyectos" />

    <div class="kpi-grid">
        <div class="kpi-card"><div class="kpi-num">@d.TotalProyectos</div><div class="kpi-lbl">Proyectos</div></div>
        <div class="kpi-card"><div class="kpi-num">@d.AvancePromedio%</div><div class="kpi-lbl">Avance promedio</div></div>
        <div class="kpi-card @(d.Atrasados > 0 ? "alert" : "ok")"><div class="kpi-num">@d.Atrasados</div><div class="kpi-lbl">Atrasados</div></div>
        <div class="kpi-card @(d.SinReportar30 > 0 ? "warn" : "ok")"><div class="kpi-num">@d.SinReportar30</div><div class="kpi-lbl">Sin reportar 30+ días</div></div>
    </div>

    <div class="dash-card" style="margin-top:16px">
        <h3>Proyectos</h3>
        @if (!d.Proyectos.Any())
        {
            <p class="res-meta">No hay proyectos donde figures como interesado o responsable.</p>
        }
        else
        {
            <table class="seg-table">
                <thead>
                    <tr>
                        <th>Proyecto</th>
                        <th style="width:150px">Unidad</th>
                        <th style="width:110px">Estado</th>
                        <th style="width:150px">Avance</th>
                        <th style="width:120px">Cierre plan.</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var p in d.Proyectos)
                    {
                        <tr>
                            <td><a asp-page="/Proyectos/Editor" asp-route-id="@p.ProyectoId" style="font-weight:600;color:var(--diger-blue-title);text-decoration:none">@p.Nombre</a>
                                <div class="res-meta">@p.Codigo</div></td>
                            <td class="res-meta">@(p.UnidadNombre ?? "—")</td>
                            <td class="res-meta">@p.Estado</td>
                            <td>
                                <div class="bar-track" style="height:12px"><div class="bar-fill @(p.Atrasado ? "c-red" : p.SinReportar ? "c-amber" : "")" style="width:@(p.AvancePct)%"></div></div>
                                <div class="res-meta">@(p.AvancePct)%</div>
                            </td>
                            <td class="res-meta">
                                @Fecha(p.FechaFinPlan)
                                @if (p.Atrasado) { <div><span class="badge badge-danger">Vencido</span></div> }
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
        }
    </div>
</div>
```

- [ ] **Step 4: Verificar en el navegador**

Iniciar el servidor de desarrollo (`preview_start` con el nombre configurado en `.claude/launch.json`, o `dotnet run --project src\Web`), autenticarse como un usuario con `Proyectos.Ver`, navegar a `/Tableros/ProyectosUnidad` y confirmar: los 4 KPI se ven, la tabla lista los proyectos correctos (o el mensaje vacío si no hay ninguno), y la pestaña "Mi unidad" aparece marcada como activa.

- [ ] **Step 5: Commit**

```bash
git add src/Web/Pages/Tableros/ProyectosUnidad.cshtml src/Web/Pages/Tableros/ProyectosUnidad.cshtml.cs src/Web/Pages/Tableros/_TabsProyectos.cshtml
git commit -m "feat: página /Tableros/ProyectosUnidad"
```

---

## Task 9: Página `/Tableros/ProyectosArea`

**Files:**
- Create: `src/Web/Pages/Tableros/ProyectosArea.cshtml`
- Create: `src/Web/Pages/Tableros/ProyectosArea.cshtml.cs`

**Interfaces:**
- Consumes: `GetMisProyectosDashboardQuery` (Task 6, mismos datos que Task 8 — la diferencia es de presentación: agrupado por unidad).

- [ ] **Step 1: Crear el PageModel**

Crear `src/Web/Pages/Tableros/ProyectosArea.cshtml.cs`:

```csharp
using Diger.TramitesEstado.Application.Dashboards.Queries.GetMisProyectosDashboard;

namespace Diger.TramitesEstado.Web.Pages.Tableros;

[Authorize]
[Permission("Proyectos", AccionModulo.Ver, "Ver proyectos")]
public sealed class ProyectosAreaModel(ISender sender) : PageModel
{
    public MisProyectosDashboardDto Data { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken ct)
    {
        Data = await sender.Send(new GetMisProyectosDashboardQuery(), ct);
    }
}
```

- [ ] **Step 2: Crear la vista, con el desglose por unidad que distingue esta vista de la de Unidad**

Crear `src/Web/Pages/Tableros/ProyectosArea.cshtml`:

```html
@page
@using System.Text.Json
@model Diger.TramitesEstado.Web.Pages.Tableros.ProyectosAreaModel
@{
    ViewData["Title"] = "Proyectos de mi área";
    ViewData["TabActiva"] = "Area";
    var d = Model.Data;
    var J = (object o) => Html.Raw(JsonSerializer.Serialize(o));
    var porUnidad = d.Proyectos
        .GroupBy(p => p.UnidadNombre ?? "Sin unidad")
        .Select(g => new { Unidad = g.Key, Cantidad = g.Count(), Avance = g.Count() == 0 ? 0 : (int)Math.Round(g.Average(x => x.AvancePct)) })
        .OrderByDescending(x => x.Cantidad)
        .ToList();
    string Fecha(DateOnly? f) => f?.ToString("dd/MM/yyyy") ?? "—";
}

<div class="container" style="max-width:1180px">
    <div class="hist-header">
        <div>
            <h2>Proyectos de mi área</h2>
            <p>Todas las unidades de tu área, en un solo lugar.</p>
        </div>
        <a class="btns" asp-page="/Tableros/Index">← Tableros</a>
    </div>

    <partial name="_TabsProyectos" />

    <div class="kpi-grid">
        <div class="kpi-card"><div class="kpi-num">@d.TotalProyectos</div><div class="kpi-lbl">Proyectos</div></div>
        <div class="kpi-card"><div class="kpi-num">@d.AvancePromedio%</div><div class="kpi-lbl">Avance promedio</div></div>
        <div class="kpi-card @(d.Atrasados > 0 ? "alert" : "ok")"><div class="kpi-num">@d.Atrasados</div><div class="kpi-lbl">Atrasados</div></div>
        <div class="kpi-card @(d.SinReportar30 > 0 ? "warn" : "ok")"><div class="kpi-num">@d.SinReportar30</div><div class="kpi-lbl">Sin reportar 30+ días</div></div>
    </div>

    <div class="dash-card" style="margin-top:16px">
        <h3>Avance por unidad</h3>
        @if (!porUnidad.Any())
        {
            <p class="res-meta">Sin datos.</p>
        }
        else
        {
            @foreach (var u in porUnidad)
            {
                <div class="bar-row">
                    <span class="bar-lbl">@u.Unidad (@u.Cantidad)</span>
                    <div class="bar-track"><div class="bar-fill" style="width:@(u.Avance)%"></div></div>
                    <span class="bar-val">@(u.Avance)%</span>
                </div>
            }
        }
    </div>

    <div class="dash-card" style="margin-top:16px">
        <h3>Proyectos</h3>
        @if (!d.Proyectos.Any())
        {
            <p class="res-meta">No hay proyectos en tu área.</p>
        }
        else
        {
            <table class="seg-table">
                <thead>
                    <tr>
                        <th>Proyecto</th>
                        <th style="width:150px">Unidad</th>
                        <th style="width:110px">Estado</th>
                        <th style="width:150px">Avance</th>
                        <th style="width:120px">Cierre plan.</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var p in d.Proyectos)
                    {
                        <tr>
                            <td><a asp-page="/Proyectos/Editor" asp-route-id="@p.ProyectoId" style="font-weight:600;color:var(--diger-blue-title);text-decoration:none">@p.Nombre</a>
                                <div class="res-meta">@p.Codigo</div></td>
                            <td class="res-meta">@(p.UnidadNombre ?? "—")</td>
                            <td class="res-meta">@p.Estado</td>
                            <td>
                                <div class="bar-track" style="height:12px"><div class="bar-fill @(p.Atrasado ? "c-red" : p.SinReportar ? "c-amber" : "")" style="width:@(p.AvancePct)%"></div></div>
                                <div class="res-meta">@(p.AvancePct)%</div>
                            </td>
                            <td class="res-meta">
                                @Fecha(p.FechaFinPlan)
                                @if (p.Atrasado) { <div><span class="badge badge-danger">Vencido</span></div> }
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
        }
    </div>
</div>
```

(El desglose por unidad se calcula en la propia vista sobre `d.Proyectos` — ya viene con `UnidadNombre` resuelto desde `GetMisProyectosDashboardQuery` — sin necesitar una consulta nueva ni JavaScript: es una lista de barras, no un `<canvas>`, así que no hace falta Chart.js aquí.)

- [ ] **Step 3: Verificar en el navegador**

Con un usuario cuyo rol tenga `EsJefeDeArea` (asignarlo desde `/Accesos/Roles` a un rol de prueba y esa asignación al usuario de prueba desde `/Usuarios`), navegar a `/Tableros/ProyectosArea` y confirmar que aparecen todos los proyectos de las unidades del área, agrupados correctamente en "Avance por unidad".

- [ ] **Step 4: Commit**

```bash
git add src/Web/Pages/Tableros/ProyectosArea.cshtml src/Web/Pages/Tableros/ProyectosArea.cshtml.cs
git commit -m "feat: página /Tableros/ProyectosArea"
```

---

## Task 10: Filtro de área + pestañas + redirección por defecto en `/Tableros/Proyectos`

**Files:**
- Modify: `src/Web/Pages/Tableros/Proyectos.cshtml.cs`
- Modify: `src/Web/Pages/Tableros/Proyectos.cshtml`

**Interfaces:**
- Consumes: `GetProyectosDashboardQuery` con `AreaIds` (Task 7), `GetAreasQuery` (ya existente y corregido antes en este repo — ver `src/Application/Areas/Queries/GetAreasQuery.cs`), `ICurrentUserService.EsJefeDeArea`/`NivelAlcance` (Task 1), `_TabsProyectos.cshtml` (Task 8).

- [ ] **Step 1: Cambiar el PageModel — redirección por defecto y filtro de área**

En `src/Web/Pages/Tableros/Proyectos.cshtml.cs`, reemplazar el contenido completo por:

```csharp
using Diger.TramitesEstado.Application.Areas.Queries;
using Diger.TramitesEstado.Application.Dashboards.Queries;
using Diger.TramitesEstado.Application.Tickets.Common;
using Diger.TramitesEstado.Application.Tickets.Queries.GetUsuariosAsignables;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.Tableros;

[Authorize]
[Permission("Proyectos", AccionModulo.Ver, "Ver proyectos")]
public sealed class ProyectosModel(ISender sender, ICurrentUserService currentUser) : PageModel
{
    public ProyectosDashboardDto Data { get; private set; } = default!;
    public IReadOnlyList<UsuarioAsignableDto> Usuarios { get; private set; } = [];
    public IReadOnlyList<AreaListItemDto> Areas { get; private set; } = [];

    [BindProperty(SupportsGet = true)] public EstadoProyecto?    Estado        { get; set; }
    [BindProperty(SupportsGet = true)] public Guid?              ResponsableId { get; set; }
    [BindProperty(SupportsGet = true)] public PrioridadProyecto? Prioridad     { get; set; }
    [BindProperty(SupportsGet = true)] public string[]?          AreaIds       { get; set; }

    public bool HayFiltro => Estado is not null || ResponsableId is not null || Prioridad is not null
                           || (AreaIds?.Length ?? 0) > 0;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var puedeInstitucion = currentUser.EsGlobal
            || currentUser.NivelAlcance is NivelAlcance.Institucion or NivelAlcance.Global;

        if (!puedeInstitucion)
        {
            return currentUser.EsJefeDeArea
                ? RedirectToPage("/Tableros/ProyectosArea")
                : RedirectToPage("/Tableros/ProyectosUnidad");
        }

        Data     = await sender.Send(new GetProyectosDashboardQuery(Estado, ResponsableId, Prioridad, AreaIds), ct);
        Usuarios = await sender.Send(new GetUsuariosAsignablesQuery(), ct);
        Areas    = await sender.Send(new GetAreasQuery(), ct);
        return Page();
    }
}
```

- [ ] **Step 2: Agregar el filtro de área y las pestañas a la vista**

En `src/Web/Pages/Tableros/Proyectos.cshtml`:

1. En el `@{ ... }` inicial, agregar `ViewData["TabActiva"] = "Institucion";` junto a `ViewData["Title"] = "Tablero de Proyectos";`.

2. Justo después de `<div class="hist-header">...</div>` y antes del `<form method="get" class="seg-filters">` existente, agregar:

```html
    <partial name="_TabsProyectos" />
```

3. Dentro del `<form method="get" class="seg-filters">` (líneas 45-80), agregar un campo de selección múltiple de área, antes del `@if (Model.HayFiltro)` final:

```html
        <div class="field" style="min-width:220px">
            <label for="f-area">Área</label>
            <select id="f-area" name="AreaIds" multiple size="1" onchange="this.form.submit()" style="min-height:38px">
                @foreach (var a in Model.Areas)
                {
                    <option value="@a.Id" selected="@(Model.AreaIds?.Contains(a.Id) == true)">@a.Nombre</option>
                }
            </select>
        </div>
```

(Un `<select multiple>` nativo alcanza para "una, varias, o todas a la vez" del spec — sin JS adicional; queda como mejora futura reemplazarlo por un componente con checkboxes si el multiple nativo resulta incómodo de usar.)

- [ ] **Step 3: Verificar en el navegador**

Con un usuario Global/Administrador, navegar a `/Tableros/Proyectos`: deben verse las 3 pestañas (Mi unidad, Mi área si aplica, Institución activa), el selector de área nuevo, y filtrar por una o dos áreas debe reducir correctamente el semáforo del portafolio. Con un usuario sin alcance de institución, navegar a `/Tableros/Proyectos` debe redirigir automáticamente a `/Tableros/ProyectosArea` (si `EsJefeDeArea`) o `/Tableros/ProyectosUnidad`.

- [ ] **Step 4: Commit**

```bash
git add src/Web/Pages/Tableros/Proyectos.cshtml src/Web/Pages/Tableros/Proyectos.cshtml.cs
git commit -m "feat: filtro de área, pestañas y redirección por defecto en /Tableros/Proyectos"
```

---

## Task 11: Verificación final de la suite completa

**Files:** ninguno (solo verificación).

- [ ] **Step 1: Correr toda la suite de Application.Tests**

Run: `dotnet test tests\Application.Tests`
Expected: PASS — todos los tests, incluidos los de las 10 tareas anteriores y los preexistentes.

- [ ] **Step 2: Correr toda la suite de Domain.Tests**

Run: `dotnet test tests\Domain.Tests`
Expected: PASS.

- [ ] **Step 3: Compilar la solución completa**

Run: `dotnet build Diger.TramitesEstado.sln`
Expected: compila sin errores (si Visual Studio tiene `Diger.TramitesEstado.Web` corriendo en el debugger, pararlo antes — es el candado de archivos ya conocido en este repo, no un error de código).

- [ ] **Step 4: Verificación manual de las tres vistas en el navegador**

Con al menos dos usuarios de prueba (uno con un rol `EsJefeDeArea = true` asignado a un área con proyectos, otro sin esa capacidad), confirmar:
1. El usuario sin alcance amplio aterriza en `/Tableros/ProyectosUnidad` y solo ve sus propios proyectos.
2. El usuario `EsJefeDeArea` aterriza en `/Tableros/ProyectosArea`, ve TODOS los proyectos del área (incluidos los que no creó ni le asignaron a mano — la sincronización automática lo puso ahí), y NO puede quitarse a sí mismo de la lista de interesados desde `/Proyectos/Editor` (el botón de quitar debe fallar con el mensaje de `DomainException`).
3. Un usuario Global/Administrador ve la pestaña Institución, puede filtrar por una o varias áreas, y las otras dos pestañas también le aparecen.

No requiere código nuevo — es la confirmación de que las 10 tareas anteriores encajan como un todo.
