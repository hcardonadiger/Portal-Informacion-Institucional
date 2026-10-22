# Reuniones a Outlook — Fase 1 — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que una reunión del portal llegue al calendario de Outlook de su organizador y de sus invitados —con reunión de Teams por defecto— y que guardar la actualice y borrarla la cancele.

**Architecture:** Cada usuario conecta su cuenta Microsoft una vez desde su perfil (OAuth delegado con MSAL; la caché de tokens se guarda cifrada con Data Protection en `ConexionesMicrosoft`). Application arma un `EventoOutlook` puro desde la `Reunion` y habla con `IOutlookCalendario`; Infrastructure lo implementa con `HttpClient` contra Microsoft Graph v1.0. Ninguna otra parte del portal conoce Graph.

**Tech Stack:** .NET 9, Razor Pages, MediatR, EF Core 9 (SQL Server), `Microsoft.Identity.Client` (MSAL.NET), ASP.NET Core Data Protection, xUnit + FluentAssertions + NSubstitute, EF In-Memory (Application.Tests), `PortalFactory` con SQLite (Web.Tests).

**Spec:** `docs/superpowers/specs/2026-10-22-reuniones-a-outlook-design.md` (fase 1). La fase 2 —transcripción de Teams a la ayuda memoria por el MCP— tiene su propio plan.

## Global Constraints

- Dirección única: portal → Outlook. Nada de lo que se haga en Outlook vuelve al portal.
- La invitación sale de la cuenta del **organizador** (Graph delegado). Las actualizaciones y la cancelación usan siempre la conexión de `OutlookOrganizadorId`, nunca la de quien edita.
- Invitados: asistentes con `EsPreregistro = true` y correo válido + `EpCorreo` + `FacCorreo`, deduplicados por correo en minúsculas, sin el correo del organizador.
- Teams **siempre por defecto** (`OutlookConTeams` nace en `true`), cualquiera sea la `Modalidad`; la casilla «Crear reunión de Teams» permite quitarlo antes del envío y queda fija después.
- Zona horaria de Graph: `Central America Standard Time`. Formato de fecha-hora: `yyyy-MM-ddTHH:mm:ss`.
- `Hora` debe ser `HH:mm`. `Duracion` vacía → 1 hora. Lo que no se pueda leer se rechaza con el valor encontrado; no se adivina.
- Guardar una reunión **nunca** falla por Outlook. Borrar una reunión **nunca** se bloquea por Outlook.
- Solo se llama a Graph al guardar si cambió la **huella** del evento (o la reunión está pendiente).
- Alcances delegados: `https://graph.microsoft.com/Calendars.ReadWrite`, `https://graph.microsoft.com/User.Read` (MSAL agrega `offline_access`, `openid`, `profile` por su cuenta: no se pasan).
- Tokens jamás en logs, en la página ni en una respuesta. Caché cifrada con el propósito `"ConexionesMicrosoft.CacheTokens"`.
- `ClientSecret` nunca en un appsettings versionado: User Secrets en desarrollo, configuración del servidor en producción.
- Nombre del producto: siempre `Marca.Nombre`, nunca el literal.
- Commands devuelven `Unit` o un id; queries usan `.AsNoTracking()` y no llaman `SaveChangesAsync`.
- Toda página nueva lleva `[Permission]`, `[AllowAnonymous]` o `[PermisoNoRequerido]`; las de `/Cuenta` que exigen sesión llevan además `[Authorize]`.
- Cambio de base: migración EF con `--output-dir Persistence\Migrations` **y** `database/<fecha>_poner_al_dia.sql` idempotente contra el catálogo, con comprobación `presente`/`FALTA`.
- Desviaciones del spec, decididas al planificar: (1) Graph con `HttpClient` y no con el SDK de Graph — son tres llamadas y así se prueban con un `HttpMessageHandler` falso; (2) la URL de retorno es `/Cuenta/OutlookCallback`; (3) `ConexionesMicrosoft` guarda también `HomeAccountId`, que MSAL necesita para encontrar la cuenta en la caché. La Tarea 10 las asienta en el spec.

## Mapa de archivos

| Archivo | Acción | Responsabilidad |
|---|---|---|
| `src/Domain/Entities/Reunion.cs` | Modificar | Estado Outlook de la reunión y sus transiciones |
| `src/Domain/Entities/ConexionMicrosoft.cs` | Crear | Conexión Microsoft de un usuario (caché cifrada) |
| `src/Application/Reuniones/Outlook/HorarioReunion.cs` | Crear | `Fecha`+`Hora`+`Duracion` → inicio/fin, o rechazo |
| `src/Application/Reuniones/Outlook/EventoOutlook.cs` | Crear | Evento canónico + `Huella()` |
| `src/Application/Reuniones/Outlook/EventoOutlookBuilder.cs` | Crear | `Reunion` → `EventoOutlook` |
| `src/Application/Reuniones/Outlook/IOutlookCalendario.cs` | Crear | Puerto a Graph + excepciones propias |
| `src/Application/Reuniones/Outlook/IConexionMicrosoft.cs` | Crear | Puerto a MSAL (conectar, token) |
| `src/Application/Reuniones/Outlook/OutlookCommands.cs` | Crear | Enviar / Sincronizar / Cancelar |
| `src/Application/Reuniones/Outlook/GetEstadoOutlookQuery.cs` | Crear | Estado Outlook para el editor y el acta |
| `src/Application/CuentaMicrosoft/ConexionMicrosoftCommands.cs` | Crear | Iniciar / completar / desconectar / mi conexión |
| `src/Application/Common/Interfaces/IRepositories.cs` | Modificar | `DbSet<ConexionMicrosoft>` en `IApplicationDbContext` |
| `src/Infrastructure/Persistence/AppDbContext.cs` | Modificar | DbSet + configuración |
| `src/Infrastructure/Persistence/Migrations/<ts>_ReunionesAOutlook.cs` | Generar | Migración EF |
| `database/2026-10-23_poner_al_dia.sql` | Crear | Script idempotente para bases desplegadas |
| `src/Infrastructure/Outlook/MicrosoftGraphOptions.cs` | Crear | Opciones (`MicrosoftGraph:*`) |
| `src/Infrastructure/Outlook/CacheTokensProtector.cs` | Crear | Cifrado de la caché MSAL |
| `src/Infrastructure/Outlook/ConexionMicrosoftService.cs` | Crear | `IConexionMicrosoft` con MSAL |
| `src/Infrastructure/Outlook/GraphOutlookCalendario.cs` | Crear | `IOutlookCalendario` con `HttpClient` |
| `src/Infrastructure/DependencyInjection.cs` | Modificar | Registro |
| `src/Web/Pages/Cuenta/Perfil.cshtml(.cs)` | Modificar | Tarjeta «Microsoft Outlook» |
| `src/Web/Pages/Cuenta/OutlookCallback.cshtml(.cs)` | Crear | Retorno de Microsoft |
| `src/Web/Pages/Reuniones/Editor.cshtml(.cs)` | Modificar | Tarjeta Outlook, enviar, reintentar, sincronizar al guardar |
| `src/Web/Pages/Reuniones/Index.cshtml.cs` | Modificar | Cancelar en Outlook antes de borrar |
| `src/Web/Pages/Reuniones/Acta.cshtml(.cs)` | Modificar | Enlace «Unirse por Teams» |
| `src/Web/appsettings.json` | Modificar | Sección `MicrosoftGraph` sin secreto |
| `tests/Domain.Tests/ReunionOutlookTests.cs` | Crear | |
| `tests/Application.Tests/Reuniones/Outlook/*.cs` | Crear | Horario, builder, comandos |
| `tests/Application.Tests/Outlook/*.cs` | Crear | Protector y cliente Graph |
| `tests/Web.Tests/OutlookPaginasTests.cs` | Crear | Callback y editor de punta a punta con fakes |
| `tests/Web.Tests/PortalFactory.cs` | Modificar | `UsuarioDe(rol)` |

---

### Task 1: Estado Outlook en el dominio

**Files:**
- Modify: `src/Domain/Entities/Reunion.cs` (bloque nuevo antes de `// ── Hijos` / la lista `_asistentes`, y métodos al final de la clase, antes del `}` de la línea 214)
- Create: `src/Domain/Entities/ConexionMicrosoft.cs`
- Test: `tests/Domain.Tests/ReunionOutlookTests.cs`

**Interfaces:**
- Produces (Reunion): `string? OutlookEventId`, `Guid? OutlookTransaccionId`, `Guid? OutlookOrganizadorId`, `bool OutlookConTeams` (= true), `string? OutlookEnlaceTeams`, `string? OutlookHuella`, `DateTime? OutlookSincronizadaEl`, `bool OutlookPendiente`, `string? OutlookError`, `bool EstaEnOutlook`, `Guid PrepararEnvioOutlook(bool conTeams)`, `void RegistrarEnvioOutlook(string eventId, Guid organizadorId, string? enlaceTeams, string huella, DateTime utcAhora)`, `void RegistrarSincronizacionOutlook(string? enlaceTeams, string huella, DateTime utcAhora)`, `void MarcarOutlookPendiente(string motivo)`.
- Produces (ConexionMicrosoft): `Guid Id` (= id del usuario), `string Upn`, `string HomeAccountId`, `byte[] CacheTokens`, `DateTime ConectadaEl`, `bool RequiereReconexion`, `string? UltimoError`, `static ConexionMicrosoft Crear(Guid usuarioId, string upn, string homeAccountId, byte[] cacheCifrada, DateTime utcAhora)`, `void Reconectar(string upn, string homeAccountId, byte[] cacheCifrada, DateTime utcAhora)`, `void GuardarCache(byte[] cacheCifrada)`, `void MarcarVencida(string motivo)`.

- [ ] **Step 1: Write the failing tests**

`tests/Domain.Tests/ReunionOutlookTests.cs`:

```csharp
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Domain.Tests;

/// <summary>
/// Estado de una reunión respecto de Outlook. Lo que se protege: que el Teams nazca encendido,
/// que el reintento de un envío reuse la misma transacción —Graph la usa para no duplicar la
/// cita a los invitados— y que un fallo deje la marca de pendiente con su motivo.
/// </summary>
public class ReunionOutlookTests
{
    [Fact]
    public void Una_reunion_nueva_no_esta_en_Outlook_y_trae_Teams_encendido()
    {
        var r = Reunion.Crear("Mesa técnica");

        r.EstaEnOutlook.Should().BeFalse();
        r.OutlookConTeams.Should().BeTrue("las presenciales también se graban para la transcripción");
        r.OutlookPendiente.Should().BeFalse();
    }

    [Fact]
    public void Preparar_dos_veces_devuelve_la_misma_transaccion()
    {
        var r = Reunion.Crear("Mesa técnica");

        var primera = r.PrepararEnvioOutlook(conTeams: true);
        var segunda = r.PrepararEnvioOutlook(conTeams: true);

        segunda.Should().Be(primera, "un reintento tras un fallo no puede crear una segunda cita");
        primera.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Preparar_sin_Teams_lo_deja_apagado()
    {
        var r = Reunion.Crear("Mesa técnica");

        r.PrepararEnvioOutlook(conTeams: false);

        r.OutlookConTeams.Should().BeFalse();
    }

    [Fact]
    public void No_se_puede_preparar_un_envio_de_una_reunion_ya_enviada()
    {
        var r = Reunion.Crear("Mesa técnica");
        r.PrepararEnvioOutlook(true);
        r.RegistrarEnvioOutlook("AAMk-1", Guid.NewGuid(), "https://teams/x", new string('a', 64), DateTime.UtcNow);

        var acto = () => r.PrepararEnvioOutlook(true);

        acto.Should().Throw<DomainException>();
    }

    [Fact]
    public void Registrar_el_envio_deja_la_reunion_en_Outlook_y_sin_pendiente()
    {
        var r = Reunion.Crear("Mesa técnica");
        var organizador = Guid.NewGuid();
        r.PrepararEnvioOutlook(true);
        r.MarcarOutlookPendiente("Outlook no respondió");

        r.RegistrarEnvioOutlook("AAMk-1", organizador, "https://teams/x", new string('a', 64), new DateTime(2026, 10, 23, 15, 0, 0, DateTimeKind.Utc));

        r.EstaEnOutlook.Should().BeTrue();
        r.OutlookOrganizadorId.Should().Be(organizador);
        r.OutlookEnlaceTeams.Should().Be("https://teams/x");
        r.OutlookHuella.Should().Be(new string('a', 64));
        r.OutlookPendiente.Should().BeFalse();
        r.OutlookError.Should().BeNull();
    }

    [Fact]
    public void Marcar_pendiente_guarda_el_motivo_recortado_a_500()
    {
        var r = Reunion.Crear("Mesa técnica");

        r.MarcarOutlookPendiente(new string('x', 900));

        r.OutlookPendiente.Should().BeTrue();
        r.OutlookError.Should().HaveLength(500);
    }

    [Fact]
    public void Sincronizar_limpia_el_pendiente_y_actualiza_la_huella()
    {
        var r = Reunion.Crear("Mesa técnica");
        r.PrepararEnvioOutlook(true);
        r.RegistrarEnvioOutlook("AAMk-1", Guid.NewGuid(), null, new string('a', 64), DateTime.UtcNow);
        r.MarcarOutlookPendiente("falló");

        r.RegistrarSincronizacionOutlook("https://teams/y", new string('b', 64), DateTime.UtcNow);

        r.OutlookPendiente.Should().BeFalse();
        r.OutlookError.Should().BeNull();
        r.OutlookHuella.Should().Be(new string('b', 64));
        r.OutlookEnlaceTeams.Should().Be("https://teams/y");
    }

    [Fact]
    public void Una_conexion_vencida_conserva_el_motivo_y_reconectar_la_limpia()
    {
        var usuario = Guid.NewGuid();
        var c = ConexionMicrosoft.Crear(usuario, "ana@diger.gob.hn", "oid.tid", [1, 2, 3], DateTime.UtcNow);

        c.MarcarVencida("invalid_grant");
        c.RequiereReconexion.Should().BeTrue();
        c.UltimoError.Should().Be("invalid_grant");

        c.Reconectar("ana@diger.gob.hn", "oid.tid", [4, 5], DateTime.UtcNow);
        c.RequiereReconexion.Should().BeFalse();
        c.UltimoError.Should().BeNull();
        c.CacheTokens.Should().Equal(4, 5);
        c.Id.Should().Be(usuario);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests\Domain.Tests --filter "FullyQualifiedName~ReunionOutlookTests"`
Expected: build FAIL — `'Reunion' does not contain a definition for 'EstaEnOutlook'`, `The type or namespace name 'ConexionMicrosoft' could not be found`.

- [ ] **Step 3: Add the Outlook state to `Reunion`**

En `src/Domain/Entities/Reunion.cs`, justo antes de `private readonly List<Asistente> _asistentes` (bloque de hijos), agregar:

```csharp
    // ── Outlook (fase 1 de reuniones a Outlook) ───────────────────
    // Ver docs/superpowers/specs/2026-10-22-reuniones-a-outlook-design.md. El portal es la
    // fuente de la verdad: estas columnas solo recuerdan qué cita le corresponde en Outlook y si
    // la última sincronización llegó.

    /// <summary>Id de la cita en el calendario del organizador. null = nunca se envió.</summary>
    public string?   OutlookEventId        { get; private set; }
    /// <summary>transactionId de Graph: se genera una vez y se reusa en cada reintento del
    /// envío, para que Graph descarte la creación repetida en vez de duplicar la cita.</summary>
    public Guid?     OutlookTransaccionId  { get; private set; }
    /// <summary>Usuario en cuyo calendario vive la cita. Actualizar y cancelar van siempre con
    /// SU conexión, aunque edite otra persona.</summary>
    public Guid?     OutlookOrganizadorId  { get; private set; }
    /// <summary>Teams nace encendido para toda modalidad: las presenciales también se graban
    /// para sacar la transcripción. Queda fijo tras el envío (Graph no le quita el Teams a una
    /// cita que ya lo tiene).</summary>
    public bool      OutlookConTeams       { get; private set; } = true;
    public string?   OutlookEnlaceTeams    { get; private set; }
    /// <summary>SHA-256 del último evento enviado. Guardar sin cambios visibles no llama a Graph:
    /// cada PATCH le manda «reunión actualizada» a todos los invitados.</summary>
    public string?   OutlookHuella         { get; private set; }
    public DateTime? OutlookSincronizadaEl { get; private set; }
    public bool      OutlookPendiente      { get; private set; }
    public string?   OutlookError          { get; private set; }

    public bool EstaEnOutlook => OutlookEventId is not null;
```

Y al final de la clase (antes del `}` que cierra `Reunion`, después de `EliminarAsistente`):

```csharp
    /// <summary>Fija la transacción del envío (la misma en cada reintento) y si lleva Teams.</summary>
    public Guid PrepararEnvioOutlook(bool conTeams)
    {
        if (EstaEnOutlook)
            throw new DomainException("La reunión ya está en Outlook: los cambios se envían al guardar.");

        OutlookConTeams = conTeams;
        OutlookTransaccionId ??= Guid.NewGuid();
        return OutlookTransaccionId.Value;
    }

    public void RegistrarEnvioOutlook(string eventId, Guid organizadorId, string? enlaceTeams, string huella, DateTime utcAhora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(huella);
        if (organizadorId == Guid.Empty) throw new ArgumentException("La cita necesita organizador.", nameof(organizadorId));

        OutlookEventId       = eventId;
        OutlookOrganizadorId = organizadorId;
        RegistrarSincronizacionOutlook(enlaceTeams, huella, utcAhora);
    }

    public void RegistrarSincronizacionOutlook(string? enlaceTeams, string huella, DateTime utcAhora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(huella);

        OutlookEnlaceTeams    = enlaceTeams ?? OutlookEnlaceTeams;
        OutlookHuella         = huella;
        OutlookSincronizadaEl = utcAhora;
        OutlookPendiente      = false;
        OutlookError          = null;
    }

    public void MarcarOutlookPendiente(string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);

        OutlookPendiente = true;
        OutlookError     = motivo.Length > 500 ? motivo[..500] : motivo;
    }
```

- [ ] **Step 4: Create `ConexionMicrosoft`**

`src/Domain/Entities/ConexionMicrosoft.cs`:

```csharp
namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// La cuenta Microsoft que un usuario conectó desde su perfil para que el portal escriba en su
/// calendario. Una por usuario: el <see cref="BaseEntity{TId}.Id"/> ES el id del usuario.
///
/// <para><see cref="CacheTokens"/> es la caché de MSAL ya cifrada con Data Protection; el dominio
/// la trata como bytes opacos. Si las llaves de Data Protection se pierden, la caché deja de poder
/// leerse y la conexión se trata como vencida: el usuario reconecta.</para>
///
/// <para>No es <c>ISoftDeletable</c>: desconectar borra la fila, porque lo único que guarda es
/// una credencial y una credencial que ya no se usa no debe quedar en la base.</para>
/// </summary>
public sealed class ConexionMicrosoft : BaseAuditableEntity<Guid>
{
    public string   Upn                { get; private set; } = default!;
    public string   HomeAccountId      { get; private set; } = default!;
    public byte[]   CacheTokens        { get; private set; } = [];
    public DateTime ConectadaEl        { get; private set; }
    public bool     RequiereReconexion { get; private set; }
    public string?  UltimoError        { get; private set; }

    private ConexionMicrosoft() { }

    public static ConexionMicrosoft Crear(Guid usuarioId, string upn, string homeAccountId, byte[] cacheCifrada, DateTime utcAhora)
    {
        if (usuarioId == Guid.Empty) throw new ArgumentException("La conexión necesita un usuario.", nameof(usuarioId));

        var c = new ConexionMicrosoft { Id = usuarioId };
        c.Reconectar(upn, homeAccountId, cacheCifrada, utcAhora);
        return c;
    }

    public void Reconectar(string upn, string homeAccountId, byte[] cacheCifrada, DateTime utcAhora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(upn);
        ArgumentException.ThrowIfNullOrWhiteSpace(homeAccountId);
        ArgumentNullException.ThrowIfNull(cacheCifrada);

        Upn                = upn.Trim().ToLowerInvariant();
        HomeAccountId      = homeAccountId;
        CacheTokens        = cacheCifrada;
        ConectadaEl        = utcAhora;
        RequiereReconexion = false;
        UltimoError        = null;
    }

    public void GuardarCache(byte[] cacheCifrada)
    {
        ArgumentNullException.ThrowIfNull(cacheCifrada);
        CacheTokens = cacheCifrada;
    }

    public void MarcarVencida(string motivo)
    {
        RequiereReconexion = true;
        UltimoError        = motivo.Length > 500 ? motivo[..500] : motivo;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests\Domain.Tests --filter "FullyQualifiedName~ReunionOutlookTests"`
Expected: PASS (8 tests).

- [ ] **Step 6: Commit**

```bash
git add src/Domain/Entities/Reunion.cs src/Domain/Entities/ConexionMicrosoft.cs tests/Domain.Tests/ReunionOutlookTests.cs
git commit -m "La reunion recuerda su cita de Outlook y el usuario su conexion Microsoft"
```

---

### Task 2: Horario de la reunión

**Files:**
- Create: `src/Application/Reuniones/Outlook/HorarioReunion.cs`
- Test: `tests/Application.Tests/Reuniones/Outlook/HorarioReunionTests.cs`

**Interfaces:**
- Produces: `public sealed record HorarioResuelto(DateTime Inicio, DateTime Fin)`; `public static class HorarioReunion { public static HorarioResuelto Resolver(DateOnly? fecha, string? hora, string? duracion) }` — lanza `DomainException` con un motivo legible. `Inicio`/`Fin` son hora local de Honduras con `DateTimeKind.Unspecified`.

- [ ] **Step 1: Write the failing tests**

```csharp
using Diger.TramitesEstado.Application.Reuniones.Outlook;
using Diger.TramitesEstado.Domain.Common;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones.Outlook;

/// <summary>
/// Hora y Duracion son texto libre en la base. Medido en GestionGD_new el 2026-10-22: Hora siempre
/// HH:mm; Duracion vacía en 79 de 81, y las otras dos «1 hora» y «1». Lo que no se pueda leer se
/// rechaza nombrando el valor: una cita a la hora equivocada es peor que ninguna.
/// </summary>
public class HorarioReunionTests
{
    private static readonly DateOnly Dia = new(2026, 10, 23);

    [Theory]
    [InlineData("09:00", 9, 0)]
    [InlineData("14:00", 14, 0)]
    [InlineData("13:59", 13, 59)]
    [InlineData("02:00", 2, 0)]
    [InlineData(" 9:30 ", 9, 30)]
    public void Lee_la_hora_HH_mm(string hora, int h, int m)
    {
        var r = HorarioReunion.Resolver(Dia, hora, null);

        r.Inicio.Should().Be(new DateTime(2026, 10, 23, h, m, 0));
    }

    [Theory]
    [InlineData(null, 60)]
    [InlineData("", 60)]
    [InlineData("1", 60)]
    [InlineData("1 hora", 60)]
    [InlineData("2 horas", 120)]
    [InlineData("1.5 horas", 90)]
    [InlineData("1,5 h", 90)]
    [InlineData("90 min", 90)]
    [InlineData("45 minutos", 45)]
    [InlineData("1:30", 90)]
    public void Lee_la_duracion(string? duracion, int minutos)
    {
        var r = HorarioReunion.Resolver(Dia, "09:00", duracion);

        (r.Fin - r.Inicio).TotalMinutes.Should().Be(minutos);
    }

    [Fact]
    public void Sin_fecha_se_rechaza()
    {
        var acto = () => HorarioReunion.Resolver(null, "09:00", null);

        acto.Should().Throw<DomainException>().WithMessage("*fecha y hora*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sin_hora_se_rechaza(string? hora)
    {
        var acto = () => HorarioReunion.Resolver(Dia, hora, null);

        acto.Should().Throw<DomainException>().WithMessage("*fecha y hora*");
    }

    [Theory]
    [InlineData("9 am")]
    [InlineData("25:00")]
    [InlineData("10:75")]
    [InlineData("mañana")]
    public void Una_hora_ilegible_se_rechaza_nombrando_el_valor(string hora)
    {
        var acto = () => HorarioReunion.Resolver(Dia, hora, null);

        acto.Should().Throw<DomainException>().WithMessage($"*«{hora}»*");
    }

    [Theory]
    [InlineData("toda la mañana")]
    [InlineData("0")]
    [InlineData("0 min")]
    [InlineData("30 horas")]
    public void Una_duracion_ilegible_o_absurda_se_rechaza(string duracion)
    {
        var acto = () => HorarioReunion.Resolver(Dia, "09:00", duracion);

        acto.Should().Throw<DomainException>().WithMessage($"*«{duracion}»*");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~HorarioReunionTests"`
Expected: build FAIL — `The name 'HorarioReunion' does not exist`.

- [ ] **Step 3: Implement**

`src/Application/Reuniones/Outlook/HorarioReunion.cs`:

```csharp
using System.Globalization;
using System.Text.RegularExpressions;

namespace Diger.TramitesEstado.Application.Reuniones.Outlook;

/// <summary>Inicio y fin en hora local de Honduras (Kind = Unspecified): Graph recibe la zona
/// aparte, como nombre, y no hay que convertir nada.</summary>
public sealed record HorarioResuelto(DateTime Inicio, DateTime Fin);

/// <summary>
/// Convierte Fecha + Hora + Duracion —texto libre en la base— en un horario de cita, o rechaza
/// nombrando el valor que no pudo leer. No adivina: una cita a la hora equivocada llega igual a la
/// bandeja de todos los invitados.
/// </summary>
public static partial class HorarioReunion
{
    private const int DuracionPorDefectoMin = 60;
    private const int DuracionMaximaMin     = 24 * 60;

    public static HorarioResuelto Resolver(DateOnly? fecha, string? hora, string? duracion)
    {
        if (fecha is null || string.IsNullOrWhiteSpace(hora))
            throw new DomainException("La reunión necesita fecha y hora para ir a Outlook.");

        var h = hora.Trim();
        var mh = HoraRegex().Match(h);
        if (!mh.Success
            || !int.TryParse(mh.Groups[1].Value, out var horas)   || horas   > 23
            || !int.TryParse(mh.Groups[2].Value, out var minutos) || minutos > 59)
            throw new DomainException($"No se entiende la hora «{h}». Debe ir como 09:30.");

        var inicio = fecha.Value.ToDateTime(new TimeOnly(horas, minutos));
        var duracionMin = LeerDuracion(duracion);
        return new HorarioResuelto(inicio, inicio.AddMinutes(duracionMin));
    }

    private static int LeerDuracion(string? duracion)
    {
        if (string.IsNullOrWhiteSpace(duracion)) return DuracionPorDefectoMin;

        var d = duracion.Trim();
        var t = d.ToLowerInvariant();
        double minutos;

        if (HoraRegex().Match(t) is { Success: true } hm)   // «1:30» = una hora y media
            minutos = int.Parse(hm.Groups[1].Value) * 60 + int.Parse(hm.Groups[2].Value);
        else if (NumeroUnidadRegex().Match(t) is { Success: true } nu
                 && double.TryParse(nu.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
            minutos = nu.Groups[2].Value.StartsWith("m") ? n : n * 60;
        else
            throw new DomainException($"No se entiende la duración «{d}». Use, por ejemplo, «1 hora», «90 min» o «1:30».");

        if (minutos <= 0 || minutos > DuracionMaximaMin)
            throw new DomainException($"La duración «{d}» no es razonable para una reunión.");

        return (int)Math.Round(minutos);
    }

    [GeneratedRegex(@"^(\d{1,2}):(\d{2})$")]
    private static partial Regex HoraRegex();

    // «1», «2 horas», «1,5 h», «90 min», «45 minutos». Sin unidad = horas.
    [GeneratedRegex(@"^(\d+(?:[.,]\d+)?)\s*(h|hr|hrs|hora|horas|m|min|mins|minuto|minutos)?$")]
    private static partial Regex NumeroUnidadRegex();
}
```

Nota: `NumeroUnidadRegex` sin unidad deja el grupo 2 vacío; `"".StartsWith("m")` es `false` → horas. Correcto.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~HorarioReunionTests"`
Expected: PASS (todas).

- [ ] **Step 5: Commit**

```bash
git add src/Application/Reuniones/Outlook/HorarioReunion.cs tests/Application.Tests/Reuniones/Outlook/HorarioReunionTests.cs
git commit -m "La hora y la duracion de una reunion se leen o se rechazan, no se adivinan"
```

---

### Task 3: El evento de Outlook y su huella

**Files:**
- Create: `src/Application/Reuniones/Outlook/EventoOutlook.cs`
- Create: `src/Application/Reuniones/Outlook/EventoOutlookBuilder.cs`
- Test: `tests/Application.Tests/Reuniones/Outlook/EventoOutlookBuilderTests.cs`

**Interfaces:**
- Consumes: `HorarioReunion.Resolver` (Task 2); `Reunion` (Task 1).
- Produces:
  - `public sealed record InvitadoOutlook(string Correo, string? Nombre)`
  - `public sealed record EventoOutlook(string Asunto, DateTime Inicio, DateTime Fin, string? Ubicacion, string CuerpoHtml, IReadOnlyList<InvitadoOutlook> Invitados, bool EsTeams) { public const string ZonaHoraria = "Central America Standard Time"; public string Huella(); }`
  - `public static class EventoOutlookBuilder { public static EventoOutlook Construir(Reunion r, string? correoOrganizador, string urlFicha); }` — lanza `DomainException` (vía `HorarioReunion`).

- [ ] **Step 1: Write the failing tests**

```csharp
using Diger.TramitesEstado.Application.Reuniones.Outlook;
using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones.Outlook;

public class EventoOutlookBuilderTests
{
    private const string Url = "https://portal/Reuniones/Acta/7";

    private static Reunion Base(string modalidad = "Presencial")
    {
        var r = Reunion.Crear("Mesa técnica SIGER");
        r.Fecha = new DateOnly(2026, 10, 23);
        r.Hora = "09:00";
        r.Modalidad = modalidad;
        r.Lugar = "Sala 2, DIGER";
        r.ObjetivoAgenda = "Revisar avances & <pendientes>";
        return r;
    }

    [Fact]
    public void Arma_asunto_horario_lugar_y_cuerpo()
    {
        var e = EventoOutlookBuilder.Construir(Base(), "org@diger.gob.hn", Url);

        e.Asunto.Should().Be("Mesa técnica SIGER");
        e.Inicio.Should().Be(new DateTime(2026, 10, 23, 9, 0, 0));
        e.Fin.Should().Be(new DateTime(2026, 10, 23, 10, 0, 0));
        e.Ubicacion.Should().Be("Sala 2, DIGER");
        e.CuerpoHtml.Should().Contain("Revisar avances &amp; &lt;pendientes&gt;", "el objetivo es texto del usuario y va escapado");
        e.CuerpoHtml.Should().Contain(Url);
    }

    [Theory]
    [InlineData("Presencial")]
    [InlineData("Virtual")]
    [InlineData("Híbrida")]
    [InlineData(null)]
    public void Teams_va_en_toda_modalidad_mientras_no_se_apague(string? modalidad)
    {
        var e = EventoOutlookBuilder.Construir(Base(modalidad!), null, Url);

        e.EsTeams.Should().BeTrue();
    }

    [Fact]
    public void Sin_Teams_cuando_se_desmarco_al_enviar()
    {
        var r = Base();
        r.PrepararEnvioOutlook(conTeams: false);

        EventoOutlookBuilder.Construir(r, null, Url).EsTeams.Should().BeFalse();
    }

    [Fact]
    public void Invita_preregistrados_con_correo_enlace_y_facilitador_sin_repetir_ni_al_organizador()
    {
        var r = Base();
        r.PreRegistrar("Ana Paz", null, "IHTT", null, "ana@ihtt.gob.hn", null);
        r.PreRegistrar("Ana Paz (dup)", null, "IHTT", null, "ANA@ihtt.gob.hn", null);
        r.PreRegistrar("Sin correo", null, "IHTT", null, null, null);
        r.PreRegistrar("Correo roto", null, "IHTT", null, "no-es-correo", null);
        r.PreRegistrar("El organizador", null, "DIGER", null, "org@diger.gob.hn", null);
        r.RegistrarAsistente("Llegó por QR", null, "IHTT", null, "qr@ihtt.gob.hn", null);
        r.EpNombre = "Luis Enlace"; r.EpCorreo = "luis@ihtt.gob.hn";
        r.FacNombre = "Carla Facilitadora"; r.FacCorreo = "carla@diger.gob.hn";

        var e = EventoOutlookBuilder.Construir(r, "ORG@diger.gob.hn", Url);

        e.Invitados.Select(i => i.Correo).Should().BeEquivalentTo(
            ["ana@ihtt.gob.hn", "luis@ihtt.gob.hn", "carla@diger.gob.hn"],
            "el auto-registro por QR es asistencia, no invitación");
    }

    [Fact]
    public void Una_hora_ilegible_se_propaga_como_rechazo()
    {
        var r = Base();
        r.Hora = "9 am";

        var acto = () => EventoOutlookBuilder.Construir(r, null, Url);

        acto.Should().Throw<Diger.TramitesEstado.Domain.Common.DomainException>();
    }

    [Fact]
    public void La_huella_es_estable_e_ignora_el_orden_y_las_mayusculas_de_los_invitados()
    {
        var a = new EventoOutlook("X", new DateTime(2026, 1, 1, 9, 0, 0), new DateTime(2026, 1, 1, 10, 0, 0), "Sala", "<p>x</p>",
            [new("b@x.hn", "B"), new("a@x.hn", "A")], true);
        var b = a with { Invitados = [new("A@X.HN", "A"), new("b@x.hn", "B")] };

        a.Huella().Should().Be(b.Huella()).And.HaveLength(64);
    }

    [Theory]
    [InlineData("asunto")]
    [InlineData("inicio")]
    [InlineData("lugar")]
    [InlineData("cuerpo")]
    [InlineData("invitado")]
    [InlineData("teams")]
    public void La_huella_cambia_con_cualquier_campo_visible(string campo)
    {
        var a = new EventoOutlook("X", new DateTime(2026, 1, 1, 9, 0, 0), new DateTime(2026, 1, 1, 10, 0, 0), "Sala", "<p>x</p>",
            [new("a@x.hn", "A")], true);
        var b = campo switch
        {
            "asunto"   => a with { Asunto = "Y" },
            "inicio"   => a with { Inicio = a.Inicio.AddMinutes(30) },
            "lugar"    => a with { Ubicacion = "Otra sala" },
            "cuerpo"   => a with { CuerpoHtml = "<p>y</p>" },
            "invitado" => a with { Invitados = [new("a@x.hn", "A"), new("c@x.hn", "C")] },
            _          => a with { EsTeams = false }
        };

        b.Huella().Should().NotBe(a.Huella());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~EventoOutlookBuilderTests"`
Expected: build FAIL — `The type or namespace name 'EventoOutlook' could not be found`.

- [ ] **Step 3: Implement `EventoOutlook`**

`src/Application/Reuniones/Outlook/EventoOutlook.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace Diger.TramitesEstado.Application.Reuniones.Outlook;

public sealed record InvitadoOutlook(string Correo, string? Nombre);

/// <summary>La cita tal como la verá el invitado. Solo datos: ni ids de Graph ni tokens.</summary>
public sealed record EventoOutlook(
    string Asunto,
    DateTime Inicio,
    DateTime Fin,
    string? Ubicacion,
    string CuerpoHtml,
    IReadOnlyList<InvitadoOutlook> Invitados,
    bool EsTeams)
{
    /// <summary>Nombre de Windows de la zona de America/Tegucigalpa, que es el que pide Graph.</summary>
    public const string ZonaHoraria = "Central America Standard Time";

    /// <summary>
    /// SHA-256 en hex de la forma canónica. Invitados por correo en minúsculas y ordenados: el
    /// orden en que se capturaron no es un cambio que justifique avisar a todos.
    /// </summary>
    public string Huella()
    {
        var sb = new StringBuilder()
            .Append(Asunto).Append('\n')
            .Append(Inicio.ToString("s")).Append('\n')
            .Append(Fin.ToString("s")).Append('\n')
            .Append(Ubicacion).Append('\n')
            .Append(CuerpoHtml).Append('\n')
            .Append(EsTeams ? '1' : '0').Append('\n');

        foreach (var correo in Invitados.Select(i => i.Correo.Trim().ToLowerInvariant()).Order(StringComparer.Ordinal))
            sb.Append(correo).Append(';');

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }
}
```

- [ ] **Step 4: Implement `EventoOutlookBuilder`**

`src/Application/Reuniones/Outlook/EventoOutlookBuilder.cs`:

```csharp
using System.Net;
using System.Net.Mail;
using Diger.TramitesEstado.Application.Common.Models;

namespace Diger.TramitesEstado.Application.Reuniones.Outlook;

/// <summary>
/// Reunion → EventoOutlook. Puro: no lee la base ni llama a Graph, así que se prueba entero.
/// Invitados = pre-registrados con correo + enlace institucional + facilitador. El auto-registro
/// por QR NO invita: es asistencia ya ocurrida.
/// </summary>
public static class EventoOutlookBuilder
{
    public static EventoOutlook Construir(Reunion r, string? correoOrganizador, string urlFicha)
    {
        var horario = HorarioReunion.Resolver(r.Fecha, r.Hora, r.Duracion);

        var organizador = Normalizar(correoOrganizador);
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var invitados = new List<InvitadoOutlook>();

        void Agregar(string? correo, string? nombre)
        {
            var c = Normalizar(correo);
            if (c is null || c == organizador || !vistos.Add(c)) return;
            invitados.Add(new InvitadoOutlook(c, string.IsNullOrWhiteSpace(nombre) ? null : nombre.Trim()));
        }

        foreach (var a in r.Asistentes.Where(a => a.EsPreregistro))
            Agregar(a.Correo, a.Nombre);
        Agregar(r.EpCorreo, r.EpNombre);
        Agregar(r.FacCorreo, r.FacNombre);

        var cuerpo = string.IsNullOrWhiteSpace(r.ObjetivoAgenda)
            ? string.Empty
            : $"<p>{WebUtility.HtmlEncode(r.ObjetivoAgenda.Trim())}</p>";
        cuerpo += $"<p>Ficha en {WebUtility.HtmlEncode(Marca.Nombre)}: <a href=\"{WebUtility.HtmlEncode(urlFicha)}\">{WebUtility.HtmlEncode(urlFicha)}</a></p>";

        return new EventoOutlook(
            Asunto:     r.Titulo,
            Inicio:     horario.Inicio,
            Fin:        horario.Fin,
            Ubicacion:  string.IsNullOrWhiteSpace(r.Lugar) ? null : r.Lugar.Trim(),
            CuerpoHtml: cuerpo,
            Invitados:  invitados,
            EsTeams:    r.OutlookConTeams);
    }

    /// <summary>Correo en minúsculas, o null si no es una dirección.</summary>
    private static string? Normalizar(string? correo)
    {
        if (string.IsNullOrWhiteSpace(correo)) return null;
        var c = correo.Trim().ToLowerInvariant();
        return MailAddress.TryCreate(c, out var m) && m.Address == c && c.Contains('.') ? c : null;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~EventoOutlookBuilderTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Application/Reuniones/Outlook/EventoOutlook.cs src/Application/Reuniones/Outlook/EventoOutlookBuilder.cs tests/Application.Tests/Reuniones/Outlook/EventoOutlookBuilderTests.cs
git commit -m "La reunion se traduce a una cita de Outlook con huella, Teams por defecto"
```

---

### Task 4: Puertos, persistencia, migración y script

**Files:**
- Create: `src/Application/Reuniones/Outlook/IOutlookCalendario.cs`
- Create: `src/Application/Reuniones/Outlook/IConexionMicrosoft.cs`
- Modify: `src/Application/Common/Interfaces/IRepositories.cs` (dentro de `IApplicationDbContext`, junto a `DbSet<TokenAccesoMcp>` línea ~241)
- Modify: `src/Infrastructure/Persistence/AppDbContext.cs` (DbSet junto a línea 20; `ReunionConfiguration` línea ~635; clase nueva junto a `TokenAccesoMcpConfiguration` línea ~1484)
- Generate: migración `ReunionesAOutlook`
- Create: `database/2026-10-23_poner_al_dia.sql`

**Interfaces:**
- Produces:
  ```csharp
  public sealed record ResultadoOutlook(string EventId, string? EnlaceTeams);
  public interface IOutlookCalendario
  {
      Task<ResultadoOutlook> CrearAsync(Guid organizadorId, EventoOutlook evento, Guid transaccionId, CancellationToken ct);
      Task<ResultadoOutlook> ActualizarAsync(Guid organizadorId, string eventId, EventoOutlook evento, CancellationToken ct);
      Task CancelarAsync(Guid organizadorId, string eventId, string comentario, CancellationToken ct);
  }
  public sealed class OutlookConexionVencidaException(string message) : Exception(message);
  public sealed class OutlookNoDisponibleException(string message) : Exception(message);

  public sealed record InicioConexionDto(string Url, string Estado, string Verificador);
  public interface IConexionMicrosoft
  {
      bool EstaConfigurada { get; }
      Task<InicioConexionDto> IniciarAsync(string urlRetorno, CancellationToken ct);
      Task CompletarAsync(Guid usuarioId, string codigo, string verificador, string urlRetorno, CancellationToken ct);
      Task<string> ObtenerTokenAsync(Guid usuarioId, CancellationToken ct);
  }
  // IApplicationDbContext:
  DbSet<ConexionMicrosoft> ConexionesMicrosoft { get; }
  ```

- [ ] **Step 1: Create the ports**

`src/Application/Reuniones/Outlook/IOutlookCalendario.cs`:

```csharp
namespace Diger.TramitesEstado.Application.Reuniones.Outlook;

public sealed record ResultadoOutlook(string EventId, string? EnlaceTeams);

/// <summary>
/// Lo único que el portal sabe de Microsoft Graph. Siempre actúa en el calendario de
/// <paramref name="organizadorId"/>: es su conexión la que se usa, no la de quien llama.
/// Los fallos llegan como <see cref="OutlookConexionVencidaException"/> o
/// <see cref="OutlookNoDisponibleException"/>; ningún tipo de Graph cruza esta interfaz.
/// </summary>
public interface IOutlookCalendario
{
    Task<ResultadoOutlook> CrearAsync(Guid organizadorId, EventoOutlook evento, Guid transaccionId, CancellationToken ct);
    Task<ResultadoOutlook> ActualizarAsync(Guid organizadorId, string eventId, EventoOutlook evento, CancellationToken ct);
    Task CancelarAsync(Guid organizadorId, string eventId, string comentario, CancellationToken ct);
}

/// <summary>Microsoft ya no acepta la conexión de ese usuario (token revocado, contraseña cambiada,
/// inactividad, llaves de cifrado perdidas): tiene que reconectarse desde su perfil.</summary>
public sealed class OutlookConexionVencidaException(string message) : Exception(message);

/// <summary>Graph no respondió o rechazó la operación por algo que no es la conexión.</summary>
public sealed class OutlookNoDisponibleException(string message) : Exception(message);
```

`src/Application/Reuniones/Outlook/IConexionMicrosoft.cs`:

```csharp
namespace Diger.TramitesEstado.Application.Reuniones.Outlook;

/// <summary>Lo que la página guarda entre ir a Microsoft y volver: el estado (anti-CSRF) y el
/// verificador PKCE. Nunca va a la base ni a un log.</summary>
public sealed record InicioConexionDto(string Url, string Estado, string Verificador);

/// <summary>
/// Conexión de un usuario con su cuenta Microsoft (OAuth delegado). La implementación es dueña
/// de la fila de <c>ConexionesMicrosoft</c>: MSAL renueva tokens en cada uso y la caché nueva
/// tiene que persistirse ahí mismo.
/// </summary>
public interface IConexionMicrosoft
{
    /// <summary>false si el servidor no tiene TenantId/ClientId/ClientSecret: la UI se apaga.</summary>
    bool EstaConfigurada { get; }

    Task<InicioConexionDto> IniciarAsync(string urlRetorno, CancellationToken ct);

    /// <summary>Canjea el código de Microsoft y crea o renueva la conexión del usuario.</summary>
    Task CompletarAsync(Guid usuarioId, string codigo, string verificador, string urlRetorno, CancellationToken ct);

    /// <summary>Token de acceso a Graph vigente. Lanza <see cref="OutlookConexionVencidaException"/>
    /// (y marca la fila) si hay que reconectar.</summary>
    Task<string> ObtenerTokenAsync(Guid usuarioId, CancellationToken ct);
}
```

- [ ] **Step 2: Register the DbSet and configure the model**

En `IApplicationDbContext` (`src/Application/Common/Interfaces/IRepositories.cs`), junto a `DbSet<TokenAccesoMcp> TokensAccesoMcp { get; }`:

```csharp
    DbSet<ConexionMicrosoft>        ConexionesMicrosoft { get; }
```

En `AppDbContext` (`src/Infrastructure/Persistence/AppDbContext.cs`), junto a la línea 20:

```csharp
    public DbSet<ConexionMicrosoft>        ConexionesMicrosoft { get; init; } = default!;
```

Al final de `ReunionConfiguration.Configure` (antes de su `}`):

```csharp
        // Outlook (fase 1). OutlookConTeams nace en 1 también en la base: las filas existentes
        // quedan con Teams encendido, que es el comportamiento por defecto.
        b.Property(x => x.OutlookEventId).HasMaxLength(400);
        b.Property(x => x.OutlookEnlaceTeams).HasMaxLength(1000);
        b.Property(x => x.OutlookHuella).HasMaxLength(64).IsFixedLength();
        b.Property(x => x.OutlookError).HasMaxLength(500);
        b.Property(x => x.OutlookConTeams).HasDefaultValue(true);
        b.Property(x => x.OutlookPendiente).HasDefaultValue(false);
        b.Ignore(x => x.EstaEnOutlook);
```

Clase nueva, después de `TokenAccesoMcpConfiguration`:

```csharp
public sealed class ConexionMicrosoftConfiguration : IEntityTypeConfiguration<ConexionMicrosoft>
{
    public void Configure(EntityTypeBuilder<ConexionMicrosoft> b)
    {
        b.ToTable("ConexionesMicrosoft");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever(); // es el id del usuario
        b.Property(x => x.Upn).HasMaxLength(256).IsRequired();
        b.Property(x => x.HomeAccountId).HasMaxLength(200).IsRequired();
        b.Property(x => x.CacheTokens).IsRequired();
        b.Property(x => x.UltimoError).HasMaxLength(500);

        // Cascada: la conexión no tiene sentido sin su usuario, y es una credencial.
        b.HasOne<Usuario>().WithOne().HasForeignKey<ConexionMicrosoft>(x => x.Id).OnDelete(DeleteBehavior.Cascade);
    }
}
```

`ConexionMicrosoft` no lleva filtro de alcance (igual que `Usuario`): no agregar nada en `OnModelCreating`.

- [ ] **Step 3: Build**

Run: `dotnet build Diger.TramitesEstado.sln`
Expected: 0 errores. Si falla solo con `MSB3027`/`MSB3021`, hay una app corriendo: cerrar `Diger.TramitesEstado.Web` (no Visual Studio) y repetir.

- [ ] **Step 4: Generate the migration**

Run:
```powershell
dotnet ef migrations add ReunionesAOutlook --project src\Infrastructure --startup-project src\Web --output-dir Persistence\Migrations
```
Expected: se crean `src/Infrastructure/Persistence/Migrations/<ts>_ReunionesAOutlook.cs` y su `.Designer.cs`, y cambia `AppDbContextModelSnapshot.cs`. Abrir el `.cs` y verificar que el `Up` contiene **solo**: `CreateTable("ConexionesMicrosoft")` con FK a `Usuarios` y nueve `AddColumn` en `Reuniones` (`OutlookConTeams` con `defaultValue: true`, `OutlookPendiente` con `defaultValue: false`). Si propone algo más, el snapshot estaba desfasado: parar y avisar, no editarlo a mano.

- [ ] **Step 5: Write the deployment script**

`database/2026-10-23_poner_al_dia.sql` (con BOM UTF-8, como los demás):

```sql
/* =====================================================================================
   Poner al día la base con los cambios al 2026-10-23 — reuniones a Outlook (fase 1)
   -------------------------------------------------------------------------------------
   Va DESPUÉS de 2026-10-22_poner_al_dia.sql. Corra antes 00_diagnostico.sql.

   QUÉ TRAE — la migración EF <ts>_ReunionesAOutlook:

     ConexionesMicrosoft   una fila por usuario que conectó su cuenta Microsoft desde su
                           perfil. Guarda la caché de tokens de MSAL CIFRADA con Data
                           Protection: sin las llaves del servidor no se puede leer.
     Reuniones.Outlook*    nueve columnas que recuerdan la cita de Outlook de cada reunión:
                           su id, quién la organiza, el enlace de Teams, la huella de lo último
                           enviado y si la última sincronización quedó pendiente.

   ES ADITIVO. No cambia tipos ni mueve datos. OutlookConTeams nace en 1 para las filas
   existentes: Teams es el comportamiento por defecto.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-23_poner_al_dia.sql
   ===================================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'[Reuniones]', N'U') IS NULL
   OR OBJECT_ID(N'[Usuarios]', N'U') IS NULL
   OR OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
BEGIN
    THROW 50000,
        'Faltan Reuniones, Usuarios o __EFMigrationsHistory. Corra 00_diagnostico.sql: no es la base del portal o está incompleta.',
        1;
END
GO

PRINT '════════ Reuniones a Outlook ════════';
GO

IF OBJECT_ID(N'[ConexionesMicrosoft]', N'U') IS NULL
BEGIN
    CREATE TABLE [ConexionesMicrosoft] (
        [Id]                 uniqueidentifier NOT NULL,
        [Upn]                nvarchar(256)    NOT NULL,
        [HomeAccountId]      nvarchar(200)    NOT NULL,
        [CacheTokens]        varbinary(max)   NOT NULL,
        [ConectadaEl]        datetime2        NOT NULL,
        [RequiereReconexion] bit              NOT NULL,
        [UltimoError]        nvarchar(500)    NULL,
        [CreatedAt]          datetime2        NOT NULL,
        [CreatedBy]          nvarchar(max)    NULL,
        [UpdatedAt]          datetime2        NULL,
        [UpdatedBy]          nvarchar(max)    NULL,
        CONSTRAINT [PK_ConexionesMicrosoft] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ConexionesMicrosoft_Usuarios_Id]
            FOREIGN KEY ([Id]) REFERENCES [Usuarios] ([Id]) ON DELETE CASCADE
    );
    PRINT '  ConexionesMicrosoft ............. creada';
END
ELSE PRINT '  ConexionesMicrosoft ............. ya estaba';
GO

IF COL_LENGTH(N'Reuniones', N'OutlookEventId') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookEventId] nvarchar(400) NULL; PRINT '  OutlookEventId .................. agregada'; END
ELSE PRINT '  OutlookEventId .................. ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookTransaccionId') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookTransaccionId] uniqueidentifier NULL; PRINT '  OutlookTransaccionId ............ agregada'; END
ELSE PRINT '  OutlookTransaccionId ............ ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookOrganizadorId') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookOrganizadorId] uniqueidentifier NULL; PRINT '  OutlookOrganizadorId ............ agregada'; END
ELSE PRINT '  OutlookOrganizadorId ............ ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookConTeams') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookConTeams] bit NOT NULL CONSTRAINT [DF_Reuniones_OutlookConTeams] DEFAULT (CONVERT(bit, 1)); PRINT '  OutlookConTeams ................. agregada'; END
ELSE PRINT '  OutlookConTeams ................. ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookEnlaceTeams') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookEnlaceTeams] nvarchar(1000) NULL; PRINT '  OutlookEnlaceTeams .............. agregada'; END
ELSE PRINT '  OutlookEnlaceTeams .............. ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookHuella') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookHuella] nchar(64) NULL; PRINT '  OutlookHuella ................... agregada'; END
ELSE PRINT '  OutlookHuella ................... ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookSincronizadaEl') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookSincronizadaEl] datetime2 NULL; PRINT '  OutlookSincronizadaEl ........... agregada'; END
ELSE PRINT '  OutlookSincronizadaEl ........... ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookPendiente') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookPendiente] bit NOT NULL CONSTRAINT [DF_Reuniones_OutlookPendiente] DEFAULT (CONVERT(bit, 0)); PRINT '  OutlookPendiente ................ agregada'; END
ELSE PRINT '  OutlookPendiente ................ ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookError') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookError] nvarchar(500) NULL; PRINT '  OutlookError .................... agregada'; END
ELSE PRINT '  OutlookError .................... ya estaba';
GO

/* El historial al final y solo si todo está: escribirlo antes haría que `dotnet ef` se
   saltara una base a medias. Reemplace <MigrationId> por el nombre exacto del archivo que
   generó el Step 4 (sin .cs). */
IF OBJECT_ID(N'[ConexionesMicrosoft]', N'U') IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookEventId')        IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookTransaccionId')  IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookOrganizadorId')  IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookConTeams')       IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookEnlaceTeams')    IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookHuella')         IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookSincronizadaEl') IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookPendiente')      IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookError')          IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'<MigrationId>')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES (N'<MigrationId>', N'9.0.0');
    PRINT '  historial ....................... registrado';
END
ELSE PRINT '  historial ....................... ya estaba';
GO

PRINT '';
PRINT '════════ Comprobación ════════';
SELECT N'Tabla ConexionesMicrosoft' AS Control,
       CASE WHEN OBJECT_ID(N'[ConexionesMicrosoft]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL SELECT N'FK ConexionesMicrosoft → Usuarios',
       CASE WHEN OBJECT_ID(N'[FK_ConexionesMicrosoft_Usuarios_Id]', N'F') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT N'Las nueve columnas Outlook de Reuniones',
       CASE WHEN (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'[Reuniones]') AND name LIKE N'Outlook%') = 9
            THEN 'presente' ELSE 'FALTA' END
UNION ALL SELECT N'Migración registrada en el historial',
       CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'<MigrationId>')
            THEN 'presente' ELSE 'FALTA' END;
PRINT '';
PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
GO
```

Después de generar la migración, reemplazar las tres apariciones de `<MigrationId>` y la de `<ts>` en el encabezado por el nombre real (p. ej. `20261023153012_ReunionesAOutlook`). Comparar los nombres de las restricciones del script con los de la migración: la FK debe llamarse igual en los dos (`FK_ConexionesMicrosoft_Usuarios_Id`); si EF la nombró distinto, usar el nombre de EF en ambos sitios del script.

- [ ] **Step 6: Run the script twice against a COPY of `GestionGD_new`**

```powershell
sqlcmd -S "localhost\SQL2025" -E -C -b -Q "BACKUP DATABASE [GestionGD_new] TO DISK = N'GestionGD_new_antes_de_outlook.bak' WITH COPY_ONLY, INIT; RESTORE DATABASE [GestionGD_PruebaOutlook] FROM DISK = N'GestionGD_new_antes_de_outlook.bak' WITH MOVE 'TramitesEstado_Prod' TO 'C:\Program Files\Microsoft SQL Server\MSSQL17.SQL2025\MSSQL\DATA\GestionGD_PruebaOutlook.mdf', MOVE 'TramitesEstado_Prod_log' TO 'C:\Program Files\Microsoft SQL Server\MSSQL17.SQL2025\MSSQL\DATA\GestionGD_PruebaOutlook_log.ldf'"
sqlcmd -S "localhost\SQL2025" -d GestionGD_PruebaOutlook -E -C -b -I -f 65001 -W -i database\2026-10-23_poner_al_dia.sql
sqlcmd -S "localhost\SQL2025" -d GestionGD_PruebaOutlook -E -C -b -I -f 65001 -W -i database\2026-10-23_poner_al_dia.sql
sqlcmd -S "localhost\SQL2025" -E -C -Q "DROP DATABASE [GestionGD_PruebaOutlook]"
```
Expected: primera corrida «creada/agregada/registrado» y cuatro `presente`; segunda corrida todo «ya estaba» y cuatro `presente`; exit 0 ambas.

- [ ] **Step 7: Apply to `GestionGD_new` and confirm EF agrees**

```powershell
sqlcmd -S "localhost\SQL2025" -d GestionGD_new -E -C -b -I -f 65001 -W -i database\2026-10-23_poner_al_dia.sql
dotnet ef migrations list --project src\Infrastructure --startup-project src\Web --no-build
```
Expected: cuatro `presente`; la lista no marca ninguna migración como `(Pending)`.

- [ ] **Step 8: Commit**

```bash
git add src/Application/Reuniones/Outlook/IOutlookCalendario.cs src/Application/Reuniones/Outlook/IConexionMicrosoft.cs src/Application/Common/Interfaces/IRepositories.cs src/Infrastructure/Persistence/AppDbContext.cs src/Infrastructure/Persistence/Migrations src/Infrastructure/Migrations/AppDbContextModelSnapshot.cs database/2026-10-23_poner_al_dia.sql
git commit -m "Base para reuniones a Outlook: conexiones Microsoft y estado de la cita, con su script"
```

---

### Task 5: Enviar, sincronizar y cancelar (Application)

**Files:**
- Create: `src/Application/Reuniones/Outlook/OutlookCommands.cs`
- Create: `src/Application/Reuniones/Outlook/GetEstadoOutlookQuery.cs`
- Test: `tests/Application.Tests/Reuniones/Outlook/OutlookCommandsTests.cs`

**Interfaces:**
- Consumes: Tasks 1–4.
- Produces:
  - `public sealed record EnviarReunionAOutlookCommand(int ReunionId, bool ConTeams, string UrlFicha) : IRequest<Unit>` — lanza `DomainException` con el motivo legible.
  - `public sealed record SincronizarReunionOutlookCommand(int ReunionId, string UrlFicha) : IRequest<Unit>` — nunca lanza por Outlook; deja `OutlookPendiente`.
  - `public sealed record CancelarReunionOutlookCommand(int ReunionId) : IRequest<Unit>` — lanza `DomainException` si no pudo cancelar.
  - `public sealed record GetEstadoOutlookQuery(int ReunionId) : IRequest<EstadoOutlookDto>`
  - `public sealed record EstadoOutlookDto(bool Configurado, bool UsuarioConectado, bool UsuarioRequiereReconexion, bool EnOutlook, bool ConTeams, string? EnlaceTeams, bool Pendiente, string? Error, DateTime? SincronizadaEl, string? Organizador)`

- [ ] **Step 1: Write the failing tests**

`tests/Application.Tests/Reuniones/Outlook/OutlookCommandsTests.cs`:

```csharp
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Reuniones.Outlook;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones.Outlook;

/// <summary>
/// Lo que se protege: guardar nunca falla por Outlook, guardar sin cambios visibles no molesta a
/// los invitados, el reintento no duplica la cita, actualizar usa la conexión del ORGANIZADOR, y
/// borrar nunca queda bloqueado por Microsoft.
/// </summary>
public class OutlookCommandsTests : IDisposable
{
    private const string Url = "https://portal/Reuniones/Acta/1";

    private readonly AppDbContext _ctx;
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();
    private readonly IOutlookCalendario _outlook = Substitute.For<IOutlookCalendario>();
    private readonly IConexionMicrosoft _conexion = Substitute.For<IConexionMicrosoft>();
    private readonly Usuario _organizador = Usuario.Crear("Ana Organizadora", "ana@diger.gob.hn", "hash");
    private readonly Usuario _editor = Usuario.Crear("Beto Editor", "beto@diger.gob.hn", "hash");

    public OutlookCommandsTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _usuario.EsGlobal.Returns(true);
        _usuario.UserId.Returns(_organizador.Id);
        _ctx = new AppDbContext(opts, _usuario, Substitute.For<MediatR.IPublisher>());
        _ctx.Usuarios.AddRange(_organizador, _editor);
        _ctx.SaveChanges();
        _conexion.EstaConfigurada.Returns(true);
        _outlook.CrearAsync(default, default!, default, default)
            .ReturnsForAnyArgs(new ResultadoOutlook("AAMk-1", "https://teams/abc"));
        _outlook.ActualizarAsync(default, default!, default!, default)
            .ReturnsForAnyArgs(new ResultadoOutlook("AAMk-1", "https://teams/abc"));
    }

    public void Dispose() => _ctx.Dispose();

    private void Conectar(Usuario u, bool vencida = false)
    {
        var c = ConexionMicrosoft.Crear(u.Id, u.Correo, "oid.tid", [1], DateTime.UtcNow);
        if (vencida) c.MarcarVencida("invalid_grant");
        _ctx.ConexionesMicrosoft.Add(c);
        _ctx.SaveChanges();
    }

    private Reunion Sembrar(string hora = "09:00")
    {
        var r = Reunion.Crear("Mesa técnica");
        r.Fecha = new DateOnly(2026, 10, 23);
        r.Hora = hora;
        r.PreRegistrar("Invitada", null, "IHTT", null, "invitada@ihtt.gob.hn", null);
        _ctx.Reuniones.Add(r);
        _ctx.SaveChanges();
        return r;
    }

    private EnviarReunionAOutlookCommandHandler Enviar() => new(_ctx, new ReunionRepository(_ctx), _usuario, _outlook, _ctx);
    private SincronizarReunionOutlookCommandHandler Sincronizar() => new(_ctx, new ReunionRepository(_ctx), _outlook, _ctx);
    private CancelarReunionOutlookCommandHandler Cancelar() => new(new ReunionRepository(_ctx), _outlook);

    private async Task<Reunion> EnviadaAsync()
    {
        Conectar(_organizador);
        var r = Sembrar();
        await Enviar().Handle(new EnviarReunionAOutlookCommand(r.Id, true, Url), default);
        _outlook.ClearReceivedCalls();
        return r;
    }

    // ── Enviar ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Enviar_crea_la_cita_y_guarda_id_organizador_enlace_y_huella()
    {
        Conectar(_organizador);
        var r = Sembrar();

        await Enviar().Handle(new EnviarReunionAOutlookCommand(r.Id, true, Url), default);

        var fila = await _ctx.Reuniones.AsNoTracking().SingleAsync();
        fila.OutlookEventId.Should().Be("AAMk-1");
        fila.OutlookOrganizadorId.Should().Be(_organizador.Id);
        fila.OutlookEnlaceTeams.Should().Be("https://teams/abc");
        fila.OutlookHuella.Should().HaveLength(64);
        await _outlook.Received(1).CrearAsync(_organizador.Id,
            Arg.Is<EventoOutlook>(e => e.EsTeams && e.Invitados.Single().Correo == "invitada@ihtt.gob.hn"),
            fila.OutlookTransaccionId!.Value, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_conexion_no_se_envia_y_se_dice_donde_conectar()
    {
        var r = Sembrar();

        var acto = () => Enviar().Handle(new EnviarReunionAOutlookCommand(r.Id, true, Url), default);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*Mi perfil*");
        await _outlook.DidNotReceiveWithAnyArgs().CrearAsync(default, default!, default, default);
    }

    [Fact]
    public async Task Una_hora_ilegible_no_llega_a_Graph()
    {
        Conectar(_organizador);
        var r = Sembrar(hora: "9 am");

        var acto = () => Enviar().Handle(new EnviarReunionAOutlookCommand(r.Id, true, Url), default);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*«9 am»*");
        await _outlook.DidNotReceiveWithAnyArgs().CrearAsync(default, default!, default, default);
    }

    [Fact]
    public async Task Si_Graph_falla_el_reintento_reusa_la_misma_transaccion()
    {
        Conectar(_organizador);
        var r = Sembrar();
        _outlook.CrearAsync(default, default!, default, default)
            .ThrowsAsyncForAnyArgs(new OutlookNoDisponibleException("503"));

        var primero = () => Enviar().Handle(new EnviarReunionAOutlookCommand(r.Id, true, Url), default);
        await primero.Should().ThrowAsync<DomainException>().WithMessage("*no se duplicará*");
        var transaccion = (await _ctx.Reuniones.AsNoTracking().SingleAsync()).OutlookTransaccionId;
        transaccion.Should().NotBeNull("se guarda ANTES de llamar a Graph");

        _outlook.CrearAsync(default, default!, default, default)
            .ReturnsForAnyArgs(new ResultadoOutlook("AAMk-1", null));
        await Enviar().Handle(new EnviarReunionAOutlookCommand(r.Id, true, Url), default);

        await _outlook.Received().CrearAsync(_organizador.Id, Arg.Any<EventoOutlook>(), transaccion!.Value, Arg.Any<CancellationToken>());
    }

    // ── Sincronizar ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Guardar_sin_cambios_visibles_no_llama_a_Graph()
    {
        var r = await EnviadaAsync();

        await Sincronizar().Handle(new SincronizarReunionOutlookCommand(r.Id, Url), default);

        await _outlook.DidNotReceiveWithAnyArgs().ActualizarAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task Un_cambio_de_hora_actualiza_con_la_conexion_del_organizador_aunque_edite_otro()
    {
        var r = await EnviadaAsync();
        _usuario.UserId.Returns(_editor.Id);
        r.Hora = "10:30";
        await _ctx.SaveChangesAsync();

        await Sincronizar().Handle(new SincronizarReunionOutlookCommand(r.Id, Url), default);

        await _outlook.Received(1).ActualizarAsync(_organizador.Id, "AAMk-1",
            Arg.Is<EventoOutlook>(e => e.Inicio.Hour == 10 && e.Inicio.Minute == 30), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Si_Graph_falla_al_actualizar_queda_pendiente_y_no_lanza()
    {
        var r = await EnviadaAsync();
        r.Hora = "11:00";
        await _ctx.SaveChangesAsync();
        _outlook.ActualizarAsync(default, default!, default!, default)
            .ThrowsAsyncForAnyArgs(new OutlookNoDisponibleException("timeout"));

        await Sincronizar().Handle(new SincronizarReunionOutlookCommand(r.Id, Url), default);

        var fila = await _ctx.Reuniones.AsNoTracking().SingleAsync();
        fila.OutlookPendiente.Should().BeTrue();
        fila.OutlookError.Should().Contain("timeout");
    }

    [Fact]
    public async Task Con_la_conexion_del_organizador_vencida_el_pendiente_lo_nombra()
    {
        var r = await EnviadaAsync();
        r.Hora = "11:00";
        await _ctx.SaveChangesAsync();
        _outlook.ActualizarAsync(default, default!, default!, default)
            .ThrowsAsyncForAnyArgs(new OutlookConexionVencidaException("invalid_grant"));

        await Sincronizar().Handle(new SincronizarReunionOutlookCommand(r.Id, Url), default);

        (await _ctx.Reuniones.AsNoTracking().SingleAsync()).OutlookError
            .Should().Contain("Ana Organizadora").And.Contain("reconectarse");
    }

    [Fact]
    public async Task Un_pendiente_se_reintenta_aunque_la_huella_no_haya_cambiado()
    {
        var r = await EnviadaAsync();
        r.MarcarOutlookPendiente("falló antes");
        await _ctx.SaveChangesAsync();

        await Sincronizar().Handle(new SincronizarReunionOutlookCommand(r.Id, Url), default);

        await _outlook.ReceivedWithAnyArgs(1).ActualizarAsync(default, default!, default!, default);
        (await _ctx.Reuniones.AsNoTracking().SingleAsync()).OutlookPendiente.Should().BeFalse();
    }

    [Fact]
    public async Task Si_le_borran_la_hora_queda_pendiente_con_el_motivo_y_no_llama_a_Graph()
    {
        var r = await EnviadaAsync();
        r.Hora = null;
        await _ctx.SaveChangesAsync();

        await Sincronizar().Handle(new SincronizarReunionOutlookCommand(r.Id, Url), default);

        var fila = await _ctx.Reuniones.AsNoTracking().SingleAsync();
        fila.OutlookPendiente.Should().BeTrue();
        fila.OutlookError.Should().Contain("fecha y hora");
        await _outlook.DidNotReceiveWithAnyArgs().ActualizarAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task Una_reunion_que_no_esta_en_Outlook_no_se_sincroniza()
    {
        var r = Sembrar();

        await Sincronizar().Handle(new SincronizarReunionOutlookCommand(r.Id, Url), default);

        _outlook.ReceivedCalls().Should().BeEmpty();
    }

    // ── Cancelar ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancelar_avisa_a_Graph_con_la_conexion_del_organizador()
    {
        var r = await EnviadaAsync();

        await Cancelar().Handle(new CancelarReunionOutlookCommand(r.Id), default);

        await _outlook.Received(1).CancelarAsync(_organizador.Id, "AAMk-1", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Si_no_se_puede_cancelar_se_dice_que_se_cancele_a_mano()
    {
        var r = await EnviadaAsync();
        _outlook.CancelarAsync(default, default!, default!, default)
            .ThrowsAsyncForAnyArgs(new OutlookNoDisponibleException("503"));

        var acto = () => Cancelar().Handle(new CancelarReunionOutlookCommand(r.Id), default);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*a mano*");
    }

    // ── Estado ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task El_estado_dice_si_el_usuario_puede_enviar_y_quien_organiza()
    {
        var r = await EnviadaAsync();

        var e = await new GetEstadoOutlookQueryHandler(_ctx, _usuario, _conexion)
            .Handle(new GetEstadoOutlookQuery(r.Id), default);

        e.Configurado.Should().BeTrue();
        e.UsuarioConectado.Should().BeTrue();
        e.EnOutlook.Should().BeTrue();
        e.Organizador.Should().Be("Ana Organizadora");
        e.EnlaceTeams.Should().Be("https://teams/abc");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~OutlookCommandsTests"`
Expected: build FAIL — `EnviarReunionAOutlookCommandHandler` no existe.

- [ ] **Step 3: Implement the commands**

`src/Application/Reuniones/Outlook/OutlookCommands.cs`:

```csharp
using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Models;

namespace Diger.TramitesEstado.Application.Reuniones.Outlook;

/// <summary>Primer envío de la reunión al calendario del usuario actual, que pasa a ser su
/// organizador en Outlook. Cualquier problema vuelve como DomainException con el motivo.</summary>
public sealed record EnviarReunionAOutlookCommand(int ReunionId, bool ConTeams, string UrlFicha) : IRequest<Unit>;

public sealed class EnviarReunionAOutlookCommandHandler(
    IApplicationDbContext ctx,
    IReunionRepository repo,
    ICurrentUserService currentUser,
    IOutlookCalendario outlook,
    IUnitOfWork uow)
    : IRequestHandler<EnviarReunionAOutlookCommand, Unit>
{
    public async Task<Unit> Handle(EnviarReunionAOutlookCommand cmd, CancellationToken ct)
    {
        var usuarioId = currentUser.UserId
            ?? throw new DomainException("No hay una sesión de usuario para enviar a Outlook.");

        var conexion = await ctx.ConexionesMicrosoft.AsNoTracking().FirstOrDefaultAsync(c => c.Id == usuarioId, ct)
            ?? throw new DomainException("Conecte su Outlook desde «Mi perfil» antes de enviar reuniones.");
        if (conexion.RequiereReconexion)
            throw new DomainException("Su conexión de Outlook venció. Reconéctela desde «Mi perfil» y vuelva a enviar.");

        var r = await repo.GetByIdWithDetailsAsync(cmd.ReunionId, ct)
            ?? throw new NotFoundException(nameof(Reunion), cmd.ReunionId);

        var transaccion = r.PrepararEnvioOutlook(cmd.ConTeams);
        var evento = EventoOutlookBuilder.Construir(r, conexion.Upn, cmd.UrlFicha); // rechaza antes de tocar Graph

        // La transacción se guarda ANTES de llamar a Graph: si Graph crea la cita y lo que falla
        // es guardar después, el reintento manda el mismo transactionId y Graph no la duplica.
        await uow.SaveChangesAsync(ct);

        ResultadoOutlook res;
        try
        {
            res = await outlook.CrearAsync(usuarioId, evento, transaccion, ct);
        }
        catch (OutlookConexionVencidaException)
        {
            throw new DomainException("Su conexión de Outlook venció. Reconéctela desde «Mi perfil» y vuelva a enviar.");
        }
        catch (OutlookNoDisponibleException ex)
        {
            throw new DomainException($"Outlook no respondió ({ex.Message}). Puede volver a intentarlo: no se duplicará la cita.");
        }

        r.RegistrarEnvioOutlook(res.EventId, usuarioId, res.EnlaceTeams, evento.Huella(), DateTime.UtcNow);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

/// <summary>Después de guardar: si la cita cambió para los invitados, se actualiza en Outlook con
/// la conexión del ORGANIZADOR. Nunca lanza por Outlook: un fallo queda como pendiente.</summary>
public sealed record SincronizarReunionOutlookCommand(int ReunionId, string UrlFicha) : IRequest<Unit>;

public sealed class SincronizarReunionOutlookCommandHandler(
    IApplicationDbContext ctx,
    IReunionRepository repo,
    IOutlookCalendario outlook,
    IUnitOfWork uow)
    : IRequestHandler<SincronizarReunionOutlookCommand, Unit>
{
    public async Task<Unit> Handle(SincronizarReunionOutlookCommand cmd, CancellationToken ct)
    {
        var r = await repo.GetByIdWithDetailsAsync(cmd.ReunionId, ct)
            ?? throw new NotFoundException(nameof(Reunion), cmd.ReunionId);
        if (!r.EstaEnOutlook) return Unit.Value;

        var organizadorId = r.OutlookOrganizadorId!.Value;
        var conexion = await ctx.ConexionesMicrosoft.AsNoTracking().FirstOrDefaultAsync(c => c.Id == organizadorId, ct);

        EventoOutlook evento;
        try
        {
            evento = EventoOutlookBuilder.Construir(r, conexion?.Upn, cmd.UrlFicha);
        }
        catch (DomainException ex)
        {
            r.MarcarOutlookPendiente($"No se actualizó en Outlook: {ex.Message}");
            await uow.SaveChangesAsync(ct);
            return Unit.Value;
        }

        var huella = evento.Huella();
        if (huella == r.OutlookHuella && !r.OutlookPendiente) return Unit.Value;

        try
        {
            if (conexion is null || conexion.RequiereReconexion)
                throw new OutlookConexionVencidaException("sin conexión vigente");

            var res = await outlook.ActualizarAsync(organizadorId, r.OutlookEventId!, evento, ct);
            r.RegistrarSincronizacionOutlook(res.EnlaceTeams, huella, DateTime.UtcNow);
        }
        catch (OutlookConexionVencidaException)
        {
            var nombre = await ctx.Usuarios.AsNoTracking().Where(u => u.Id == organizadorId)
                .Select(u => u.Nombre).FirstOrDefaultAsync(ct) ?? "quien la envió";
            r.MarcarOutlookPendiente($"La conexión de Outlook de {nombre} venció; debe reconectarse desde su perfil.");
        }
        catch (OutlookNoDisponibleException ex)
        {
            r.MarcarOutlookPendiente($"No se pudo actualizar en Outlook: {ex.Message}");
        }

        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

/// <summary>Antes de borrar la reunión. Si no puede cancelar lanza DomainException con el aviso;
/// quien llama decide borrar igual (el portal no bloquea un borrado por Microsoft).</summary>
public sealed record CancelarReunionOutlookCommand(int ReunionId) : IRequest<Unit>;

public sealed class CancelarReunionOutlookCommandHandler(IReunionRepository repo, IOutlookCalendario outlook)
    : IRequestHandler<CancelarReunionOutlookCommand, Unit>
{
    public async Task<Unit> Handle(CancelarReunionOutlookCommand cmd, CancellationToken ct)
    {
        var r = await repo.GetByIdAsync(cmd.ReunionId, ct);
        if (r is null || !r.EstaEnOutlook) return Unit.Value;

        try
        {
            await outlook.CancelarAsync(r.OutlookOrganizadorId!.Value, r.OutlookEventId!,
                $"La reunión se canceló en {Marca.Nombre}.", ct);
        }
        catch (Exception ex) when (ex is OutlookConexionVencidaException or OutlookNoDisponibleException)
        {
            throw new DomainException("No se pudo cancelar la cita en Outlook: cancélela a mano desde el calendario del organizador.");
        }

        return Unit.Value;
    }
}
```

- [ ] **Step 4: Implement the status query**

`src/Application/Reuniones/Outlook/GetEstadoOutlookQuery.cs`:

```csharp
using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Reuniones.Outlook;

public sealed record EstadoOutlookDto(
    bool Configurado,
    bool UsuarioConectado,
    bool UsuarioRequiereReconexion,
    bool EnOutlook,
    bool ConTeams,
    string? EnlaceTeams,
    bool Pendiente,
    string? Error,
    DateTime? SincronizadaEl,
    string? Organizador);

public sealed record GetEstadoOutlookQuery(int ReunionId) : IRequest<EstadoOutlookDto>;

public sealed class GetEstadoOutlookQueryHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser,
    IConexionMicrosoft conexiones)
    : IRequestHandler<GetEstadoOutlookQuery, EstadoOutlookDto>
{
    public async Task<EstadoOutlookDto> Handle(GetEstadoOutlookQuery q, CancellationToken ct)
    {
        // Pasa por el filtro de alcance: quien no ve la reunión no ve su estado.
        var r = await ctx.Reuniones.AsNoTracking()
            .Where(x => x.Id == q.ReunionId)
            .Select(x => new
            {
                x.OutlookEventId, x.OutlookConTeams, x.OutlookEnlaceTeams, x.OutlookPendiente,
                x.OutlookError, x.OutlookSincronizadaEl, x.OutlookOrganizadorId
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Reunion), q.ReunionId);

        var mia = currentUser.UserId is { } uid
            ? await ctx.ConexionesMicrosoft.AsNoTracking().Where(c => c.Id == uid)
                .Select(c => new { c.RequiereReconexion }).FirstOrDefaultAsync(ct)
            : null;

        var organizador = r.OutlookOrganizadorId is { } oid
            ? await ctx.Usuarios.AsNoTracking().Where(u => u.Id == oid).Select(u => u.Nombre).FirstOrDefaultAsync(ct)
            : null;

        return new EstadoOutlookDto(
            Configurado:               conexiones.EstaConfigurada,
            UsuarioConectado:          mia is not null,
            UsuarioRequiereReconexion: mia?.RequiereReconexion ?? false,
            EnOutlook:                 r.OutlookEventId is not null,
            ConTeams:                  r.OutlookConTeams,
            EnlaceTeams:               r.OutlookEnlaceTeams,
            Pendiente:                 r.OutlookPendiente,
            Error:                     r.OutlookError,
            SincronizadaEl:            r.OutlookSincronizadaEl,
            Organizador:               organizador);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~OutlookCommandsTests"`
Expected: PASS (14 tests). Si `Usuario.Crear` no deja `Id` asignado antes de guardar, el `SaveChanges` del constructor lo asigna (el dominio pone `Id = Guid.NewGuid()`): no hace falta cambiar nada.

- [ ] **Step 6: Commit**

```bash
git add src/Application/Reuniones/Outlook/OutlookCommands.cs src/Application/Reuniones/Outlook/GetEstadoOutlookQuery.cs tests/Application.Tests/Reuniones/Outlook/OutlookCommandsTests.cs
git commit -m "Enviar, actualizar y cancelar la cita de Outlook sin que guardar o borrar dependan de Microsoft"
```

---

### Task 6: Conexión Microsoft con MSAL (Infrastructure + Application)

**Files:**
- Modify: `src/Infrastructure/Diger.TramitesEstado.Infrastructure.csproj` (paquete MSAL)
- Create: `src/Infrastructure/Outlook/MicrosoftGraphOptions.cs`
- Create: `src/Infrastructure/Outlook/CacheTokensProtector.cs`
- Create: `src/Infrastructure/Outlook/ConexionMicrosoftService.cs`
- Create: `src/Application/CuentaMicrosoft/ConexionMicrosoftCommands.cs`
- Modify: `src/Infrastructure/DependencyInjection.cs`
- Modify: `src/Web/appsettings.json`
- Test: `tests/Application.Tests/Outlook/CacheTokensProtectorTests.cs`, `tests/Application.Tests/Outlook/ConexionMicrosoftCommandsTests.cs`

**Interfaces:**
- Consumes: `IConexionMicrosoft`, `InicioConexionDto` (Task 4); `ConexionMicrosoft` (Task 1).
- Produces:
  - `public sealed class MicrosoftGraphOptions { public const string Seccion = "MicrosoftGraph"; string TenantId; string ClientId; string ClientSecret; bool EstaCompleta; }`
  - `public sealed class CacheTokensProtector(IDataProtectionProvider dp) { byte[] Cifrar(byte[] claro); byte[]? Descifrar(byte[] cifrado); }`
  - Application: `IniciarConexionOutlookQuery(string UrlRetorno) : IRequest<InicioConexionDto>`, `CompletarConexionOutlookCommand(string Codigo, string Verificador, string UrlRetorno) : IRequest<Unit>`, `DesconectarOutlookCommand() : IRequest<Unit>`, `GetMiConexionOutlookQuery() : IRequest<MiConexionOutlookDto>`, `MiConexionOutlookDto(bool Configurada, bool Conectada, string? Upn, DateTime? ConectadaEl, bool RequiereReconexion)`.

- [ ] **Step 1: Add MSAL**

Run: `dotnet add src\Infrastructure package Microsoft.Identity.Client`
Expected: el `.csproj` gana `<PackageReference Include="Microsoft.Identity.Client" Version="4.x.y" />`. Anotar la versión en el mensaje del commit.

- [ ] **Step 2: Write the failing tests**

`tests/Application.Tests/Outlook/CacheTokensProtectorTests.cs`:

```csharp
using System.Text;
using Diger.TramitesEstado.Infrastructure.Outlook;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Outlook;

/// <summary>La caché de MSAL lleva el refresh token: en la base tiene que ir ilegible, y una caché
/// que ya no se puede descifrar (llaves perdidas) es «reconectar», no un error 500.</summary>
public class CacheTokensProtectorTests
{
    private readonly CacheTokensProtector _p = new(new EphemeralDataProtectionProvider());

    [Fact]
    public void Lo_que_se_guarda_no_contiene_el_texto_original()
    {
        var claro = Encoding.UTF8.GetBytes("{\"RefreshToken\":\"0.AAAA-secreto\"}");

        var cifrado = _p.Cifrar(claro);

        Encoding.UTF8.GetString(cifrado).Should().NotContain("secreto");
        _p.Descifrar(cifrado).Should().Equal(claro);
    }

    [Fact]
    public void Una_cache_que_no_se_puede_descifrar_devuelve_null()
    {
        var otro = new CacheTokensProtector(new EphemeralDataProtectionProvider());
        var ajeno = otro.Cifrar([1, 2, 3]);

        _p.Descifrar(ajeno).Should().BeNull();
    }

    [Fact]
    public void Una_cache_vacia_es_nula_sin_lanzar()
    {
        _p.Descifrar([]).Should().BeNull();
    }
}
```

`tests/Application.Tests/Outlook/ConexionMicrosoftCommandsTests.cs`:

```csharp
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.CuentaMicrosoft;
using Diger.TramitesEstado.Application.Reuniones.Outlook;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Outlook;

public class ConexionMicrosoftCommandsTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();
    private readonly IConexionMicrosoft _conexion = Substitute.For<IConexionMicrosoft>();
    private readonly Usuario _ana = Usuario.Crear("Ana", "ana@diger.gob.hn", "hash");

    public ConexionMicrosoftCommandsTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _usuario.EsGlobal.Returns(true);
        _usuario.UserId.Returns(_ana.Id);
        _ctx = new AppDbContext(opts, _usuario, Substitute.For<MediatR.IPublisher>());
        _ctx.Usuarios.Add(_ana);
        _ctx.SaveChanges();
        _conexion.EstaConfigurada.Returns(true);
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public async Task Completar_canjea_para_el_usuario_de_la_sesion()
    {
        await new CompletarConexionOutlookCommandHandler(_conexion, _usuario)
            .Handle(new CompletarConexionOutlookCommand("codigo", "verif", "https://x/cb"), default);

        await _conexion.Received(1).CompletarAsync(_ana.Id, "codigo", "verif", "https://x/cb", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Desconectar_borra_la_fila_del_usuario()
    {
        _ctx.ConexionesMicrosoft.Add(ConexionMicrosoft.Crear(_ana.Id, "ana@diger.gob.hn", "h", [1], DateTime.UtcNow));
        await _ctx.SaveChangesAsync();

        await new DesconectarOutlookCommandHandler(_ctx, _usuario, _ctx).Handle(new DesconectarOutlookCommand(), default);

        (await _ctx.ConexionesMicrosoft.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Mi_conexion_sin_fila_dice_no_conectada_pero_configurada()
    {
        var dto = await new GetMiConexionOutlookQueryHandler(_ctx, _usuario, _conexion)
            .Handle(new GetMiConexionOutlookQuery(), default);

        dto.Configurada.Should().BeTrue();
        dto.Conectada.Should().BeFalse();
        dto.Upn.Should().BeNull();
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~Outlook.CacheTokensProtectorTests|FullyQualifiedName~ConexionMicrosoftCommandsTests"`
Expected: build FAIL — `CacheTokensProtector` y `CompletarConexionOutlookCommandHandler` no existen.

- [ ] **Step 4: Options and protector**

`src/Infrastructure/Outlook/MicrosoftGraphOptions.cs`:

```csharp
namespace Diger.TramitesEstado.Infrastructure.Outlook;

/// <summary>
/// Registro de la aplicación en Entra ID (lo entrega TI). TenantId y ClientId no son secretos;
/// ClientSecret sí: User Secrets en desarrollo, configuración del servidor en producción, nunca un
/// appsettings versionado. Mientras falte cualquiera de los tres, la integración se apaga sola.
/// </summary>
public sealed class MicrosoftGraphOptions
{
    public const string Seccion = "MicrosoftGraph";

    public string TenantId     { get; set; } = string.Empty;
    public string ClientId     { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    public bool EstaCompleta =>
        !string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
```

`src/Infrastructure/Outlook/CacheTokensProtector.cs`:

```csharp
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Diger.TramitesEstado.Infrastructure.Outlook;

/// <summary>Cifra la caché de MSAL (que lleva el refresh token) con un propósito exclusivo.</summary>
public sealed class CacheTokensProtector(IDataProtectionProvider dp)
{
    private readonly IDataProtector _p = dp.CreateProtector("ConexionesMicrosoft.CacheTokens");

    public byte[] Cifrar(byte[] claro) => _p.Protect(claro);

    /// <summary>null si no se puede descifrar (llaves perdidas o datos ajenos): el llamador lo
    /// trata como conexión vencida.</summary>
    public byte[]? Descifrar(byte[] cifrado)
    {
        if (cifrado.Length == 0) return null;
        try { return _p.Unprotect(cifrado); }
        catch (CryptographicException) { return null; }
    }
}
```

- [ ] **Step 5: The MSAL service**

`src/Infrastructure/Outlook/ConexionMicrosoftService.cs`:

```csharp
using System.Security.Cryptography;
using Diger.TramitesEstado.Application.Reuniones.Outlook;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;

namespace Diger.TramitesEstado.Infrastructure.Outlook;

/// <summary>
/// OAuth delegado con MSAL (authorization code + PKCE). Un ConfidentialClientApplication por
/// operación, con la caché enganchada a la fila del usuario: MSAL renueva el token en silencio y
/// la caché nueva se cifra y se guarda en la misma operación.
///
/// <para>Nunca registra tokens ni la caché. Los logs llevan el id del usuario y el tipo de error.</para>
/// </summary>
public sealed class ConexionMicrosoftService(
    IOptions<MicrosoftGraphOptions> opciones,
    AppDbContext ctx,
    CacheTokensProtector protector,
    ILogger<ConexionMicrosoftService> log) : IConexionMicrosoft
{
    // offline_access, openid y profile los agrega MSAL: pasarlos aquí es un error de MSAL.
    internal static readonly string[] Alcances =
    [
        "https://graph.microsoft.com/Calendars.ReadWrite",
        "https://graph.microsoft.com/User.Read"
    ];

    private readonly MicrosoftGraphOptions _o = opciones.Value;

    public bool EstaConfigurada => _o.EstaCompleta;

    private IConfidentialClientApplication Cliente(string? urlRetorno, byte[]? cacheClara, Action<byte[]>? alCambiar)
    {
        if (!_o.EstaCompleta)
            throw new OutlookNoDisponibleException("La integración con Outlook no está configurada en este servidor.");

        var b = ConfidentialClientApplicationBuilder.Create(_o.ClientId)
            .WithTenantId(_o.TenantId)
            .WithClientSecret(_o.ClientSecret);
        if (urlRetorno is not null) b = b.WithRedirectUri(urlRetorno);
        var app = b.Build();

        app.UserTokenCache.SetBeforeAccess(args =>
        {
            if (cacheClara is not null) args.TokenCache.DeserializeMsalV3(cacheClara);
        });
        app.UserTokenCache.SetAfterAccess(args =>
        {
            if (args.HasStateChanged) alCambiar?.Invoke(args.TokenCache.SerializeMsalV3());
        });
        return app;
    }

    public async Task<InicioConexionDto> IniciarAsync(string urlRetorno, CancellationToken ct)
    {
        var estado = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var url = await Cliente(urlRetorno, null, null)
            .GetAuthorizationRequestUrl(Alcances)
            .WithPkce(out var verificador)
            .WithPrompt(Prompt.SelectAccount)
            .WithExtraQueryParameters(new Dictionary<string, string> { ["state"] = estado })
            .ExecuteAsync(ct);
        return new InicioConexionDto(url.ToString(), estado, verificador);
    }

    public async Task CompletarAsync(Guid usuarioId, string codigo, string verificador, string urlRetorno, CancellationToken ct)
    {
        byte[]? cacheNueva = null;
        var app = Cliente(urlRetorno, null, c => cacheNueva = c);

        AuthenticationResult res;
        try
        {
            res = await app.AcquireTokenByAuthorizationCode(Alcances, codigo)
                .WithPkceCodeVerifier(verificador)
                .ExecuteAsync(ct);
        }
        catch (MsalException ex)
        {
            log.LogWarning("Conexión Outlook rechazada para {UsuarioId}: {Codigo}", usuarioId, ex.ErrorCode);
            throw new OutlookNoDisponibleException($"Microsoft rechazó la conexión ({ex.ErrorCode}).");
        }

        var cifrada = protector.Cifrar(cacheNueva ?? []);
        var fila = await ctx.ConexionesMicrosoft.FirstOrDefaultAsync(c => c.Id == usuarioId, ct);
        if (fila is null)
            ctx.ConexionesMicrosoft.Add(ConexionMicrosoft.Crear(usuarioId, res.Account.Username, res.Account.HomeAccountId.Identifier, cifrada, DateTime.UtcNow));
        else
            fila.Reconectar(res.Account.Username, res.Account.HomeAccountId.Identifier, cifrada, DateTime.UtcNow);

        await ctx.SaveChangesAsync(ct);
    }

    public async Task<string> ObtenerTokenAsync(Guid usuarioId, CancellationToken ct)
    {
        var fila = await ctx.ConexionesMicrosoft.FirstOrDefaultAsync(c => c.Id == usuarioId, ct)
            ?? throw new OutlookConexionVencidaException("El usuario no tiene Outlook conectado.");
        if (fila.RequiereReconexion)
            throw new OutlookConexionVencidaException(fila.UltimoError ?? "Requiere reconexión.");

        var clara = protector.Descifrar(fila.CacheTokens);
        if (clara is null)
            return await VencerAsync(fila, "La caché no se pudo descifrar (¿cambiaron las llaves del servidor?).", ct);

        byte[]? cacheNueva = null;
        var app = Cliente(null, clara, c => cacheNueva = c);

        try
        {
            var cuenta = await app.GetAccountAsync(fila.HomeAccountId);
            if (cuenta is null)
                return await VencerAsync(fila, "La cuenta no está en la caché.", ct);

            var res = await app.AcquireTokenSilent(Alcances, cuenta).ExecuteAsync(ct);
            if (cacheNueva is not null)
            {
                fila.GuardarCache(protector.Cifrar(cacheNueva));
                await ctx.SaveChangesAsync(ct);
            }
            return res.AccessToken;
        }
        catch (MsalUiRequiredException ex)
        {
            return await VencerAsync(fila, ex.ErrorCode, ct);
        }
        catch (MsalServiceException ex)
        {
            log.LogWarning("Microsoft no entregó token para {UsuarioId}: {Codigo}", usuarioId, ex.ErrorCode);
            throw new OutlookNoDisponibleException($"Microsoft no respondió ({ex.ErrorCode}).");
        }
    }

    private async Task<string> VencerAsync(ConexionMicrosoft fila, string motivo, CancellationToken ct)
    {
        fila.MarcarVencida(motivo);
        await ctx.SaveChangesAsync(ct);
        log.LogInformation("Conexión Outlook vencida para {UsuarioId}: {Motivo}", fila.Id, motivo);
        throw new OutlookConexionVencidaException(motivo);
    }
}
```

- [ ] **Step 6: Application commands for the profile**

`src/Application/CuentaMicrosoft/ConexionMicrosoftCommands.cs`:

```csharp
using Diger.TramitesEstado.Application.Reuniones.Outlook;

namespace Diger.TramitesEstado.Application.CuentaMicrosoft;

public sealed record MiConexionOutlookDto(bool Configurada, bool Conectada, string? Upn, DateTime? ConectadaEl, bool RequiereReconexion);

/// <summary>Arma la URL de Microsoft. No escribe nada: el estado y el verificador los guarda la página.</summary>
public sealed record IniciarConexionOutlookQuery(string UrlRetorno) : IRequest<InicioConexionDto>;

public sealed class IniciarConexionOutlookQueryHandler(IConexionMicrosoft conexion)
    : IRequestHandler<IniciarConexionOutlookQuery, InicioConexionDto>
{
    public Task<InicioConexionDto> Handle(IniciarConexionOutlookQuery q, CancellationToken ct) =>
        conexion.IniciarAsync(q.UrlRetorno, ct);
}

public sealed record CompletarConexionOutlookCommand(string Codigo, string Verificador, string UrlRetorno) : IRequest<Unit>;

public sealed class CompletarConexionOutlookCommandHandler(IConexionMicrosoft conexion, ICurrentUserService currentUser)
    : IRequestHandler<CompletarConexionOutlookCommand, Unit>
{
    public async Task<Unit> Handle(CompletarConexionOutlookCommand cmd, CancellationToken ct)
    {
        var usuarioId = currentUser.UserId
            ?? throw new DomainException("No hay una sesión de usuario para conectar Outlook.");
        try
        {
            await conexion.CompletarAsync(usuarioId, cmd.Codigo, cmd.Verificador, cmd.UrlRetorno, ct);
        }
        catch (OutlookNoDisponibleException ex)
        {
            throw new DomainException($"No se pudo conectar Outlook: {ex.Message}");
        }
        return Unit.Value;
    }
}

/// <summary>Borra la conexión propia. Las citas ya enviadas siguen en Outlook.</summary>
public sealed record DesconectarOutlookCommand : IRequest<Unit>;

public sealed class DesconectarOutlookCommandHandler(IApplicationDbContext ctx, ICurrentUserService currentUser, IUnitOfWork uow)
    : IRequestHandler<DesconectarOutlookCommand, Unit>
{
    public async Task<Unit> Handle(DesconectarOutlookCommand cmd, CancellationToken ct)
    {
        if (currentUser.UserId is not { } uid) return Unit.Value;
        var fila = await ctx.ConexionesMicrosoft.FirstOrDefaultAsync(c => c.Id == uid, ct);
        if (fila is null) return Unit.Value;
        ctx.ConexionesMicrosoft.Remove(fila);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed record GetMiConexionOutlookQuery : IRequest<MiConexionOutlookDto>;

public sealed class GetMiConexionOutlookQueryHandler(IApplicationDbContext ctx, ICurrentUserService currentUser, IConexionMicrosoft conexion)
    : IRequestHandler<GetMiConexionOutlookQuery, MiConexionOutlookDto>
{
    public async Task<MiConexionOutlookDto> Handle(GetMiConexionOutlookQuery q, CancellationToken ct)
    {
        var fila = currentUser.UserId is { } uid
            ? await ctx.ConexionesMicrosoft.AsNoTracking().Where(c => c.Id == uid)
                .Select(c => new { c.Upn, c.ConectadaEl, c.RequiereReconexion }).FirstOrDefaultAsync(ct)
            : null;

        return new MiConexionOutlookDto(conexion.EstaConfigurada, fila is not null, fila?.Upn, fila?.ConectadaEl, fila?.RequiereReconexion ?? false);
    }
}
```

Nota: la carpeta se llama `CuentaMicrosoft` y no `ConexionMicrosoft` a propósito. Un namespace `Diger.TramitesEstado.Application.ConexionMicrosoft` taparía a la entidad `ConexionMicrosoft` en todo código que viva bajo `Diger.TramitesEstado.Application.*` —incluido el proyecto de pruebas—, porque C# resuelve los namespaces que encierran al archivo antes que los `using`.

- [ ] **Step 7: Register in DI and configuration**

En `src/Infrastructure/DependencyInjection.cs`, `using Diger.TramitesEstado.Infrastructure.Outlook;` y, después de `services.Configure<SolOptions>(...)`:

```csharp
        // Reuniones a Outlook (fase 1). Sin TenantId/ClientId/ClientSecret la integración se
        // apaga sola: la UI lo dice y no se llama a Microsoft.
        services.Configure<MicrosoftGraphOptions>(configuration.GetSection(MicrosoftGraphOptions.Seccion));
        services.AddSingleton<CacheTokensProtector>();
        services.AddScoped<IConexionMicrosoft, ConexionMicrosoftService>();
```

En `src/Web/appsettings.json`, sección nueva al nivel raíz:

```json
  "MicrosoftGraph": {
    "TenantId": "a28bd482-1bfe-46d3-9a02-ccd7f27b83bf",
    "ClientId": "",
    "ClientSecret": ""
  },
```

- [ ] **Step 8: Run tests to verify they pass, and build**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~Outlook"` y `dotnet build Diger.TramitesEstado.sln`
Expected: PASS; 0 errores.

- [ ] **Step 9: Commit**

```bash
git add src/Infrastructure/Diger.TramitesEstado.Infrastructure.csproj src/Infrastructure/Outlook/MicrosoftGraphOptions.cs src/Infrastructure/Outlook/CacheTokensProtector.cs src/Infrastructure/Outlook/ConexionMicrosoftService.cs src/Application/CuentaMicrosoft/ConexionMicrosoftCommands.cs src/Infrastructure/DependencyInjection.cs src/Web/appsettings.json tests/Application.Tests/Outlook
git commit -m "Cada usuario conecta su cuenta Microsoft; la cache de tokens se guarda cifrada"
```

---

### Task 7: Cliente de Graph

**Files:**
- Create: `src/Infrastructure/Outlook/GraphOutlookCalendario.cs`
- Modify: `src/Infrastructure/DependencyInjection.cs`
- Test: `tests/Application.Tests/Outlook/GraphOutlookCalendarioTests.cs`

**Interfaces:**
- Consumes: `IOutlookCalendario`, `EventoOutlook`, `IConexionMicrosoft.ObtenerTokenAsync`.
- Produces: `public sealed class GraphOutlookCalendario(HttpClient http, IConexionMicrosoft conexion) : IOutlookCalendario`. `http.BaseAddress` = `https://graph.microsoft.com/v1.0/`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Net;
using System.Text.Json;
using Diger.TramitesEstado.Application.Reuniones.Outlook;
using Diger.TramitesEstado.Infrastructure.Outlook;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Outlook;

public class GraphOutlookCalendarioTests
{
    private sealed class Manejador(HttpStatusCode codigo, string cuerpo) : HttpMessageHandler
    {
        public HttpRequestMessage? Pedido;
        public string? CuerpoEnviado;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Pedido = r;
            CuerpoEnviado = r.Content is null ? null : await r.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(codigo) { Content = new StringContent(cuerpo) };
        }
    }

    private static readonly Guid Org = Guid.NewGuid();
    private static readonly EventoOutlook Evento = new("Mesa técnica", new DateTime(2026, 10, 23, 9, 0, 0),
        new DateTime(2026, 10, 23, 10, 0, 0), "Sala 2", "<p>x</p>", [new("ana@ihtt.gob.hn", "Ana")], true);

    private static (GraphOutlookCalendario cal, Manejador m) Armar(HttpStatusCode codigo, string cuerpo = "{}")
    {
        var m = new Manejador(codigo, cuerpo);
        var conexion = Substitute.For<IConexionMicrosoft>();
        conexion.ObtenerTokenAsync(Org, Arg.Any<CancellationToken>()).Returns("tok-123");
        var http = new HttpClient(m) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        return (new GraphOutlookCalendario(http, conexion), m);
    }

    [Fact]
    public async Task Crear_manda_el_evento_completo_con_transaccion_y_Teams()
    {
        var (cal, m) = Armar(HttpStatusCode.Created, """{"id":"AAMk-1","onlineMeeting":{"joinUrl":"https://teams/abc"}}""");
        var tx = Guid.NewGuid();

        var res = await cal.CrearAsync(Org, Evento, tx, default);

        res.Should().Be(new ResultadoOutlook("AAMk-1", "https://teams/abc"));
        m.Pedido!.Method.Should().Be(HttpMethod.Post);
        m.Pedido.RequestUri!.ToString().Should().Be("https://graph.microsoft.com/v1.0/me/events");
        m.Pedido.Headers.Authorization!.ToString().Should().Be("Bearer tok-123");

        using var j = JsonDocument.Parse(m.CuerpoEnviado!);
        var raiz = j.RootElement;
        raiz.GetProperty("transactionId").GetString().Should().Be(tx.ToString());
        raiz.GetProperty("isOnlineMeeting").GetBoolean().Should().BeTrue();
        raiz.GetProperty("onlineMeetingProvider").GetString().Should().Be("teamsForBusiness");
        raiz.GetProperty("start").GetProperty("dateTime").GetString().Should().Be("2026-10-23T09:00:00");
        raiz.GetProperty("start").GetProperty("timeZone").GetString().Should().Be("Central America Standard Time");
        raiz.GetProperty("location").GetProperty("displayName").GetString().Should().Be("Sala 2");
        raiz.GetProperty("attendees")[0].GetProperty("emailAddress").GetProperty("address").GetString().Should().Be("ana@ihtt.gob.hn");
        raiz.GetProperty("body").GetProperty("contentType").GetString().Should().Be("HTML");
    }

    [Fact]
    public async Task Sin_Teams_no_pide_reunion_en_linea()
    {
        var (cal, m) = Armar(HttpStatusCode.Created, """{"id":"AAMk-1"}""");

        var res = await cal.CrearAsync(Org, Evento with { EsTeams = false }, Guid.NewGuid(), default);

        res.EnlaceTeams.Should().BeNull();
        JsonDocument.Parse(m.CuerpoEnviado!).RootElement.TryGetProperty("isOnlineMeeting", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Actualizar_hace_PATCH_sin_transaccion()
    {
        var (cal, m) = Armar(HttpStatusCode.OK, """{"id":"AAMk-1","onlineMeeting":{"joinUrl":"https://teams/abc"}}""");

        await cal.ActualizarAsync(Org, "AAMk-1", Evento, default);

        m.Pedido!.Method.Should().Be(HttpMethod.Patch);
        m.Pedido.RequestUri!.ToString().Should().Be("https://graph.microsoft.com/v1.0/me/events/AAMk-1");
        JsonDocument.Parse(m.CuerpoEnviado!).RootElement.TryGetProperty("transactionId", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Cancelar_hace_POST_a_cancel_con_comentario()
    {
        var (cal, m) = Armar(HttpStatusCode.Accepted);

        await cal.CancelarAsync(Org, "AAMk-1", "Se canceló.", default);

        m.Pedido!.RequestUri!.ToString().Should().Be("https://graph.microsoft.com/v1.0/me/events/AAMk-1/cancel");
        JsonDocument.Parse(m.CuerpoEnviado!).RootElement.GetProperty("comment").GetString().Should().Be("Se canceló.");
    }

    [Fact]
    public async Task Un_401_es_conexion_vencida()
    {
        var (cal, _) = Armar(HttpStatusCode.Unauthorized);

        var acto = () => cal.CrearAsync(Org, Evento, Guid.NewGuid(), default);

        await acto.Should().ThrowAsync<OutlookConexionVencidaException>();
    }

    [Fact]
    public async Task Un_404_al_actualizar_dice_que_la_cita_ya_no_existe()
    {
        var (cal, _) = Armar(HttpStatusCode.NotFound);

        var acto = () => cal.ActualizarAsync(Org, "AAMk-1", Evento, default);

        await acto.Should().ThrowAsync<OutlookNoDisponibleException>().WithMessage("*ya no existe*");
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Otros_fallos_son_no_disponible(HttpStatusCode codigo)
    {
        var (cal, _) = Armar(codigo);

        var acto = () => cal.CrearAsync(Org, Evento, Guid.NewGuid(), default);

        await acto.Should().ThrowAsync<OutlookNoDisponibleException>();
    }

    [Fact]
    public async Task Sin_red_es_no_disponible()
    {
        var conexion = Substitute.For<IConexionMicrosoft>();
        conexion.ObtenerTokenAsync(Org, Arg.Any<CancellationToken>()).Returns("tok");
        var http = new HttpClient(new FallaRed()) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };

        var acto = () => new GraphOutlookCalendario(http, conexion).CrearAsync(Org, Evento, Guid.NewGuid(), default);

        await acto.Should().ThrowAsync<OutlookNoDisponibleException>();
    }

    private sealed class FallaRed : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) =>
            throw new HttpRequestException("No such host is known.");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~GraphOutlookCalendarioTests"`
Expected: build FAIL — `GraphOutlookCalendario` no existe.

- [ ] **Step 3: Implement**

`src/Infrastructure/Outlook/GraphOutlookCalendario.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diger.TramitesEstado.Application.Reuniones.Outlook;

namespace Diger.TramitesEstado.Infrastructure.Outlook;

/// <summary>
/// Las tres llamadas a Graph que necesita el portal, sobre /me (el token es del organizador).
/// Sin SDK de Graph a propósito: son tres peticiones y así se prueban con un HttpMessageHandler.
/// </summary>
public sealed class GraphOutlookCalendario(HttpClient http, IConexionMicrosoft conexion) : IOutlookCalendario
{
    public async Task<ResultadoOutlook> CrearAsync(Guid organizadorId, EventoOutlook evento, Guid transaccionId, CancellationToken ct)
    {
        var cuerpo = Cuerpo(evento);
        cuerpo["transactionId"] = transaccionId.ToString();
        using var resp = await EnviarAsync(organizadorId, HttpMethod.Post, "me/events", cuerpo, ct);
        return await LeerAsync(resp, ct);
    }

    public async Task<ResultadoOutlook> ActualizarAsync(Guid organizadorId, string eventId, EventoOutlook evento, CancellationToken ct)
    {
        using var resp = await EnviarAsync(organizadorId, HttpMethod.Patch, $"me/events/{Uri.EscapeDataString(eventId)}", Cuerpo(evento), ct);
        return await LeerAsync(resp, ct);
    }

    public async Task CancelarAsync(Guid organizadorId, string eventId, string comentario, CancellationToken ct)
    {
        using var _ = await EnviarAsync(organizadorId, HttpMethod.Post, $"me/events/{Uri.EscapeDataString(eventId)}/cancel",
            new JsonObject { ["comment"] = comentario }, ct);
    }

    private static JsonObject Cuerpo(EventoOutlook e)
    {
        var o = new JsonObject
        {
            ["subject"]  = e.Asunto,
            ["body"]     = new JsonObject { ["contentType"] = "HTML", ["content"] = e.CuerpoHtml },
            ["start"]    = new JsonObject { ["dateTime"] = e.Inicio.ToString("yyyy-MM-ddTHH:mm:ss"), ["timeZone"] = EventoOutlook.ZonaHoraria },
            ["end"]      = new JsonObject { ["dateTime"] = e.Fin.ToString("yyyy-MM-ddTHH:mm:ss"),    ["timeZone"] = EventoOutlook.ZonaHoraria },
            ["location"] = new JsonObject { ["displayName"] = e.Ubicacion ?? string.Empty },
            ["attendees"] = new JsonArray(e.Invitados.Select(i => (JsonNode)new JsonObject
            {
                ["emailAddress"] = new JsonObject { ["address"] = i.Correo, ["name"] = i.Nombre ?? i.Correo },
                ["type"] = "required"
            }).ToArray())
        };
        if (e.EsTeams)
        {
            o["isOnlineMeeting"] = true;
            o["onlineMeetingProvider"] = "teamsForBusiness";
        }
        return o;
    }

    private async Task<HttpResponseMessage> EnviarAsync(Guid organizadorId, HttpMethod metodo, string ruta, JsonObject cuerpo, CancellationToken ct)
    {
        var token = await conexion.ObtenerTokenAsync(organizadorId, ct);
        using var pedido = new HttpRequestMessage(metodo, ruta) { Content = JsonContent.Create(cuerpo) };
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage resp;
        try
        {
            resp = await http.SendAsync(pedido, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new OutlookNoDisponibleException($"sin conexión con Microsoft: {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new OutlookNoDisponibleException("Microsoft tardó demasiado en responder");
        }

        if (resp.IsSuccessStatusCode) return resp;

        var codigo = resp.StatusCode;
        resp.Dispose();
        throw codigo switch
        {
            HttpStatusCode.Unauthorized => new OutlookConexionVencidaException("Graph rechazó el token (401)"),
            HttpStatusCode.NotFound     => new OutlookNoDisponibleException("la cita ya no existe en Outlook (¿se borró desde el calendario?)"),
            _                           => new OutlookNoDisponibleException($"Graph respondió {(int)codigo}")
        };
    }

    private static async Task<ResultadoOutlook> LeerAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var raiz = doc.RootElement;
        var id = raiz.GetProperty("id").GetString()!;
        string? enlace = raiz.TryGetProperty("onlineMeeting", out var om) && om.ValueKind == JsonValueKind.Object
                         && om.TryGetProperty("joinUrl", out var ju) ? ju.GetString() : null;
        return new ResultadoOutlook(id, enlace);
    }
}
```

- [ ] **Step 4: Register**

En `src/Infrastructure/DependencyInjection.cs`, junto al registro de la Task 6:

```csharp
        services.AddHttpClient<IOutlookCalendario, GraphOutlookCalendario>(c =>
        {
            c.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/");
            c.Timeout = TimeSpan.FromSeconds(20);
        });
```

- [ ] **Step 5: Run tests to verify they pass, and build**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~GraphOutlookCalendarioTests"` y `dotnet build Diger.TramitesEstado.sln`
Expected: PASS (9); 0 errores.

- [ ] **Step 6: Commit**

```bash
git add src/Infrastructure/Outlook/GraphOutlookCalendario.cs src/Infrastructure/DependencyInjection.cs tests/Application.Tests/Outlook/GraphOutlookCalendarioTests.cs
git commit -m "Cliente de Graph: crear, actualizar y cancelar la cita, con los fallos traducidos"
```

---

### Task 8: «Conectar mi Outlook» en el perfil

**Files:**
- Modify: `src/Web/Pages/Cuenta/Perfil.cshtml.cs`, `src/Web/Pages/Cuenta/Perfil.cshtml`
- Create: `src/Web/Pages/Cuenta/OutlookCallback.cshtml`, `src/Web/Pages/Cuenta/OutlookCallback.cshtml.cs`
- Modify: `tests/Web.Tests/PortalFactory.cs` (agregar `UsuarioDe`)
- Test: `tests/Web.Tests/OutlookPaginasTests.cs`

**Interfaces:**
- Consumes: `IniciarConexionOutlookQuery`, `CompletarConexionOutlookCommand`, `DesconectarOutlookCommand`, `GetMiConexionOutlookQuery`, `MiConexionOutlookDto` (Task 6).
- Produces: página `/Cuenta/OutlookCallback`; handlers `Perfil?handler=ConectarOutlook` y `Perfil?handler=DesconectarOutlook`; TempData `OutlookEstado`, `OutlookVerificador`, `OutlookAviso`; `PortalFactory.UsuarioDe(string rol) : Guid`.

- [ ] **Step 1: Expose test user ids**

En `tests/Web.Tests/PortalFactory.cs`, después de `ClienteAnonimo()`:

```csharp
    /// <summary>Id del usuario sembrado para el rol (para sembrar datos suyos, como su conexión Outlook).</summary>
    public Guid UsuarioDe(string rolId) => _usuarios[rolId];
```

- [ ] **Step 2: Write the failing Web tests**

`tests/Web.Tests/OutlookPaginasTests.cs`:

```csharp
using System.Net;
using Diger.TramitesEstado.Application.Reuniones.Outlook;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>El retorno de Microsoft solo se acepta con el estado que la propia sesión emitió
/// (anti-CSRF); el perfil apaga la tarjeta cuando el servidor no tiene la app registrada.</summary>
public sealed class OutlookPaginasTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private readonly IConexionMicrosoft _conexion = Substitute.For<IConexionMicrosoft>();

    public async Task InitializeAsync()
    {
        _portal.ServiciosDePrueba = s =>
        {
            s.RemoveAll<IConexionMicrosoft>();
            s.AddScoped(_ => _conexion);
        };
        await _portal.PrepararAsync();
    }

    public Task DisposeAsync() { _portal.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task Sin_configuracion_el_perfil_lo_dice_y_no_ofrece_conectar()
    {
        _conexion.EstaConfigurada.Returns(false);

        var html = await _portal.ClienteComo("Empleado").GetStringAsync("/Cuenta/Perfil");

        html.Should().Contain("no está configurada");
        html.Should().NotContain("ConectarOutlook");
    }

    [Fact]
    public async Task Configurada_y_sin_conexion_ofrece_conectar()
    {
        _conexion.EstaConfigurada.Returns(true);

        var html = await _portal.ClienteComo("Empleado").GetStringAsync("/Cuenta/Perfil");

        html.Should().Contain("Conectar mi Outlook");
    }

    [Fact]
    public async Task Un_retorno_con_estado_ajeno_no_canjea_nada()
    {
        _conexion.EstaConfigurada.Returns(true);

        var resp = await _portal.ClienteComo("Empleado").GetAsync("/Cuenta/OutlookCallback?code=abc&state=inventado");

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        resp.Headers.Location!.ToString().Should().Contain("/Cuenta/Perfil");
        await _conexion.DidNotReceiveWithAnyArgs().CompletarAsync(default, default!, default!, default!, default);
    }

    [Fact]
    public async Task Sin_sesion_el_retorno_no_es_alcanzable()
    {
        var resp = await _portal.ClienteAnonimo().GetAsync("/Cuenta/OutlookCallback?code=abc&state=x");

        resp.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests\Web.Tests --filter "FullyQualifiedName~OutlookPaginasTests"`
Expected: FAIL — el perfil no contiene «no está configurada» y `/Cuenta/OutlookCallback` responde 404.

- [ ] **Step 4: Profile handlers**

En `src/Web/Pages/Cuenta/Perfil.cshtml.cs`: agregar `using Diger.TramitesEstado.Application.CuentaMicrosoft;` y, dentro de `PerfilModel`:

```csharp
    public MiConexionOutlookDto Outlook { get; private set; } = new(false, false, null, null, false);

    /// <summary>Resultado del ida y vuelta con Microsoft (lo deja OutlookCallback).</summary>
    [TempData] public string? OutlookAviso { get; set; }

    public async Task<IActionResult> OnPostConectarOutlookAsync(CancellationToken ct)
    {
        var urlRetorno = Url.PageLink("/Cuenta/OutlookCallback")!;
        var inicio = await sender.Send(new IniciarConexionOutlookQuery(urlRetorno), ct);

        // Estado y verificador PKCE viajan en TempData (cookie cifrada con Data Protection):
        // nunca a la base ni a un log.
        TempData["OutlookEstado"]      = inicio.Estado;
        TempData["OutlookVerificador"] = inicio.Verificador;
        return Redirect(inicio.Url);
    }

    public async Task<IActionResult> OnPostDesconectarOutlookAsync(CancellationToken ct)
    {
        await sender.Send(new DesconectarOutlookCommand(), ct);
        TempData["SuccessMsg"] = "Outlook desconectado. Las citas ya enviadas siguen en su calendario.";
        return RedirectToPage();
    }
```

Y en `OnGetAsync`, antes de su `return Page();` final:

```csharp
        Outlook = await sender.Send(new GetMiConexionOutlookQuery(), ct);
```

- [ ] **Step 5: Profile card**

En `src/Web/Pages/Cuenta/Perfil.cshtml`, como última tarjeta de la página (después del formulario de datos, fuera de él):

```cshtml
<div class="card" style="margin-top:1rem">
    <h3 style="margin-top:0">Microsoft Outlook</h3>
    @if (!string.IsNullOrEmpty(Model.OutlookAviso))
    {
        <div class="alert-warn">@Model.OutlookAviso</div>
    }
    @if (!Model.Outlook.Configurada)
    {
        <p class="hint">La integración con Outlook no está configurada en este servidor. Pídasela a TI.</p>
    }
    else if (!Model.Outlook.Conectada)
    {
        <p class="hint">Conecte su cuenta @@diger.gob.hn para enviar reuniones a su calendario de Outlook, con reunión de Teams.
            Se inicia sesión en la página de Microsoft: el portal nunca ve su contraseña.</p>
        <form method="post" asp-page-handler="ConectarOutlook">
            <button type="submit" class="btnp">Conectar mi Outlook</button>
        </form>
    }
    else
    {
        @if (Model.Outlook.RequiereReconexion)
        {
            <div class="alert-warn">Su conexión venció. Las reuniones que envió no se actualizarán hasta que reconecte.</div>
        }
        <p>Conectado como <strong>@Model.Outlook.Upn</strong>
           <span class="hint">desde @Model.Outlook.ConectadaEl?.ToLocalTime().ToString("dd/MM/yyyy")</span></p>
        <div style="display:flex;gap:8px">
            <form method="post" asp-page-handler="ConectarOutlook">
                <button type="submit" class="btns">@(Model.Outlook.RequiereReconexion ? "Reconectar" : "Cambiar de cuenta")</button>
            </form>
            <form method="post" asp-page-handler="DesconectarOutlook">
                <button type="submit" class="btns">Desconectar</button>
            </form>
        </div>
    }
</div>
```

- [ ] **Step 6: Callback page**

`src/Web/Pages/Cuenta/OutlookCallback.cshtml`:

```cshtml
@page
@model Diger.TramitesEstado.Web.Pages.Cuenta.OutlookCallbackModel
```

`src/Web/Pages/Cuenta/OutlookCallback.cshtml.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Diger.TramitesEstado.Application.CuentaMicrosoft;
using Diger.TramitesEstado.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Diger.TramitesEstado.Web.Pages.Cuenta;

/// <summary>
/// Adonde Microsoft devuelve al usuario tras aceptar. Solo canjea si el <c>state</c> es el que
/// esta misma sesión emitió en Perfil: sin eso, un enlace preparado por otro podría conectar la
/// cuenta Microsoft de un tercero al usuario que lo abra.
/// </summary>
// [Authorize] explícito: la carpeta /Cuenta está exenta por convención.
[Authorize]
[PermisoNoRequerido("Autoservicio: cada usuario conecta su propia cuenta Microsoft.")]
public sealed class OutlookCallbackModel(ISender sender) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string? code, string? state, string? error, string? error_description, CancellationToken ct)
    {
        var esperado    = TempData["OutlookEstado"] as string;
        var verificador = TempData["OutlookVerificador"] as string;

        if (!string.IsNullOrEmpty(error))
            return Volver(error == "access_denied"
                ? "No se conectó: la solicitud se canceló en Microsoft."
                : $"Microsoft no completó la conexión ({error}).");

        if (string.IsNullOrEmpty(code) || esperado is null || verificador is null || !Iguales(state, esperado))
            return Volver("La respuesta de Microsoft no corresponde a esta sesión. Vuelva a pulsar «Conectar mi Outlook».");

        try
        {
            await sender.Send(new CompletarConexionOutlookCommand(code, verificador, Url.PageLink("/Cuenta/OutlookCallback")!), ct);
        }
        catch (DomainException ex)
        {
            return Volver(ex.Message);
        }

        TempData["SuccessMsg"] = "Outlook conectado. Ya puede enviar reuniones a su calendario.";
        return RedirectToPage("/Cuenta/Perfil");
    }

    private RedirectToPageResult Volver(string aviso)
    {
        TempData["OutlookAviso"] = aviso;
        return RedirectToPage("/Cuenta/Perfil");
    }

    private static bool Iguales(string? a, string b) =>
        a is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
```

Verificar con `git grep -n "class PermisoNoRequerido"` que el atributo vive en `Diger.TramitesEstado.Infrastructure.Security` (es lo que usa `Perfil.cshtml.cs`); si vive en otro namespace, usar ese.

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test tests\Web.Tests --filter "FullyQualifiedName~OutlookPaginasTests"`
Expected: PASS (4).

- [ ] **Step 8: Commit**

```bash
git add src/Web/Pages/Cuenta/Perfil.cshtml src/Web/Pages/Cuenta/Perfil.cshtml.cs src/Web/Pages/Cuenta/OutlookCallback.cshtml src/Web/Pages/Cuenta/OutlookCallback.cshtml.cs tests/Web.Tests/PortalFactory.cs tests/Web.Tests/OutlookPaginasTests.cs
git commit -m "El perfil conecta y desconecta Outlook; el retorno de Microsoft exige el estado de la sesion"
```

---

### Task 9: Outlook en el editor, el listado y el acta

**Files:**
- Modify: `src/Web/Pages/Reuniones/Editor.cshtml.cs`, `src/Web/Pages/Reuniones/Editor.cshtml`
- Modify: `src/Web/Pages/Reuniones/Index.cshtml.cs` (`OnPostEliminarAsync`, línea ~54)
- Modify: `src/Web/Pages/Reuniones/Acta.cshtml.cs`, `src/Web/Pages/Reuniones/Acta.cshtml`
- Test: `tests/Web.Tests/OutlookPaginasTests.cs` (agregar casos)

**Interfaces:**
- Consumes: `EnviarReunionAOutlookCommand`, `SincronizarReunionOutlookCommand`, `CancelarReunionOutlookCommand`, `GetEstadoOutlookQuery`, `EstadoOutlookDto` (Task 5); `PortalFactory.UsuarioDe` (Task 8).
- Produces: handlers `Editor?handler=EnviarOutlook`, `Editor?handler=ReintentarOutlook`.

- [ ] **Step 1: Write the failing Web tests**

Agregar a `OutlookPaginasTests` (mismo archivo). Primero, en `InitializeAsync`, registrar también el calendario falso y otorgar el permiso:

```csharp
    private readonly IOutlookCalendario _calendario = Substitute.For<IOutlookCalendario>();
```

```csharp
        _portal.ServiciosDePrueba = s =>
        {
            s.RemoveAll<IConexionMicrosoft>();
            s.AddScoped(_ => _conexion);
            s.RemoveAll<IOutlookCalendario>();
            s.AddScoped(_ => _calendario);
        };
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Reuniones.Ver", "Reuniones.Editar", "Reuniones.Eliminar");
```

Casos nuevos:

```csharp
    private async Task<int> SembrarReunionAsync(bool conectada)
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Diger.TramitesEstado.Infrastructure.Persistence.AppDbContext>();
        var r = Diger.TramitesEstado.Domain.Entities.Reunion.Crear("Mesa técnica");
        r.InstitucionId = "DIGER"; r.InstitucionDuenaId = "DIGER";
        r.AreaId = PortalFactory.AreaDeCasa; r.UnidadId = PortalFactory.UnidadDeCasa;
        r.Fecha = new DateOnly(2026, 10, 23); r.Hora = "09:00";
        db.Reuniones.Add(r);
        if (conectada)
            db.ConexionesMicrosoft.Add(Diger.TramitesEstado.Domain.Entities.ConexionMicrosoft.Crear(
                _portal.UsuarioDe("JefeArea"), "jefe@diger.gob.hn", "h", [1], DateTime.UtcNow));
        await db.SaveChangesAsync();
        return r.Id;
    }

    private HttpClient Jefe() => _portal.ClienteComo("JefeArea", "DIGER", PortalFactory.AreaDeCasa, PortalFactory.UnidadDeCasa);

    private static async Task<string> TokenAsync(HttpClient c, string url)
    {
        var html = await c.GetStringAsync(url);
        var m = System.Text.RegularExpressions.Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        return m.Groups[1].Value;
    }

    [Fact]
    public async Task El_editor_ofrece_enviar_con_Teams_marcado_si_el_usuario_esta_conectado()
    {
        _conexion.EstaConfigurada.Returns(true);
        var id = await SembrarReunionAsync(conectada: true);

        var html = await Jefe().GetStringAsync($"/Reuniones/Editor?id={id}");

        html.Should().Contain("Enviar a Outlook");
        html.Should().MatchRegex("name=\"conTeams\"[^>]*checked");
    }

    [Fact]
    public async Task Enviar_desde_el_editor_deja_la_reunion_en_Outlook()
    {
        _conexion.EstaConfigurada.Returns(true);
        _calendario.CrearAsync(default, default!, default, default).ReturnsForAnyArgs(new ResultadoOutlook("AAMk-9", "https://teams/z"));
        var id = await SembrarReunionAsync(conectada: true);
        var c = Jefe();
        var token = await TokenAsync(c, $"/Reuniones/Editor?id={id}");

        var resp = await c.PostAsync($"/Reuniones/Editor?id={id}&handler=EnviarOutlook", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["conTeams"] = "true"
        }));

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Diger.TramitesEstado.Infrastructure.Persistence.AppDbContext>();
        // IgnoreQueryFilters: este scope no tiene sesión, y el filtro de alcance lo dejaría sin filas.
        (await db.Reuniones.IgnoreQueryFilters().SingleAsync(r => r.Id == id)).OutlookEventId.Should().Be("AAMk-9");
    }

    [Fact]
    public async Task Borrar_una_reunion_cuya_cita_no_se_pudo_cancelar_la_borra_igual()
    {
        _conexion.EstaConfigurada.Returns(true);
        _calendario.CrearAsync(default, default!, default, default).ReturnsForAnyArgs(new ResultadoOutlook("AAMk-9", null));
        _calendario.CancelarAsync(default, default!, default!, default)
            .ThrowsAsyncForAnyArgs(new OutlookNoDisponibleException("503"));
        var id = await SembrarReunionAsync(conectada: true);
        var c = Jefe();
        var token = await TokenAsync(c, $"/Reuniones/Editor?id={id}");
        await c.PostAsync($"/Reuniones/Editor?id={id}&handler=EnviarOutlook", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["__RequestVerificationToken"] = token, ["conTeams"] = "true" }));

        var tokenIndex = await TokenAsync(c, "/Reuniones");
        var resp = await c.PostAsync($"/Reuniones?handler=Eliminar&id={id}", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["__RequestVerificationToken"] = tokenIndex }));

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Diger.TramitesEstado.Infrastructure.Persistence.AppDbContext>();
        (await db.Reuniones.IgnoreQueryFilters().SingleAsync(r => r.Id == id)).IsDeleted
            .Should().BeTrue("el portal no bloquea un borrado por Microsoft");
    }
```

(Agregar arriba `using NSubstitute.ExceptionExtensions;` y `using Microsoft.EntityFrameworkCore;`.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests\Web.Tests --filter "FullyQualifiedName~OutlookPaginasTests"`
Expected: los tres casos nuevos FAIL (no hay «Enviar a Outlook»; el handler no existe → 400/404).

- [ ] **Step 3: Editor PageModel**

En `src/Web/Pages/Reuniones/Editor.cshtml.cs`, agregar `using Diger.TramitesEstado.Application.Reuniones.Outlook;` y, dentro de `EditorModel`:

```csharp
    /// <summary>Estado de la cita de Outlook. null en una reunión nueva (aún no existe).</summary>
    public EstadoOutlookDto? Outlook { get; private set; }

    [TempData] public string? OutlookAviso { get; set; }

    private string UrlFicha(int id) => Url.PageLink("/Reuniones/Acta", values: new { id })!;

    /// <summary>Tras guardar: actualiza la cita si cambió para los invitados. Nunca lanza por
    /// Outlook (un fallo queda como pendiente y se ve en la tarjeta).</summary>
    private Task SincronizarOutlookAsync(int id, CancellationToken ct) =>
        sender.Send(new SincronizarReunionOutlookCommand(id, UrlFicha(id)), ct);

    public async Task<IActionResult> OnPostEnviarOutlookAsync(int id, bool conTeams, CancellationToken ct)
    {
        try
        {
            await sender.Send(new EnviarReunionAOutlookCommand(id, conTeams, UrlFicha(id)), ct);
            TempData["SuccessMsg"] = conTeams
                ? "La reunión se envió a Outlook, con reunión de Teams."
                : "La reunión se envió a Outlook.";
        }
        catch (DomainException ex)
        {
            OutlookAviso = ex.Message;
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReintentarOutlookAsync(int id, CancellationToken ct)
    {
        await SincronizarOutlookAsync(id, ct);
        return RedirectToPage(new { id });
    }
```

En `OnGetAsync`, dentro del `try`, después de `RepartirAdjuntos(d.Datos);`:

```csharp
            Outlook = await sender.Send(new GetEstadoOutlookQuery(d.Id), ct);
```

En `OnPostAsync`, después de `await GuardarAsync(id, ct);`:

```csharp
            if (id is not null) await SincronizarOutlookAsync(id.Value, ct);
```

En `OnPostGuardarContinuarAsync`, después de `var reunionId = await GuardarAsync(id, ct);`:

```csharp
            if (id is not null) await SincronizarOutlookAsync(reunionId, ct);
```

- [ ] **Step 4: Editor card**

En `src/Web/Pages/Reuniones/Editor.cshtml`, justo después del bloque `@if (!string.IsNullOrEmpty(Model.Error)) { ... }` y antes de `<form method="post" enctype="multipart/form-data" id="reu-form">` (no puede ir dentro de ese form: los formularios no se anidan):

```cshtml
@if (Model.ReunionId is not null && Model.Outlook is { Configurado: true } o)
{
    <div class="container" style="max-width:1280px;padding-top:0">
      <div class="card" style="padding:12px 16px;display:flex;gap:12px;align-items:center;flex-wrap:wrap">
        <strong>Outlook</strong>
        @if (!string.IsNullOrEmpty(Model.OutlookAviso))
        {
            <div class="alert-warn" style="flex-basis:100%">@Model.OutlookAviso</div>
        }
        @if (o.EnOutlook)
        {
            <span class="hint">En el calendario de @o.Organizador
                @(o.SincronizadaEl is { } s ? $"· actualizada {s.ToLocalTime():dd/MM/yyyy HH:mm}" : "")</span>
            @if (!string.IsNullOrEmpty(o.EnlaceTeams))
            {
                <a class="btns" href="@o.EnlaceTeams" target="_blank" rel="noopener">Abrir en Teams</a>
            }
            @if (o.Pendiente)
            {
                <div class="alert-warn" style="flex-basis:100%;display:flex;gap:8px;align-items:center;justify-content:space-between">
                    <span>@o.Error</span>
                    <form method="post" asp-page-handler="ReintentarOutlook" asp-route-id="@Model.ReunionId">
                        <button type="submit" class="btns">Reintentar</button>
                    </form>
                </div>
            }
        }
        else if (!o.UsuarioConectado || o.UsuarioRequiereReconexion)
        {
            <span class="hint">Para enviar esta reunión a su calendario,
                <a asp-page="/Cuenta/Perfil">@(o.UsuarioRequiereReconexion ? "reconecte" : "conecte") su Outlook</a> desde su perfil.</span>
        }
        else
        {
            <form method="post" asp-page-handler="EnviarOutlook" asp-route-id="@Model.ReunionId"
                  style="display:flex;gap:12px;align-items:center;margin:0">
                <label style="display:flex;gap:6px;align-items:center;margin:0">
                    @* Sin hidden de respaldo a propósito: desmarcada, la casilla no se envía y el
                       parámetro bool conTeams del handler queda en false. *@
                    <input type="checkbox" name="conTeams" value="true" checked /> Crear reunión de Teams
                </label>
                <button type="submit" class="btnp">Enviar a Outlook</button>
            </form>
            <span class="hint">Llega a usted y a los pre-registrados con correo, al enlace y al facilitador.</span>
        }
      </div>
    </div>
}
```


- [ ] **Step 5: Cancel before deleting**

En `src/Web/Pages/Reuniones/Index.cshtml.cs`, agregar `using Diger.TramitesEstado.Application.Reuniones.Outlook;` y reemplazar el cuerpo de `OnPostEliminarAsync`:

```csharp
    [Permission("Reuniones", AccionModulo.Eliminar, "Eliminar reuniones")]
    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        // Primero se cancela la cita (Graph avisa a los invitados). Si no se puede, se borra
        // igual y se dice: el portal no bloquea un borrado por Microsoft.
        string? avisoOutlook = null;
        try
        {
            await sender.Send(new CancelarReunionOutlookCommand(id), ct);
        }
        catch (DomainException ex)
        {
            avisoOutlook = ex.Message;
        }

        await sender.Send(new EliminarReunionCommand(id), ct);
        TempData["SuccessMsg"] = avisoOutlook is null ? "Reunión eliminada." : $"Reunión eliminada. {avisoOutlook}";
        return RedirectToPage();
    }
```

- [ ] **Step 6: Teams link in the minutes**

En `src/Web/Pages/Reuniones/Acta.cshtml.cs`, agregar `using Diger.TramitesEstado.Application.Reuniones.Outlook;`, la propiedad:

```csharp
    public string? EnlaceTeams { get; private set; }
```

y, en su `OnGetAsync`, después de cargar `ReunionId` (donde se asignan `Datos`, `Asistentes`, `Acuerdos`):

```csharp
        EnlaceTeams = (await sender.Send(new GetEstadoOutlookQuery(ReunionId), ct)).EnlaceTeams;
```

(Si el constructor de `ActaModel` no recibe `ISender` con el nombre `sender`, usar el nombre que tenga.) En `Acta.cshtml`, en la barra superior de botones (junto al de descarga del PDF):

```cshtml
@if (!string.IsNullOrEmpty(Model.EnlaceTeams))
{
    <a class="btns" href="@Model.EnlaceTeams" target="_blank" rel="noopener">Unirse por Teams</a>
}
```

- [ ] **Step 7: Run tests, full suite and build**

Run: `dotnet test tests\Web.Tests --filter "FullyQualifiedName~OutlookPaginasTests"` y luego `dotnet test Diger.TramitesEstado.sln`
Expected: los 7 de `OutlookPaginasTests` PASS. En la suite completa, las únicas fallas admitidas son las 6 de `TableroEjecutivoTests` que ya fallaban antes de empezar (trabajo de tableros sin confirmar en el árbol); cualquier otra falla es de esta tarea.

- [ ] **Step 8: Commit**

```bash
git add src/Web/Pages/Reuniones/Editor.cshtml src/Web/Pages/Reuniones/Editor.cshtml.cs src/Web/Pages/Reuniones/Index.cshtml.cs src/Web/Pages/Reuniones/Acta.cshtml src/Web/Pages/Reuniones/Acta.cshtml.cs tests/Web.Tests/OutlookPaginasTests.cs
git commit -m "El editor envia la reunion a Outlook, guardar la actualiza y borrar la cancela"
```

---

### Task 10: Configuración, verificación real y cierre de documentación

**Files:**
- Modify: `docs/superpowers/specs/2026-10-22-reuniones-a-outlook-design.md` (desviaciones)
- Modify: `CLAUDE.md` (sección corta sobre la integración)

Esta tarea necesita el registro de TI (spec, «Requisitos fuera del código»). Si todavía no está, hacer solo los Steps 1, 5 y 6 y dejar el resto marcado como pendiente en el reporte.

- [ ] **Step 1: Record the plan's deviations in the spec**

En el spec: (a) «Microsoft Graph SDK + MSAL» → «MSAL + `HttpClient` contra Graph v1.0 (sin SDK: tres llamadas)»; (b) `/Cuenta/Microsoft/Callback` → `/Cuenta/OutlookCallback` (en el flujo y en los requisitos de TI); (c) tabla `ConexionesMicrosoft`: agregar `HomeAccountId nvarchar(200)` y precisar que la PK `Id` es el id del usuario.

- [ ] **Step 2: Configure the secret locally**

```powershell
dotnet user-secrets set "MicrosoftGraph:ClientId" "<ClientId que entregó TI>" --project src\Web
dotnet user-secrets set "MicrosoftGraph:ClientSecret" "<secreto que entregó TI>" --project src\Web
```

- [ ] **Step 3: Run the app with the right profile**

```powershell
dotnet run --project src\Web --launch-profile Diger.TramitesEstado.Web
```
Expected: escucha en `https://localhost:49175`. (Otro nombre de perfil cae fuera de Development y apunta a `GestionGD_TEST`, que no existe.)

- [ ] **Step 4: End-to-end by hand**

1. `/Cuenta/Perfil` → «Conectar mi Outlook» → iniciar sesión con una cuenta @diger.gob.hn → aceptar → vuelve con «Outlook conectado».
2. Crear una reunión de prueba con fecha mañana 09:00, Lugar «Prueba», y un pre-registro con un correo propio. Guardar.
3. En la tarjeta Outlook: «Crear reunión de Teams» marcado → «Enviar a Outlook». Comprobar en Outlook: cita en el calendario del organizador, invitación en el correo propio, enlace de Teams, «Abrir en Teams» en el editor y «Unirse por Teams» en el acta.
4. Guardar sin cambios → Outlook **no** recibe «actualizada» (no llega correo nuevo).
5. Cambiar la hora a 10:30 y guardar → la cita se mueve; llega la actualización.
6. Quitar el pre-registro y guardar → al correo propio le llega la cancelación.
7. Borrar la reunión → la cita desaparece del calendario del organizador.
8. Repetir 2–3 con la casilla de Teams **desmarcada** → la cita no trae Teams.
9. «Desconectar» en el perfil → la tarjeta del editor vuelve a pedir conectar.

- [ ] **Step 5: CLAUDE.md**

En `CLAUDE.md`, después de la sección «Data import», agregar:

```markdown
### Reuniones a Outlook

Portal → Outlook, nunca al revés. Cada usuario conecta su cuenta Microsoft desde su perfil
(OAuth delegado, MSAL); la caché de tokens se guarda cifrada en `ConexionesMicrosoft` y la
integración se apaga sola sin `MicrosoftGraph:TenantId/ClientId/ClientSecret` (el secreto va en
User Secrets, nunca en appsettings). Application arma un `EventoOutlook` puro y habla con
`IOutlookCalendario`; solo `Infrastructure/Outlook/` conoce Graph. Guardar una reunión nunca
falla por Outlook (queda `OutlookPendiente`) y borrar nunca se bloquea. Actualizar y cancelar
usan la conexión del **organizador**, no la de quien edita. Spec:
`docs/superpowers/specs/2026-10-22-reuniones-a-outlook-design.md`.
```

- [ ] **Step 6: Commit**

```bash
git add docs/superpowers/specs/2026-10-22-reuniones-a-outlook-design.md CLAUDE.md
git commit -m "Reuniones a Outlook: spec al dia con lo construido y nota en CLAUDE.md"
```
