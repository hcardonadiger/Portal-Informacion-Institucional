# Panel de control — Notificaciones (entrega 1) — plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que desde `/PanelControl/Notificaciones` se configuren, sin editar `appsettings.json` ni reiniciar: la frecuencia de la revisión automática; los interruptores de campana, correo y agrupación por módulo; y el correo saliente con su contraseña cifrada. Los avisos parecidos se agrupan en uno y el correo sale por una bandeja de salida.

**Architecture:** Los ajustes viven en la base (una fila de `AjustesNotificaciones` y una fila por módulo en `ModulosNotificacion`). Un proveedor con caché corta (`IAjustesNotificaciones`) los lee para todo el sistema y, si nunca se guardaron, devuelve los valores de `appsettings`. `INotificacionService` es la única puerta de los avisos: aplica los interruptores, agrupa con `NotificacionElementos` y anota los correos en `CorreosPendientes`. Un despachador manda un correo por persona al final de cada revisión.

**Tech Stack:** .NET 9, Razor Pages, EF Core 9 (SQL Server; SQLite en las pruebas Web; In-Memory en las de Application), MediatR, FluentValidation, ASP.NET Core Data Protection, xUnit + FluentAssertions + NSubstitute.

**Spec:** `docs/superpowers/specs/2026-10-22-panel-control-notificaciones-design.md`

## Global Constraints

- Revisión automática: **mínimo 5 min, máximo 24 h (1440 min)**, guardada como total de minutos; la pantalla pide horas (0–24) y minutos (0–59).
- Interruptores por módulo: **Campana**, **Correo**, **Agrupar**. Módulos: Tickets, Reuniones, Expedientes, Proyectos, Soporte. **Soporte nunca admite correo.**
- Valores iniciales: Campana **encendida**, Correo **apagado**, Agrupar **encendido** en los cinco módulos; `IntervaloMinutos = 240`; `SmtpConfigurado = 0`.
- Mientras `SmtpConfigurado = 0` se usa la sección `Smtp` de `appsettings`.
- La contraseña se cifra con `IDataProtector` de propósito **`PanelControl.Notificaciones.Smtp`**. **Nunca** llega al navegador, al registro ni a la bitácora.
- Permiso: módulo **`PanelControl.Notificaciones`**, acciones **Ver** y **Editar**.
- Agrupación por **persona + tipo**, solo sobre un aviso **sin leer**. «Ya se avisó hoy» aplica **solo** a los tipos diarios (`ReuniónMañana`, `CompromisoVencido`, `CompromisoProximo`, `EtapaCronogramaVencida`, `EtapaCronogramaProxima`).
- Los recordatorios manuales mandan su correo **al instante, sin bandeja**, solo si el correo de su módulo está encendido.
- Despachador: **un correo por persona por revisión**; 3 intentos y después `Fallido`.
- El producto se nombra con `Marca.Nombre`, nunca con un literal (CLAUDE.md).
- Cada cambio de base lleva migración EF **más** un script idempotente en `database/` (CLAUDE.md).
- Las migraciones nuevas van con `--output-dir Persistence\Migrations` (CLAUDE.md).
- Todo `fetch` que escribe lleva el token antiforgery (CLAUDE.md). Esta entrega usa formularios normales, que lo llevan solo.

## Mapa de archivos

| Archivo | Responsabilidad |
|---|---|
| `src/Domain/Enums/Enums.cs` | + `ModuloNotificacion`, `EstadoCorreoPendiente` |
| `src/Domain/Entities/AjustesNotificaciones.cs` | Fila única: intervalo, SMTP, última revisión |
| `src/Domain/Entities/InterruptorNotificacion.cs` | Interruptores de un módulo |
| `src/Domain/Entities/NotificacionElemento.cs` | Un elemento dentro de un aviso |
| `src/Domain/Entities/CorreoPendiente.cs` | Bandeja de salida |
| `src/Domain/Entities/PanelControlBitacora.cs` | Bitácora del panel (solo agregar) |
| `src/Domain/Entities/Notificacion.cs` | + `Cantidad`, `FechaActualizacion`, `Elementos`, `Sumar`, `Destino` |
| `src/Application/Notificaciones/CatalogoNotificaciones.cs` | Tipo → módulo, etiqueta, plural, diario, correo inmediato |
| `src/Application/Notificaciones/IAjustesNotificaciones.cs` | Contratos del proveedor, del protector y de `SenalRevision` |
| `src/Application/Notificaciones/IDespachadorCorreos.cs` | Contrato del despachador |
| `src/Application/Notificaciones/INotificacionService.cs` | Firma asíncrona y DTO con `Cantidad` |
| `src/Application/Common/Interfaces/IEmailService.cs` | + `TipoCorreo.ResumenAvisos/Prueba`, `IProbadorCorreo` |
| `src/Application/Common/Interfaces/IRepositories.cs` | + 5 `DbSet` en `IApplicationDbContext` |
| `src/Application/PanelControl/Notificaciones/PanelNotificaciones.cs` | Consulta + 4 comandos del panel |
| `src/Infrastructure/Persistence/AppDbContext.cs` | DbSets + 5 configuraciones + cambio en `NotificacionConfiguration` |
| `src/Infrastructure/Persistence/Migrations/<ts>_PanelControlNotificaciones.cs` | Migración (generada) |
| `src/Infrastructure/Security/ProtectorSecretos.cs` | Cifrado con Data Protection |
| `src/Infrastructure/Notifications/AjustesNotificacionesProvider.cs` | Proveedor con caché y respaldo de `appsettings` |
| `src/Infrastructure/Notifications/NotificacionService.cs` | La puerta única |
| `src/Infrastructure/Notifications/DespachadorCorreos.cs` | Bandeja → un correo por persona |
| `src/Infrastructure/Notifications/RecordatorioBackgroundService.cs` | Intervalo vivo, señal, marca de revisión, despacho |
| `src/Infrastructure/Email/SmtpEmailService.cs` | SMTP desde el proveedor + `IProbadorCorreo` |
| `src/Infrastructure/DependencyInjection.cs` | Registros nuevos |
| `src/Web/Pages/PanelControl/Index.cshtml(.cs)` | Portada del módulo |
| `src/Web/Pages/PanelControl/Notificaciones.cshtml(.cs)` | La pantalla |
| `src/Web/Pages/Notificaciones/Detalle.cshtml(.cs)` | `/Notificaciones/{id}` |
| `src/Web/Pages/Shared/_Layout.cshtml` | Grupo «Panel de control» en el menú |
| `src/Web/Models/AvisoRecordatorio.cs` | Texto para «correo apagado» |
| `src/Web/Common/AvisosDeConfiguracion.cs` | El aviso de arranque menciona el panel |
| `database/2026-10-22_panel_control_notificaciones.sql` | Script idempotente |
| `docs/despliegue/2026-10-22_panel_control_notificaciones.md` | Notas de despliegue |

---

### Task 1: Catálogo de tipos y módulos

**Files:**
- Modify: `src/Domain/Enums/Enums.cs` (después de `TipoNotificacion`, línea ~168)
- Create: `src/Application/Notificaciones/CatalogoNotificaciones.cs`
- Test: `tests/Application.Tests/Notificaciones/CatalogoNotificacionesTests.cs`

**Interfaces:**
- Produces: `enum ModuloNotificacion { Tickets = 1, Reuniones = 2, Expedientes = 3, Proyectos = 4, Soporte = 5 }`; `enum EstadoCorreoPendiente { Pendiente = 1, Enviado = 2, Fallido = 3 }`; `record DefinicionNotificacion(TipoNotificacion Tipo, ModuloNotificacion Modulo, string Etiqueta, Func<int,string> Plural, bool Diario = false, bool CorreoInmediato = false)` con `bool AdmiteCorreo`; `static class CatalogoNotificaciones` con `De(TipoNotificacion)`, `Todas`, `DelModulo(ModuloNotificacion)`, `ModuloAdmiteCorreo(ModuloNotificacion)`, `Nombre(ModuloNotificacion)`.

- [ ] **Step 1: Write the failing test**

```csharp
using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Notificaciones;

/// <summary>El catálogo es lo que pone cada aviso bajo el interruptor de su módulo. Un tipo nuevo sin
/// módulo quedaría fuera de todos los interruptores, y lanzar en tiempo de ejecución sería peor que
/// fallar acá.</summary>
public sealed class CatalogoNotificacionesTests
{
    [Fact]
    public void Todos_los_tipos_tienen_modulo()
    {
        foreach (var tipo in Enum.GetValues<TipoNotificacion>())
            CatalogoNotificaciones.Invoking(_ => CatalogoNotificaciones.De(tipo))
                .Should().NotThrow($"{tipo} tiene que tener módulo en CatalogoNotificaciones");
    }

    [Fact]
    public void El_chat_no_admite_correo()
    {
        CatalogoNotificaciones.De(TipoNotificacion.ChatRecibido).AdmiteCorreo.Should().BeFalse();
        CatalogoNotificaciones.ModuloAdmiteCorreo(ModuloNotificacion.Soporte).Should().BeFalse();
        CatalogoNotificaciones.ModuloAdmiteCorreo(ModuloNotificacion.Reuniones).Should().BeTrue();
    }

    [Fact]
    public void Solo_los_cinco_del_revisor_son_diarios()
    {
        CatalogoNotificaciones.Todas.Where(d => d.Diario).Select(d => d.Tipo).Should().BeEquivalentTo(new[]
        {
            TipoNotificacion.ReuniónMañana, TipoNotificacion.CompromisoVencido, TipoNotificacion.CompromisoProximo,
            TipoNotificacion.EtapaCronogramaVencida, TipoNotificacion.EtapaCronogramaProxima,
        });
    }

    [Fact]
    public void Los_recordatorios_manuales_mandan_correo_al_instante()
    {
        CatalogoNotificaciones.Todas.Where(d => d.CorreoInmediato).Select(d => d.Tipo).Should().BeEquivalentTo(new[]
        {
            TipoNotificacion.RecordatorioManualTicket, TipoNotificacion.RecordatorioManualExpediente,
            TipoNotificacion.RecordatorioManualReunion, TipoNotificacion.RecordatorioManualCompromiso,
        });
    }

    [Fact]
    public void El_plural_dice_la_cantidad()
    {
        CatalogoNotificaciones.De(TipoNotificacion.CompromisoVencido).Plural(6).Should().Be("Tiene 6 compromisos vencidos");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~CatalogoNotificacionesTests"`
Expected: falla la compilación: no existe `CatalogoNotificaciones` ni `ModuloNotificacion`.

- [ ] **Step 3: Write minimal implementation**

En `src/Domain/Enums/Enums.cs`, justo después del cierre de `enum TipoNotificacion`:

```csharp
// ── Módulo al que pertenece un aviso, para los interruptores del Panel de control ──
public enum ModuloNotificacion
{
    Tickets     = 1,
    Reuniones   = 2,
    Expedientes = 3,
    Proyectos   = 4,
    Soporte     = 5,
}

// ── Estado de un correo en la bandeja de salida ───────────────────────────
public enum EstadoCorreoPendiente
{
    Pendiente = 1,
    Enviado   = 2,
    Fallido   = 3,
}
```

`src/Application/Notificaciones/CatalogoNotificaciones.cs`:

```csharp
namespace Diger.TramitesEstado.Application.Notificaciones;

/// <summary>Qué es cada tipo de aviso para el Panel de control.</summary>
/// <param name="Etiqueta">Cómo se nombra el tipo en el panel y como encabezado del correo resumen.</param>
/// <param name="Plural">Título de un aviso agrupado con N elementos.</param>
/// <param name="Diario">Lo genera el revisor automático y se avisa una vez por día y elemento.</param>
/// <param name="CorreoInmediato">Recordatorio manual: manda su correo en el acto, sin bandeja.</param>
public sealed record DefinicionNotificacion(
    TipoNotificacion   Tipo,
    ModuloNotificacion Modulo,
    string             Etiqueta,
    Func<int, string>  Plural,
    bool               Diario          = false,
    bool               CorreoInmediato = false)
{
    public bool AdmiteCorreo => CatalogoNotificaciones.ModuloAdmiteCorreo(Modulo);
}

/// <summary>
/// A qué módulo pertenece cada <see cref="TipoNotificacion"/>. Vive en el código y no en la base:
/// un tipo es siempre del mismo módulo, y en la base alguien podría descuadrarlo. Un tipo nuevo se
/// declara acá; <c>CatalogoNotificacionesTests</c> falla si se olvida.
/// </summary>
public static class CatalogoNotificaciones
{
    private static readonly DefinicionNotificacion[] Definiciones =
    [
        new(TipoNotificacion.TicketAsignado,               ModuloNotificacion.Tickets,     "Tickets asignados",                n => $"Tiene {n} tickets asignados"),
        new(TipoNotificacion.TicketResuelto,               ModuloNotificacion.Tickets,     "Tickets resueltos",                n => $"{n} tickets resueltos"),
        new(TipoNotificacion.RecordatorioManualTicket,     ModuloNotificacion.Tickets,     "Recordatorios de tickets",         n => $"{n} recordatorios de tickets", CorreoInmediato: true),
        new(TipoNotificacion.ReuniónMañana,                ModuloNotificacion.Reuniones,   "Reuniones de mañana",              n => $"{n} reuniones mañana", Diario: true),
        new(TipoNotificacion.CompromisoVencido,            ModuloNotificacion.Reuniones,   "Compromisos vencidos",             n => $"Tiene {n} compromisos vencidos", Diario: true),
        new(TipoNotificacion.CompromisoProximo,            ModuloNotificacion.Reuniones,   "Compromisos por vencer",           n => $"{n} compromisos vencen en los próximos 3 días", Diario: true),
        new(TipoNotificacion.RecordatorioManualReunion,    ModuloNotificacion.Reuniones,   "Recordatorios de reuniones",       n => $"{n} recordatorios de reuniones", CorreoInmediato: true),
        new(TipoNotificacion.RecordatorioManualCompromiso, ModuloNotificacion.Reuniones,   "Recordatorios de compromisos",     n => $"{n} recordatorios de compromisos", CorreoInmediato: true),
        new(TipoNotificacion.EtapaCronogramaVencida,       ModuloNotificacion.Expedientes, "Etapas de cronograma vencidas",    n => $"{n} etapas de cronograma vencidas", Diario: true),
        new(TipoNotificacion.EtapaCronogramaProxima,       ModuloNotificacion.Expedientes, "Etapas de cronograma por vencer",  n => $"{n} etapas de cronograma vencen pronto", Diario: true),
        new(TipoNotificacion.RecordatorioManualExpediente, ModuloNotificacion.Expedientes, "Recordatorios de expedientes",     n => $"{n} recordatorios de expedientes", CorreoInmediato: true),
        new(TipoNotificacion.ProyectoCambioEstado,         ModuloNotificacion.Proyectos,   "Proyectos que cambiaron de estado", n => $"{n} proyectos cambiaron de estado"),
        new(TipoNotificacion.ChatRecibido,                 ModuloNotificacion.Soporte,     "Chats de soporte",                 n => $"{n} chats nuevos"),
    ];

    private static readonly Dictionary<TipoNotificacion, DefinicionNotificacion> PorTipo =
        Definiciones.ToDictionary(d => d.Tipo);

    public static IReadOnlyCollection<DefinicionNotificacion> Todas => Definiciones;

    public static DefinicionNotificacion De(TipoNotificacion tipo) =>
        PorTipo.TryGetValue(tipo, out var d)
            ? d
            : throw new InvalidOperationException($"El tipo de notificación {tipo} no tiene módulo en CatalogoNotificaciones.");

    public static IReadOnlyList<DefinicionNotificacion> DelModulo(ModuloNotificacion modulo) =>
        Definiciones.Where(d => d.Modulo == modulo).ToList();

    /// <summary>El chat de soporte va solo a la campana: un correo por cada chat sería ruido.</summary>
    public static bool ModuloAdmiteCorreo(ModuloNotificacion modulo) => modulo != ModuloNotificacion.Soporte;

    public static string Nombre(ModuloNotificacion modulo) => modulo switch
    {
        ModuloNotificacion.Tickets     => "Tickets",
        ModuloNotificacion.Reuniones   => "Reuniones",
        ModuloNotificacion.Expedientes => "Expedientes",
        ModuloNotificacion.Proyectos   => "Proyectos",
        ModuloNotificacion.Soporte     => "Soporte (chat)",
        _                              => modulo.ToString(),
    };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~CatalogoNotificacionesTests"`
Expected: PASS (5 pruebas).

- [ ] **Step 5: Commit**

```powershell
git add src/Domain/Enums/Enums.cs src/Application/Notificaciones/CatalogoNotificaciones.cs tests/Application.Tests/Notificaciones/CatalogoNotificacionesTests.cs
git commit -m "Catalogo de notificaciones: cada tipo de aviso pertenece a un modulo"
```

---

### Task 2: Entidades de dominio

**Files:**
- Create: `src/Domain/Entities/AjustesNotificaciones.cs`, `InterruptorNotificacion.cs`, `NotificacionElemento.cs`, `CorreoPendiente.cs`, `PanelControlBitacora.cs`
- Modify: `src/Domain/Entities/Notificacion.cs`
- Test: `tests/Domain.Tests/NotificacionAgrupadaTests.cs`, `tests/Domain.Tests/AjustesNotificacionesTests.cs`

**Interfaces:**
- Consumes: `ModuloNotificacion`, `EstadoCorreoPendiente` (Task 1).
- Produces:
  - `AjustesNotificaciones` con las constantes `IdUnico`, `MinimoMinutos`, `MaximoMinutos` e `IntervaloPorDefecto`, y los métodos `CrearPorDefecto(int)`, `CambiarIntervalo(int)`, `ConfigurarSmtp(string host, int puerto, bool ssl, string usuario, string remitente, string? nombreRemitente)`, `FijarPasswordCifrada(string?)` y `MarcarRevision(DateTime)`.
  - `InterruptorNotificacion.Crear(ModuloNotificacion, bool campana = true, bool correo = false, bool agrupar = true)` y `Cambiar(bool, bool, bool) → bool`.
  - `Notificacion.Crear(Guid, TipoNotificacion, string titulo, string? url = null, string? clave = null, DateTime? ahoraUtc = null)`.
  - `Notificacion.Sumar(string clave, string titulo, string? url, Func<int,string> tituloGrupo, DateTime ahoraUtc) → bool`.
  - `Notificacion.Destino`, `Notificacion.RutaDetalle(int)`, `Notificacion.Recortar(string, int)`.
  - `CorreoPendiente.Crear(Guid, ModuloNotificacion, TipoNotificacion, string titulo, string? url, string clave, DateTime)`, `MarcarEnviado(DateTime)`, `RegistrarFallo(string, int)` y `Descartar(string)`.
  - `PanelControlBitacora.Crear(string seccion, string detalle, string actor, DateTime fechaUtc)`.

- [ ] **Step 1: Write the failing tests**

`tests/Domain.Tests/NotificacionAgrupadaTests.cs`:

```csharp
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Domain.Tests;

public sealed class NotificacionAgrupadaTests
{
    private static readonly DateTime T0 = new(2026, 10, 22, 15, 0, 0, DateTimeKind.Utc);
    private static string Plural(int n) => $"Tiene {n} compromisos vencidos";

    [Fact]
    public void Un_aviso_nuevo_tiene_un_elemento_y_lleva_directo()
    {
        var n = Notificacion.Crear(Guid.NewGuid(), TipoNotificacion.CompromisoVencido, "Compromiso vencido: Acta", "/Reuniones/CompromisoDetalle/1", ahoraUtc: T0);

        n.Cantidad.Should().Be(1);
        n.Elementos.Should().ContainSingle(e => e.Clave == "/Reuniones/CompromisoDetalle/1");
        n.Destino.Should().Be("/Reuniones/CompromisoDetalle/1");
        n.FechaActualizacion.Should().Be(T0);
    }

    [Fact]
    public void Sumar_otro_elemento_cambia_el_titulo_y_el_destino()
    {
        var n = Notificacion.Crear(Guid.NewGuid(), TipoNotificacion.CompromisoVencido, "Compromiso vencido: Acta", "/c/1", ahoraUtc: T0);

        var sumo = n.Sumar("/c/2", "Compromiso vencido: Informe", "/c/2", Plural, T0.AddMinutes(15));

        sumo.Should().BeTrue();
        n.Cantidad.Should().Be(2);
        n.Titulo.Should().Be("Tiene 2 compromisos vencidos");
        n.Destino.Should().Be(Notificacion.RutaDetalle(n.Id));
        n.FechaActualizacion.Should().Be(T0.AddMinutes(15));
    }

    [Fact]
    public void El_mismo_elemento_no_cuenta_dos_veces_pero_sube_el_aviso()
    {
        var n = Notificacion.Crear(Guid.NewGuid(), TipoNotificacion.ChatRecibido, "Nuevo chat de Ana", "/Chat#4", ahoraUtc: T0);

        var sumo = n.Sumar("/Chat#4", "Nuevo chat de Ana", "/Chat#4", x => $"{x} chats nuevos", T0.AddMinutes(1));

        sumo.Should().BeFalse();
        n.Cantidad.Should().Be(1);
        n.Titulo.Should().Be("Nuevo chat de Ana");
        n.FechaActualizacion.Should().Be(T0.AddMinutes(1));
    }

    [Fact]
    public void No_se_suma_a_un_aviso_leido()
    {
        var n = Notificacion.Crear(Guid.NewGuid(), TipoNotificacion.CompromisoVencido, "x", "/c/1", ahoraUtc: T0);
        n.MarcarLeida();

        n.Invoking(x => x.Sumar("/c/2", "y", "/c/2", Plural, T0)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Un_titulo_largo_se_recorta_al_largo_de_la_columna()
    {
        var n = Notificacion.Crear(Guid.NewGuid(), TipoNotificacion.CompromisoVencido, new string('a', 300), "/c/1", ahoraUtc: T0);

        n.Titulo.Length.Should().Be(Notificacion.LargoTitulo);
    }
}
```

`tests/Domain.Tests/AjustesNotificacionesTests.cs`:

```csharp
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Domain.Tests;

public sealed class AjustesNotificacionesTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(1441)]
    public void El_intervalo_fuera_de_rango_se_rechaza(int minutos) =>
        AjustesNotificaciones.CrearPorDefecto().Invoking(a => a.CambiarIntervalo(minutos)).Should().Throw<DomainException>();

    [Fact]
    public void Quince_minutos_es_valido()
    {
        var a = AjustesNotificaciones.CrearPorDefecto();
        a.CambiarIntervalo(15);
        a.IntervaloMinutos.Should().Be(15);
        a.Id.Should().Be(AjustesNotificaciones.IdUnico);
    }

    [Fact]
    public void Configurar_el_correo_lo_marca_como_configurado()
    {
        var a = AjustesNotificaciones.CrearPorDefecto();
        a.ConfigurarSmtp(" smtp.office365.com ", 587, true, "no-reply@diger.gob.hn", "no-reply@diger.gob.hn", null);
        a.SmtpConfigurado.Should().BeTrue();
        a.SmtpHost.Should().Be("smtp.office365.com");
    }

    [Fact]
    public void El_interruptor_avisa_si_cambio()
    {
        var i = InterruptorNotificacion.Crear(ModuloNotificacion.Reuniones);
        i.Cambiar(true, false, true).Should().BeFalse();
        i.Cambiar(true, true, true).Should().BeTrue();
    }

    [Fact]
    public void Al_tercer_fallo_el_correo_queda_fallido()
    {
        var c = CorreoPendiente.Crear(Guid.NewGuid(), ModuloNotificacion.Reuniones, TipoNotificacion.CompromisoVencido,
            "x", "/c/1", "/c/1", DateTime.UtcNow);
        c.RegistrarFallo("e", 3); c.RegistrarFallo("e", 3);
        c.Estado.Should().Be(EstadoCorreoPendiente.Pendiente);
        c.RegistrarFallo("e", 3);
        c.Estado.Should().Be(EstadoCorreoPendiente.Fallido);
        c.Intentos.Should().Be(3);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests\Domain.Tests --filter "FullyQualifiedName~NotificacionAgrupadaTests|FullyQualifiedName~AjustesNotificacionesTests"`
Expected: falla la compilación: no existen `AjustesNotificaciones`, `Sumar`, etc.

- [ ] **Step 3: Write the entities**

`src/Domain/Entities/AjustesNotificaciones.cs`:

```csharp
using Diger.TramitesEstado.Domain.Common;

namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Configuración de las notificaciones que se administra desde Panel de control → Notificaciones.
/// Es una sola fila (<see cref="IdUnico"/>). Mientras <see cref="SmtpConfigurado"/> sea false, el
/// correo sale con la sección <c>Smtp</c> de appsettings: el panel toma el control en el primer
/// guardado de su bloque de correo.
/// </summary>
public sealed class AjustesNotificaciones : BaseAuditableEntity
{
    public const int IdUnico             = 1;
    public const int MinimoMinutos       = 5;
    public const int MaximoMinutos       = 24 * 60;
    public const int IntervaloPorDefecto = 240;

    public int       IntervaloMinutos    { get; private set; }
    public bool      SmtpConfigurado     { get; private set; }
    public string?   SmtpHost            { get; private set; }
    public int       SmtpPuerto          { get; private set; } = 587;
    public bool      SmtpSsl             { get; private set; } = true;
    public string?   SmtpUsuario         { get; private set; }
    public string?   SmtpRemitente       { get; private set; }
    public string?   SmtpNombreRemitente { get; private set; }

    /// <summary>Cifrada con Data Protection (propósito <c>PanelControl.Notificaciones.Smtp</c>).
    /// Nunca en claro, nunca hacia el navegador.</summary>
    public string?   SmtpPasswordCifrada { get; private set; }

    public DateTime? UltimaRevisionUtc   { get; private set; }

    private AjustesNotificaciones() { }

    public static AjustesNotificaciones CrearPorDefecto(int intervaloMinutos = IntervaloPorDefecto)
    {
        var a = new AjustesNotificaciones { Id = IdUnico };
        a.CambiarIntervalo(Math.Clamp(intervaloMinutos, MinimoMinutos, MaximoMinutos));
        return a;
    }

    public void CambiarIntervalo(int minutos)
    {
        if (minutos < MinimoMinutos || minutos > MaximoMinutos)
            throw new DomainException("La revisión automática tiene que ser de entre 5 minutos y 24 horas.");
        IntervaloMinutos = minutos;
    }

    public void ConfigurarSmtp(string host, int puerto, bool ssl, string usuario, string remitente, string? nombreRemitente)
    {
        if (string.IsNullOrWhiteSpace(host))      throw new DomainException("Falta el servidor de correo.");
        if (puerto is < 1 or > 65535)             throw new DomainException("El puerto tiene que estar entre 1 y 65535.");
        if (string.IsNullOrWhiteSpace(usuario))   throw new DomainException("Falta el usuario del correo.");
        if (string.IsNullOrWhiteSpace(remitente)) throw new DomainException("Falta el correo remitente.");

        SmtpHost            = host.Trim();
        SmtpPuerto          = puerto;
        SmtpSsl             = ssl;
        SmtpUsuario         = usuario.Trim();
        SmtpRemitente       = remitente.Trim();
        SmtpNombreRemitente = string.IsNullOrWhiteSpace(nombreRemitente) ? null : nombreRemitente.Trim();
        SmtpConfigurado     = true;
    }

    public void FijarPasswordCifrada(string? cifrada) => SmtpPasswordCifrada = cifrada;

    public void MarcarRevision(DateTime utc) => UltimaRevisionUtc = utc;
}
```

`src/Domain/Entities/InterruptorNotificacion.cs`:

```csharp
namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>Los tres interruptores de un módulo en el Panel de control. Tabla <c>ModulosNotificacion</c>.</summary>
public sealed class InterruptorNotificacion
{
    public ModuloNotificacion Modulo        { get; private set; }
    public bool               CampanaActiva { get; private set; }
    public bool               CorreoActivo  { get; private set; }
    public bool               Agrupar       { get; private set; }

    private InterruptorNotificacion() { }

    public static InterruptorNotificacion Crear(ModuloNotificacion modulo, bool campana = true, bool correo = false, bool agrupar = true)
        => new() { Modulo = modulo, CampanaActiva = campana, CorreoActivo = correo, Agrupar = agrupar };

    /// <returns>Si algo cambió: un guardado sin cambios no ensucia la bitácora.</returns>
    public bool Cambiar(bool campana, bool correo, bool agrupar)
    {
        var cambio = CampanaActiva != campana || CorreoActivo != correo || Agrupar != agrupar;
        CampanaActiva = campana;
        CorreoActivo  = correo;
        Agrupar       = agrupar;
        return cambio;
    }
}
```

`src/Domain/Entities/NotificacionElemento.cs`:

```csharp
namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Una cosa dentro de un aviso: el compromiso, el ticket, la etapa. Un aviso agrupado tiene varios.
/// <see cref="Clave"/> identifica al elemento —por defecto su URL— y es contra lo que se mide «ya se
/// avisó hoy»: con avisos agrupados, la URL del aviso dejó de servir para eso.
/// </summary>
public sealed class NotificacionElemento : BaseEntity
{
    public const int LargoClave = 500;

    public int      NotificacionId { get; private set; }
    public string   Clave          { get; private set; } = "";
    public string   Titulo         { get; private set; } = "";
    public string?  Url            { get; private set; }
    public DateTime FechaUtc       { get; private set; }

    private NotificacionElemento() { }

    internal static NotificacionElemento Crear(string clave, string titulo, string? url, DateTime fechaUtc) => new()
    {
        Clave    = Notificacion.Recortar(clave, LargoClave),
        Titulo   = Notificacion.Recortar(titulo, Notificacion.LargoTitulo),
        Url      = url,
        FechaUtc = fechaUtc,
    };
}
```

`src/Domain/Entities/CorreoPendiente.cs`:

```csharp
namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Un aviso que tiene que salir por correo. Los avisos no mandan correo en el acto: se anotan acá y
/// el despachador junta lo de cada persona en un solo correo al final de cada revisión. En la
/// entrega 2, las reglas de correo deciden aquí cuándo sale.
/// </summary>
public sealed class CorreoPendiente : BaseEntity
{
    public Guid                  DestinatarioId { get; private set; }
    public ModuloNotificacion    Modulo         { get; private set; }
    public TipoNotificacion      Tipo           { get; private set; }
    public string                Titulo         { get; private set; } = "";
    public string?               Url            { get; private set; }
    public string                Clave          { get; private set; } = "";
    public DateTime              CreadoUtc      { get; private set; }
    public EstadoCorreoPendiente Estado         { get; private set; }
    public int                   Intentos       { get; private set; }
    public string?               UltimoError    { get; private set; }
    public DateTime?             EnviadoUtc     { get; private set; }

    private CorreoPendiente() { }

    public static CorreoPendiente Crear(Guid destinatarioId, ModuloNotificacion modulo, TipoNotificacion tipo,
                                        string titulo, string? url, string clave, DateTime creadoUtc) => new()
    {
        DestinatarioId = destinatarioId,
        Modulo         = modulo,
        Tipo           = tipo,
        Titulo         = Notificacion.Recortar(titulo, Notificacion.LargoTitulo),
        Url            = url,
        Clave          = Notificacion.Recortar(clave, NotificacionElemento.LargoClave),
        CreadoUtc      = creadoUtc,
        Estado         = EstadoCorreoPendiente.Pendiente,
    };

    public void MarcarEnviado(DateTime utc)
    {
        Estado      = EstadoCorreoPendiente.Enviado;
        EnviadoUtc  = utc;
        UltimoError = null;
    }

    public void RegistrarFallo(string error, int maxIntentos)
    {
        Intentos++;
        UltimoError = Notificacion.Recortar(error, 500);
        if (Intentos >= maxIntentos) Estado = EstadoCorreoPendiente.Fallido;
    }

    /// <summary>No tiene sentido reintentar (destinatario sin correo o inactivo).</summary>
    public void Descartar(string motivo)
    {
        Estado      = EstadoCorreoPendiente.Fallido;
        UltimoError = Notificacion.Recortar(motivo, 500);
    }
}
```

`src/Domain/Entities/PanelControlBitacora.cs`:

```csharp
namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Bitácora de solo agregar del Panel de control: qué cambió, quién y cuándo. Mismo patrón que
/// <see cref="PermisoAuditoria"/>, que no sirve acá porque sus columnas son las de la matriz de
/// permisos. <b>El valor de una contraseña nunca se escribe</b>: solo «contraseña cambiada».
/// </summary>
public sealed class PanelControlBitacora : BaseEntity
{
    public string   Seccion { get; private set; } = "";
    public string   Detalle { get; private set; } = "";
    public string   Actor   { get; private set; } = "";
    public DateTime Fecha   { get; private set; }

    private PanelControlBitacora() { }

    public static PanelControlBitacora Crear(string seccion, string detalle, string actor, DateTime fechaUtc) => new()
    {
        Seccion = Notificacion.Recortar(seccion, 100),
        Detalle = Notificacion.Recortar(detalle, 1000),
        Actor   = Notificacion.Recortar(actor, 200),
        Fecha   = fechaUtc,
    };
}
```

`src/Domain/Entities/Notificacion.cs` completo:

```csharp
namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Un aviso de la campana. Puede juntar varios elementos del mismo tipo («Tiene 6 compromisos
/// vencidos»): <see cref="Sumar"/> agrega a un aviso sin leer, y al leerlo lo siguiente empieza un
/// aviso nuevo, para que nada nuevo quede escondido dentro de algo ya leído.
/// </summary>
public sealed class Notificacion : BaseEntity
{
    public const int LargoTitulo = 200;

    public Guid              DestinatarioId     { get; private set; }
    public TipoNotificacion  Tipo               { get; private set; }
    public string            Titulo             { get; private set; } = "";
    public string?           Url                { get; private set; }
    public bool              Leida              { get; private set; }
    public DateTime          FechaCreacion      { get; private set; }
    public DateTime?         FechaLectura       { get; private set; }
    public int               Cantidad           { get; private set; } = 1;

    /// <summary>Cuándo entró el último elemento. La campana ordena por esto.</summary>
    public DateTime          FechaActualizacion { get; private set; }

    private readonly List<NotificacionElemento> _elementos = [];
    public IReadOnlyCollection<NotificacionElemento> Elementos => _elementos.AsReadOnly();

    /// <summary>Adónde lleva el aviso: con un elemento, directo a él; con varios, a la lista.</summary>
    public string? Destino => Cantidad > 1 ? RutaDetalle(Id) : Url;

    public static string RutaDetalle(int id) => $"/Notificaciones/{id}";

    private Notificacion() { }

    public static Notificacion Crear(Guid destinatarioId, TipoNotificacion tipo, string titulo,
                                     string? url = null, string? clave = null, DateTime? ahoraUtc = null)
    {
        var ahora = ahoraUtc ?? DateTime.UtcNow;
        var n = new Notificacion
        {
            DestinatarioId     = destinatarioId,
            Tipo               = tipo,
            Titulo             = Recortar(titulo, LargoTitulo),
            Url                = url,
            Leida              = false,
            FechaCreacion      = ahora,
            FechaActualizacion = ahora,
            Cantidad           = 1,
        };
        n._elementos.Add(NotificacionElemento.Crear(clave ?? url ?? titulo, titulo, url, ahora));
        return n;
    }

    /// <summary>Junta un elemento más en este aviso.</summary>
    /// <returns>Si el elemento era nuevo. Si ya estaba, el aviso solo sube en la campana.</returns>
    public bool Sumar(string clave, string titulo, string? url, Func<int, string> tituloGrupo, DateTime ahoraUtc)
    {
        if (Leida)
            throw new InvalidOperationException("No se suma a un aviso ya leído: lo nuevo va en un aviso nuevo.");

        FechaActualizacion = ahoraUtc;
        if (_elementos.Any(e => e.Clave == clave)) return false;

        _elementos.Add(NotificacionElemento.Crear(clave, titulo, url, ahoraUtc));
        Cantidad = _elementos.Count;
        Titulo   = Recortar(tituloGrupo(Cantidad), LargoTitulo);
        return true;
    }

    public void MarcarLeida()
    {
        if (Leida) return;
        Leida        = true;
        FechaLectura = DateTime.UtcNow;
    }

    /// <summary>Recorta al largo de la columna en vez de dejar que SQL Server rechace el guardado.</summary>
    public static string Recortar(string texto, int largo) =>
        texto.Length <= largo ? texto : texto[..(largo - 1)] + "…";
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests\Domain.Tests`
Expected: PASS. Luego `dotnet build Diger.TramitesEstado.sln`: debe compilar. `ProyectoEstadoCambiadoEventHandler` sigue usando `Notificacion.Crear` con cuatro argumentos, y eso compila.

- [ ] **Step 5: Commit**

```powershell
git add src/Domain tests/Domain.Tests
git commit -m "Dominio del Panel de control: ajustes, interruptores, bandeja de correo y avisos agrupados"
```

---

### Task 3: Persistencia, migración y script SQL

**Files:**
- Modify: `src/Application/Common/Interfaces/IRepositories.cs` (interfaz `IApplicationDbContext`, línea ~250)
- Modify: `src/Infrastructure/Persistence/AppDbContext.cs` (DbSets cerca de la línea 66; `NotificacionConfiguration` en la línea ~1725)
- Create: `src/Infrastructure/Persistence/Migrations/<timestamp>_PanelControlNotificaciones.cs` (generado)
- Create: `database/2026-10-22_panel_control_notificaciones.sql`

**Interfaces:**
- Consumes: entidades de la Task 2.
- Produces: `IApplicationDbContext.AjustesNotificaciones`, `.InterruptoresNotificacion`, `.NotificacionElementos`, `.CorreosPendientes`, `.PanelControlBitacora`.

- [ ] **Step 1: Agregar los DbSets**

En `IApplicationDbContext`, debajo de `DbSet<Notificacion> Notificaciones { get; }`:

```csharp
    DbSet<NotificacionElemento>     NotificacionElementos { get; }
    DbSet<AjustesNotificaciones>    AjustesNotificaciones { get; }
    DbSet<InterruptorNotificacion>  InterruptoresNotificacion { get; }
    DbSet<CorreoPendiente>          CorreosPendientes { get; }
    DbSet<PanelControlBitacora>     PanelControlBitacora { get; }
```

En `AppDbContext`, debajo de `public DbSet<Notificacion> Notificaciones ...`:

```csharp
    public DbSet<NotificacionElemento>    NotificacionElementos     { get; init; } = default!;
    public DbSet<AjustesNotificaciones>   AjustesNotificaciones     { get; init; } = default!;
    public DbSet<InterruptorNotificacion> InterruptoresNotificacion { get; init; } = default!;
    public DbSet<CorreoPendiente>         CorreosPendientes         { get; init; } = default!;
    public DbSet<PanelControlBitacora>    PanelControlBitacora      { get; init; } = default!;
```

- [ ] **Step 2: Configuraciones EF**

Sustituir `NotificacionConfiguration` por la siguiente versión y poner las cinco nuevas a continuación. `ApplyConfigurationsFromAssembly` las recoge solas.

```csharp
public sealed class NotificacionConfiguration : IEntityTypeConfiguration<Notificacion>
{
    public void Configure(EntityTypeBuilder<Notificacion> b)
    {
        b.ToTable("Notificaciones");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Titulo).HasMaxLength(Notificacion.LargoTitulo).IsRequired();
        b.Property(x => x.Url).HasMaxLength(500);
        b.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Cantidad).HasDefaultValue(1);
        b.Ignore(x => x.Destino);
        b.HasMany(x => x.Elementos).WithOne().HasForeignKey(e => e.NotificacionId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Elementos).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(x => new { x.DestinatarioId, x.Leida });
        b.HasIndex(x => x.FechaCreacion);
    }
}

public sealed class NotificacionElementoConfiguration : IEntityTypeConfiguration<NotificacionElemento>
{
    public void Configure(EntityTypeBuilder<NotificacionElemento> b)
    {
        b.ToTable("NotificacionElementos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Clave).HasMaxLength(NotificacionElemento.LargoClave).IsRequired();
        b.Property(x => x.Titulo).HasMaxLength(Notificacion.LargoTitulo).IsRequired();
        b.Property(x => x.Url).HasMaxLength(500);
        b.HasIndex(x => x.NotificacionId);
        b.HasIndex(x => new { x.Clave, x.FechaUtc });
    }
}

public sealed class AjustesNotificacionesConfiguration : IEntityTypeConfiguration<AjustesNotificaciones>
{
    public void Configure(EntityTypeBuilder<AjustesNotificaciones> b)
    {
        b.ToTable("AjustesNotificaciones");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.SmtpHost).HasMaxLength(200);
        b.Property(x => x.SmtpUsuario).HasMaxLength(200);
        b.Property(x => x.SmtpRemitente).HasMaxLength(200);
        b.Property(x => x.SmtpNombreRemitente).HasMaxLength(200);
        b.Property(x => x.CreatedBy).HasMaxLength(200);
        b.Property(x => x.UpdatedBy).HasMaxLength(200);
    }
}

public sealed class InterruptorNotificacionConfiguration : IEntityTypeConfiguration<InterruptorNotificacion>
{
    public void Configure(EntityTypeBuilder<InterruptorNotificacion> b)
    {
        b.ToTable("ModulosNotificacion");
        b.HasKey(x => x.Modulo);
        b.Property(x => x.Modulo).HasConversion<string>().HasMaxLength(30).ValueGeneratedNever();
    }
}

public sealed class CorreoPendienteConfiguration : IEntityTypeConfiguration<CorreoPendiente>
{
    public void Configure(EntityTypeBuilder<CorreoPendiente> b)
    {
        b.ToTable("CorreosPendientes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Modulo).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Titulo).HasMaxLength(Notificacion.LargoTitulo).IsRequired();
        b.Property(x => x.Url).HasMaxLength(500);
        b.Property(x => x.Clave).HasMaxLength(NotificacionElemento.LargoClave).IsRequired();
        b.Property(x => x.UltimoError).HasMaxLength(500);
        b.HasIndex(x => new { x.Estado, x.DestinatarioId });
    }
}

public sealed class PanelControlBitacoraConfiguration : IEntityTypeConfiguration<PanelControlBitacora>
{
    public void Configure(EntityTypeBuilder<PanelControlBitacora> b)
    {
        b.ToTable("PanelControlBitacora");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Seccion).HasMaxLength(100).IsRequired();
        b.Property(x => x.Detalle).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Actor).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Fecha);
    }
}
```

Run: `dotnet build Diger.TramitesEstado.sln`
Expected: compila. Si falla con MSB3027/MSB3021, cerrar `Diger.TramitesEstado.Web` (autorizado en la memoria del proyecto); a Visual Studio no se lo toca.

- [ ] **Step 3: Generar la migración**

Run: `dotnet ef migrations add PanelControlNotificaciones --project src\Infrastructure --startup-project src\Web --output-dir Persistence\Migrations`

Abrir el archivo generado y, **al final de `Up`**, después de los `CreateTable`/`AddColumn`, agregar:

```csharp
            // Los avisos que ya existen pasan a tener su elemento y su fecha de actualización: sin
            // eso, el primer día no sabrían que ya se avisó lo que avisaron y lo repetirían.
            migrationBuilder.Sql("UPDATE Notificaciones SET FechaActualizacion = FechaCreacion;");
            migrationBuilder.Sql(@"
                INSERT INTO NotificacionElementos (NotificacionId, Clave, Titulo, Url, FechaUtc)
                SELECT Id, LEFT(COALESCE(Url, Titulo), 500), Titulo, Url, FechaCreacion
                FROM Notificaciones;");

            // Valores iniciales: correo apagado en todos los módulos (decisión del 2026-10-22).
            migrationBuilder.Sql(@"
                INSERT INTO AjustesNotificaciones (Id, IntervaloMinutos, SmtpConfigurado, SmtpPuerto, SmtpSsl, CreatedAt)
                VALUES (1, 240, 0, 587, 1, SYSUTCDATETIME());");
            migrationBuilder.Sql(@"
                INSERT INTO ModulosNotificacion (Modulo, CampanaActiva, CorreoActivo, Agrupar) VALUES
                (N'Tickets', 1, 0, 1), (N'Reuniones', 1, 0, 1), (N'Expedientes', 1, 0, 1),
                (N'Proyectos', 1, 0, 1), (N'Soporte', 1, 0, 1);");
```

Comprobar en el archivo generado que `AddColumn<DateTime>("FechaActualizacion")` y `AddColumn<int>("Cantidad", defaultValue: 1)` estén en `Notificaciones`. No editar el snapshot a mano: lo escribe `migrations add` (memoria «El snapshot de EF no se fusiona a mano»).

Run: `dotnet build Diger.TramitesEstado.sln`
Expected: compila.

- [ ] **Step 4: Escribir el script SQL**

Ver el SQL que EF genera para comparar los tipos de columna:
`dotnet ef migrations script 20261019174802_AddPasswordCambiadoEl PanelControlNotificaciones --project src\Infrastructure --startup-project src\Web`
(si el `migrations list` muestra otra migración inmediatamente anterior, usar esa como origen).

Crear `database/2026-10-22_panel_control_notificaciones.sql` con esta estructura. Los tipos tienen que coincidir con el SQL generado. Sustituir `<MIGRATION_ID>` por el nombre del archivo generado sin `.cs`, por ejemplo `20261022213000_PanelControlNotificaciones`:

```sql
/* ════════════════════════════════════════════════════════════════════════════
   2026-10-22 · Panel de control → Notificaciones
   Va DESPUÉS de 2026-10-22_poner_al_dia.sql. Corra antes 00_diagnostico.sql si no
   sabe en qué estado está la base.

   QUÉ TRAE (todo es la migración EF <MIGRATION_ID>):
     1. Notificaciones.Cantidad y Notificaciones.FechaActualizacion: avisos agrupados.
     2. NotificacionElementos: los elementos de cada aviso, con los avisos existentes
        cargados (uno por aviso) para que el primer día no se repita lo ya avisado.
     3. AjustesNotificaciones (fila única): intervalo de revisión, correo saliente.
     4. ModulosNotificacion: interruptores Campana / Correo / Agrupar por módulo.
        Nacen con el CORREO APAGADO en los cinco: los recordatorios manuales dejan
        de mandar correo hasta que se encienda en el panel.
     5. CorreosPendientes: bandeja de salida.
     6. PanelControlBitacora: quién cambió qué en el panel.

   Cada paso se decide contra el catálogo de la base, no contra __EFMigrationsHistory.
   La fila del historial se escribe al final. Correrlo dos veces no cambia nada.

   CÓMO CORRERLO: sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-22_panel_control_notificaciones.sql
   ════════════════════════════════════════════════════════════════════════════ */
SET XACT_ABORT ON;
SET NOCOUNT ON;

IF OBJECT_ID(N'[Notificaciones]', 'U') IS NULL
    THROW 50001, N'No existe la tabla Notificaciones: esta no es una base del portal o le faltan scripts anteriores. Corra 00_diagnostico.sql.', 1;
GO

BEGIN TRANSACTION;

PRINT '── 1. Notificaciones: Cantidad y FechaActualizacion ──────────────────';
IF COL_LENGTH('Notificaciones', 'Cantidad') IS NULL
BEGIN
    ALTER TABLE Notificaciones ADD Cantidad int NOT NULL CONSTRAINT DF_Notificaciones_Cantidad DEFAULT 1;
    PRINT '   Cantidad agregada';
END ELSE PRINT '   Cantidad ya estaba';

IF COL_LENGTH('Notificaciones', 'FechaActualizacion') IS NULL
BEGIN
    ALTER TABLE Notificaciones ADD FechaActualizacion datetime2 NOT NULL
        CONSTRAINT DF_Notificaciones_FechaActualizacion DEFAULT '0001-01-01T00:00:00';
    EXEC (N'UPDATE Notificaciones SET FechaActualizacion = FechaCreacion;');
    PRINT '   FechaActualizacion agregada y cargada';
END ELSE PRINT '   FechaActualizacion ya estaba';

PRINT '── 2. NotificacionElementos ──────────────────────────────────────────';
IF OBJECT_ID(N'[NotificacionElementos]', 'U') IS NULL
BEGIN
    CREATE TABLE NotificacionElementos (
        Id             int IDENTITY(1,1) NOT NULL CONSTRAINT PK_NotificacionElementos PRIMARY KEY,
        NotificacionId int            NOT NULL,
        Clave          nvarchar(500)  NOT NULL,
        Titulo         nvarchar(200)  NOT NULL,
        Url            nvarchar(500)  NULL,
        FechaUtc       datetime2      NOT NULL,
        CONSTRAINT FK_NotificacionElementos_Notificaciones_NotificacionId
            FOREIGN KEY (NotificacionId) REFERENCES Notificaciones (Id) ON DELETE CASCADE);
    CREATE INDEX IX_NotificacionElementos_NotificacionId ON NotificacionElementos (NotificacionId);
    CREATE INDEX IX_NotificacionElementos_Clave_FechaUtc ON NotificacionElementos (Clave, FechaUtc);
    EXEC (N'INSERT INTO NotificacionElementos (NotificacionId, Clave, Titulo, Url, FechaUtc)
            SELECT Id, LEFT(COALESCE(Url, Titulo), 500), Titulo, Url, FechaCreacion FROM Notificaciones;');
    PRINT '   creada y cargada con los avisos existentes';
END ELSE PRINT '   ya estaba';

PRINT '── 3. AjustesNotificaciones ──────────────────────────────────────────';
IF OBJECT_ID(N'[AjustesNotificaciones]', 'U') IS NULL
BEGIN
    CREATE TABLE AjustesNotificaciones (
        Id                  int            NOT NULL CONSTRAINT PK_AjustesNotificaciones PRIMARY KEY,
        IntervaloMinutos    int            NOT NULL,
        SmtpConfigurado     bit            NOT NULL,
        SmtpHost            nvarchar(200)  NULL,
        SmtpPuerto          int            NOT NULL,
        SmtpSsl             bit            NOT NULL,
        SmtpUsuario         nvarchar(200)  NULL,
        SmtpRemitente       nvarchar(200)  NULL,
        SmtpNombreRemitente nvarchar(200)  NULL,
        SmtpPasswordCifrada nvarchar(max)  NULL,
        UltimaRevisionUtc   datetime2      NULL,
        CreatedAt           datetime2      NOT NULL,
        CreatedBy           nvarchar(200)  NULL,
        UpdatedAt           datetime2      NULL,
        UpdatedBy           nvarchar(200)  NULL);
    PRINT '   creada';
END ELSE PRINT '   ya estaba';

IF NOT EXISTS (SELECT 1 FROM AjustesNotificaciones WHERE Id = 1)
BEGIN
    INSERT INTO AjustesNotificaciones (Id, IntervaloMinutos, SmtpConfigurado, SmtpPuerto, SmtpSsl, CreatedAt)
    VALUES (1, 240, 0, 587, 1, SYSUTCDATETIME());
    PRINT '   fila inicial: revisión cada 4 h, correo desde appsettings';
END ELSE PRINT '   fila inicial ya estaba';

PRINT '── 4. ModulosNotificacion ────────────────────────────────────────────';
IF OBJECT_ID(N'[ModulosNotificacion]', 'U') IS NULL
BEGIN
    CREATE TABLE ModulosNotificacion (
        Modulo        nvarchar(30) NOT NULL CONSTRAINT PK_ModulosNotificacion PRIMARY KEY,
        CampanaActiva bit          NOT NULL,
        CorreoActivo  bit          NOT NULL,
        Agrupar       bit          NOT NULL);
    PRINT '   creada';
END ELSE PRINT '   ya estaba';

INSERT INTO ModulosNotificacion (Modulo, CampanaActiva, CorreoActivo, Agrupar)
SELECT v.Modulo, 1, 0, 1
FROM (VALUES (N'Tickets'), (N'Reuniones'), (N'Expedientes'), (N'Proyectos'), (N'Soporte')) v(Modulo)
WHERE NOT EXISTS (SELECT 1 FROM ModulosNotificacion m WHERE m.Modulo = v.Modulo);
PRINT CONCAT('   módulos agregados: ', @@ROWCOUNT);

PRINT '── 5. CorreosPendientes ──────────────────────────────────────────────';
IF OBJECT_ID(N'[CorreosPendientes]', 'U') IS NULL
BEGIN
    CREATE TABLE CorreosPendientes (
        Id             int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CorreosPendientes PRIMARY KEY,
        DestinatarioId uniqueidentifier NOT NULL,
        Modulo         nvarchar(30)     NOT NULL,
        Tipo           nvarchar(30)     NOT NULL,
        Titulo         nvarchar(200)    NOT NULL,
        Url            nvarchar(500)    NULL,
        Clave          nvarchar(500)    NOT NULL,
        CreadoUtc      datetime2        NOT NULL,
        Estado         nvarchar(20)     NOT NULL,
        Intentos       int              NOT NULL,
        UltimoError    nvarchar(500)    NULL,
        EnviadoUtc     datetime2        NULL);
    CREATE INDEX IX_CorreosPendientes_Estado_DestinatarioId ON CorreosPendientes (Estado, DestinatarioId);
    PRINT '   creada';
END ELSE PRINT '   ya estaba';

PRINT '── 6. PanelControlBitacora ───────────────────────────────────────────';
IF OBJECT_ID(N'[PanelControlBitacora]', 'U') IS NULL
BEGIN
    CREATE TABLE PanelControlBitacora (
        Id      int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PanelControlBitacora PRIMARY KEY,
        Seccion nvarchar(100)  NOT NULL,
        Detalle nvarchar(1000) NOT NULL,
        Actor   nvarchar(200)  NOT NULL,
        Fecha   datetime2      NOT NULL);
    CREATE INDEX IX_PanelControlBitacora_Fecha ON PanelControlBitacora (Fecha);
    PRINT '   creada';
END ELSE PRINT '   ya estaba';

PRINT '── 7. Historial de migraciones ───────────────────────────────────────';
IF OBJECT_ID(N'[__EFMigrationsHistory]', 'U') IS NOT NULL
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    SELECT N'<MIGRATION_ID>', N'9.0.0'
    WHERE NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'<MIGRATION_ID>');
    PRINT '   historial al día';
END
ELSE PRINT '   *** no existe __EFMigrationsHistory: no se registró nada';

COMMIT;
GO

PRINT '';
PRINT '════════ Comprobación ════════';
SELECT v.Control,
       CASE WHEN COL_LENGTH(v.Tabla, v.Columna) IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
FROM (VALUES
        (N'Notificaciones.Cantidad',           N'Notificaciones',        N'Cantidad'),
        (N'Notificaciones.FechaActualizacion', N'Notificaciones',        N'FechaActualizacion'),
        (N'NotificacionElementos',             N'NotificacionElementos', N'Clave'),
        (N'AjustesNotificaciones',             N'AjustesNotificaciones', N'IntervaloMinutos'),
        (N'ModulosNotificacion',               N'ModulosNotificacion',   N'CorreoActivo'),
        (N'CorreosPendientes',                 N'CorreosPendientes',     N'Estado'),
        (N'PanelControlBitacora',              N'PanelControlBitacora',  N'Detalle')
     ) v(Control, Tabla, Columna)
UNION ALL
SELECT N'Fila de ajustes', CASE WHEN EXISTS (SELECT 1 FROM AjustesNotificaciones WHERE Id = 1) THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Cinco módulos', CASE WHEN (SELECT COUNT(*) FROM ModulosNotificacion) = 5 THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT N'Migración registrada',
       CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'<MIGRATION_ID>') THEN 'presente' ELSE 'FALTA' END;

PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
GO
```

Los `EXEC (N'...')` son a propósito. Una columna o tabla agregada en el mismo lote no es visible para las sentencias compiladas de ese lote, y el `EXEC` las compila aparte.

- [ ] **Step 5: Correr el script dos veces contra `GestionGD_new`**

Run (dos veces):
`sqlcmd -S "localhost\SQL2025" -E -d GestionGD_new -b -I -f 65001 -i database\2026-10-22_panel_control_notificaciones.sql`
Expected: la primera dice «agregada/creada» y todos los controles dicen `presente`. La segunda dice «ya estaba» en todo, `módulos agregados: 0` y otra vez `presente`.

Run: `dotnet ef migrations list --project src\Infrastructure --startup-project src\Web`
Expected: `PanelControlNotificaciones` sin «(Pending)».

- [ ] **Step 6: Commit**

```powershell
git add src/Application/Common/Interfaces/IRepositories.cs src/Infrastructure/Persistence database/2026-10-22_panel_control_notificaciones.sql
git commit -m "Persistencia del Panel de control: migracion y script idempotente"
```

---

### Task 4: Proveedor de ajustes y cifrado de la contraseña

**Files:**
- Create: `src/Application/Notificaciones/IAjustesNotificaciones.cs`
- Create: `src/Infrastructure/Security/ProtectorSecretos.cs`
- Create: `src/Infrastructure/Notifications/AjustesNotificacionesProvider.cs`
- Modify: `src/Infrastructure/DependencyInjection.cs` (cerca de las líneas 66–70)
- Test: `tests/Application.Tests/Notificaciones/AjustesNotificacionesProviderTests.cs`
- Create (helper de pruebas): `tests/Application.Tests/Notificaciones/AjustesDePrueba.cs`

**Interfaces:**
- Produces:
  - `record InterruptoresModulo(bool Campana, bool Correo, bool Agrupar)` con `PorDefecto`.
  - `record AjustesVigentes(int IntervaloMinutos, IReadOnlyDictionary<ModuloNotificacion, InterruptoresModulo> Modulos, DateTime? UltimaRevisionUtc)` con `De(ModuloNotificacion)`.
  - `record SmtpVigente(string Host, int Puerto, bool Ssl, string Usuario, string Password, string Remitente, string NombreRemitente, int TimeoutSegundos, bool DesdePanel, bool HayPassword, bool PasswordIlegible)` con `EstaConfigurado`.
  - `interface IAjustesNotificaciones { Task<AjustesVigentes> ObtenerAsync(CancellationToken ct = default); Task<SmtpVigente> SmtpAsync(CancellationToken ct = default); void Invalidar(); }`.
  - `interface IProtectorSecretos { string Cifrar(string texto); string? Descifrar(string cifrado); }`.
  - `sealed class SenalRevision { void Disparar(); Task<bool> EsperarAsync(TimeSpan, CancellationToken); }`.
  - En las pruebas: `AjustesDePrueba` (implementa `IAjustesNotificaciones`) y `NotificacionesDePrueba.Servicio(...)`. Este último se agrega en la Task 5.

- [ ] **Step 1: Contratos (Application)**

`src/Application/Notificaciones/IAjustesNotificaciones.cs`:

```csharp
namespace Diger.TramitesEstado.Application.Notificaciones;

public sealed record InterruptoresModulo(bool Campana, bool Correo, bool Agrupar)
{
    /// <summary>Lo que rige para un módulo sin fila: campana sí, correo no, agrupar sí.</summary>
    public static readonly InterruptoresModulo PorDefecto = new(true, false, true);
}

public sealed record AjustesVigentes(
    int IntervaloMinutos,
    IReadOnlyDictionary<ModuloNotificacion, InterruptoresModulo> Modulos,
    DateTime? UltimaRevisionUtc)
{
    public InterruptoresModulo De(ModuloNotificacion modulo) =>
        Modulos.TryGetValue(modulo, out var i) ? i : InterruptoresModulo.PorDefecto;
}

/// <summary>El correo saliente que rige ahora: el del panel o, si nunca se guardó, el de appsettings.</summary>
/// <param name="PasswordIlegible">Hay una contraseña guardada pero este servidor no la puede descifrar.</param>
public sealed record SmtpVigente(
    string Host, int Puerto, bool Ssl, string Usuario, string Password,
    string Remitente, string NombreRemitente, int TimeoutSegundos,
    bool DesdePanel, bool HayPassword, bool PasswordIlegible)
{
    /// <summary>Mismo criterio que <c>SmtpSettings.EstaConfigurado</c>, más la contraseña ilegible.</summary>
    public bool EstaConfigurado =>
        !PasswordIlegible &&
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(Usuario) &&
        !Usuario.Contains("ejemplo", StringComparison.OrdinalIgnoreCase);

    /// <summary>Un record imprime todas sus propiedades; este no puede imprimir la contraseña.</summary>
    public override string ToString() => $"SmtpVigente {{ Host = {Host}, Usuario = {Usuario}, Password = *** }}";
}

/// <summary>Los ajustes de notificaciones vigentes, con caché corta. <see cref="Invalidar"/> se llama al guardar.</summary>
public interface IAjustesNotificaciones
{
    Task<AjustesVigentes> ObtenerAsync(CancellationToken ct = default);
    Task<SmtpVigente>     SmtpAsync(CancellationToken ct = default);
    void                  Invalidar();
}

/// <summary>Cifra y descifra secretos guardados en la base. <see cref="Descifrar"/> devuelve null
/// si no se puede (claves de Data Protection perdidas): nunca lanza.</summary>
public interface IProtectorSecretos
{
    string  Cifrar(string texto);
    string? Descifrar(string cifrado);
}

/// <summary>
/// Despierta al revisor automático. Guardar el intervalo la dispara: así un cambio de 4 h a 15 min
/// aplica ya, y no al terminar la espera de 4 h que estaba en curso.
/// </summary>
public sealed class SenalRevision
{
    private readonly SemaphoreSlim _senal = new(0, 1);

    public void Disparar()
    {
        try { if (_senal.CurrentCount == 0) _senal.Release(); }
        catch (SemaphoreFullException) { /* ya estaba disparada */ }
    }

    /// <returns>true si la despertaron; false si se cumplió el tiempo.</returns>
    public Task<bool> EsperarAsync(TimeSpan tiempo, CancellationToken ct) => _senal.WaitAsync(tiempo, ct);
}
```

- [ ] **Step 2: Helper de pruebas y pruebas que fallan**

`tests/Application.Tests/Notificaciones/AjustesDePrueba.cs`:

```csharp
using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Domain.Enums;

namespace Diger.TramitesEstado.Application.Tests.Notificaciones;

/// <summary>Ajustes fijos para las pruebas: por defecto, campana sí y correo no, como el despliegue.</summary>
internal sealed class AjustesDePrueba : IAjustesNotificaciones
{
    public Dictionary<ModuloNotificacion, InterruptoresModulo> Modulos { get; } =
        Enum.GetValues<ModuloNotificacion>().ToDictionary(m => m, _ => InterruptoresModulo.PorDefecto);

    public int         IntervaloMinutos { get; set; } = 240;
    public SmtpVigente Smtp             { get; set; } = ConSmtp("smtp.prueba.local", "no-reply@prueba.local");
    public int         Invalidaciones   { get; private set; }

    public Task<AjustesVigentes> ObtenerAsync(CancellationToken ct = default) =>
        Task.FromResult(new AjustesVigentes(IntervaloMinutos, Modulos, null));

    public Task<SmtpVigente> SmtpAsync(CancellationToken ct = default) => Task.FromResult(Smtp);

    public void Invalidar() => Invalidaciones++;

    public AjustesDePrueba Con(ModuloNotificacion modulo, bool campana = true, bool correo = false, bool agrupar = true)
    {
        Modulos[modulo] = new InterruptoresModulo(campana, correo, agrupar);
        return this;
    }

    /// <summary>Correo encendido donde se admite: lo que necesitan las pruebas de recordatorios.</summary>
    public static AjustesDePrueba CorreoEncendido()
    {
        var a = new AjustesDePrueba();
        foreach (var m in Enum.GetValues<ModuloNotificacion>())
            a.Modulos[m] = new InterruptoresModulo(true, CatalogoNotificaciones.ModuloAdmiteCorreo(m), true);
        return a;
    }

    public static SmtpVigente ConSmtp(string host, string usuario, string password = "x") =>
        new(host, 587, true, usuario, password, "no-reply@prueba.local", "Prueba", 15,
            DesdePanel: false, HayPassword: password.Length > 0, PasswordIlegible: false);
}
```

`tests/Application.Tests/Notificaciones/AjustesNotificacionesProviderTests.cs`:

```csharp
using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Application.Tests.Expedientes; // FakeCurrentUser
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Email;
using Diger.TramitesEstado.Infrastructure.Notifications;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Security;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Notificaciones;

public sealed class AjustesNotificacionesProviderTests : IDisposable
{
    private readonly AppDbContext _ctx = new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());

    private readonly ProtectorSecretos _protector = new(new EphemeralDataProtectionProvider());
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private AjustesNotificacionesProvider Proveedor() => new(
        _ctx, _cache, _protector,
        Options.Create(new NotificacionesOptions { IntervaloHoras = 6 }),
        Options.Create(new SmtpSettings { Host = "smtp.gmail.com", Username = "no-reply@diger.gob.hn", Password = "deappsettings" }));

    [Fact]
    public async Task Sin_nada_guardado_rige_appsettings()
    {
        var p = Proveedor();

        (await p.ObtenerAsync()).IntervaloMinutos.Should().Be(360);
        (await p.ObtenerAsync()).De(ModuloNotificacion.Reuniones).Should().Be(InterruptoresModulo.PorDefecto);
        var smtp = await p.SmtpAsync();
        smtp.DesdePanel.Should().BeFalse();
        smtp.Host.Should().Be("smtp.gmail.com");
        smtp.Password.Should().Be("deappsettings");
    }

    [Fact]
    public async Task Lo_guardado_en_el_panel_manda_y_la_contrasena_se_descifra()
    {
        var fila = AjustesNotificaciones.CrearPorDefecto(15);
        fila.ConfigurarSmtp("smtp.office365.com", 587, true, "no-reply@diger.gob.hn", "no-reply@diger.gob.hn", null);
        fila.FijarPasswordCifrada(_protector.Cifrar("Secreta#1"));
        _ctx.AjustesNotificaciones.Add(fila);
        _ctx.InterruptoresNotificacion.Add(InterruptorNotificacion.Crear(ModuloNotificacion.Reuniones, correo: true));
        await _ctx.SaveChangesAsync();

        var p = Proveedor();
        (await p.ObtenerAsync()).IntervaloMinutos.Should().Be(15);
        (await p.ObtenerAsync()).De(ModuloNotificacion.Reuniones).Correo.Should().BeTrue();
        var smtp = await p.SmtpAsync();
        smtp.DesdePanel.Should().BeTrue();
        smtp.Password.Should().Be("Secreta#1");
        smtp.ToString().Should().NotContain("Secreta#1");
    }

    [Fact]
    public async Task Soporte_nunca_queda_con_correo_aunque_la_fila_lo_diga()
    {
        _ctx.InterruptoresNotificacion.Add(InterruptorNotificacion.Crear(ModuloNotificacion.Soporte, correo: true));
        await _ctx.SaveChangesAsync();

        (await Proveedor().ObtenerAsync()).De(ModuloNotificacion.Soporte).Correo.Should().BeFalse();
    }

    [Fact]
    public async Task Una_contrasena_que_no_se_puede_descifrar_se_marca_ilegible()
    {
        var fila = AjustesNotificaciones.CrearPorDefecto();
        fila.ConfigurarSmtp("smtp.office365.com", 587, true, "u@diger.gob.hn", "u@diger.gob.hn", null);
        fila.FijarPasswordCifrada("cifrada-con-otras-claves");
        _ctx.AjustesNotificaciones.Add(fila);
        await _ctx.SaveChangesAsync();

        var smtp = await Proveedor().SmtpAsync();
        smtp.PasswordIlegible.Should().BeTrue();
        smtp.EstaConfigurado.Should().BeFalse();
    }

    [Fact]
    public async Task Invalidar_hace_releer_la_base()
    {
        var p = Proveedor();
        (await p.ObtenerAsync()).IntervaloMinutos.Should().Be(360);

        _ctx.AjustesNotificaciones.Add(AjustesNotificaciones.CrearPorDefecto(30));
        await _ctx.SaveChangesAsync();
        (await p.ObtenerAsync()).IntervaloMinutos.Should().Be(360, "sigue en caché");

        p.Invalidar();
        (await p.ObtenerAsync()).IntervaloMinutos.Should().Be(30);
    }

    [Fact]
    public async Task La_senal_despierta_al_que_espera()
    {
        var s = new SenalRevision();
        var espera = s.EsperarAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        s.Disparar();
        (await espera).Should().BeTrue();
        (await s.EsperarAsync(TimeSpan.FromMilliseconds(10), CancellationToken.None)).Should().BeFalse();
    }

    public void Dispose() { _ctx.Dispose(); _cache.Dispose(); }
}
```

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~AjustesNotificacionesProviderTests"`
Expected: falla la compilación: no existen `ProtectorSecretos` ni `AjustesNotificacionesProvider`.

- [ ] **Step 3: Implementación (Infrastructure)**

`src/Infrastructure/Security/ProtectorSecretos.cs`:

```csharp
using System.Security.Cryptography;
using Diger.TramitesEstado.Application.Notificaciones;
using Microsoft.AspNetCore.DataProtection;

namespace Diger.TramitesEstado.Infrastructure.Security;

/// <summary>
/// Cifra la contraseña del correo saliente con Data Protection. Las claves viven en la carpeta que
/// configura Program.cs (<c>PersistKeysToFileSystem</c>): si el servidor la pierde, lo guardado deja
/// de leerse y <see cref="Descifrar"/> devuelve null; el panel lo dice y basta con volver a escribirla.
/// </summary>
public sealed class ProtectorSecretos(IDataProtectionProvider proveedor) : IProtectorSecretos
{
    public const string Proposito = "PanelControl.Notificaciones.Smtp";

    private readonly IDataProtector _protector = proveedor.CreateProtector(Proposito);

    public string Cifrar(string texto) => _protector.Protect(texto);

    public string? Descifrar(string cifrado)
    {
        try { return _protector.Unprotect(cifrado); }
        catch (CryptographicException) { return null; }
    }
}
```

`src/Infrastructure/Notifications/AjustesNotificacionesProvider.cs`:

```csharp
using Diger.TramitesEstado.Application.Common.Models;
using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Infrastructure.Email;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Infrastructure.Notifications;

/// <summary>
/// Lee los ajustes del Panel de control con una caché de un minuto. Sin fila en la base rige
/// appsettings (<c>Notificaciones:IntervaloHoras</c> y la sección <c>Smtp</c>), así que el día del
/// despliegue nada deja de funcionar aunque nadie haya abierto el panel.
/// </summary>
public sealed class AjustesNotificacionesProvider(
    IApplicationDbContext ctx,
    IMemoryCache cache,
    IProtectorSecretos protector,
    IOptions<NotificacionesOptions> notificaciones,
    IOptions<SmtpSettings> smtp) : IAjustesNotificaciones
{
    internal const string ClaveCache = "PanelControl.Notificaciones.Ajustes";
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(1);

    private sealed record Instantanea(AjustesVigentes Ajustes, SmtpVigente Smtp);

    public async Task<AjustesVigentes> ObtenerAsync(CancellationToken ct = default) => (await CargarAsync(ct)).Ajustes;

    public async Task<SmtpVigente> SmtpAsync(CancellationToken ct = default) => (await CargarAsync(ct)).Smtp;

    public void Invalidar() => cache.Remove(ClaveCache);

    private async Task<Instantanea> CargarAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(ClaveCache, out Instantanea? guardada) && guardada is not null)
            return guardada;

        var fila = await ctx.AjustesNotificaciones.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == AjustesNotificaciones.IdUnico, ct);
        var filas = await ctx.InterruptoresNotificacion.AsNoTracking().ToListAsync(ct);

        var modulos = Enum.GetValues<ModuloNotificacion>().ToDictionary(m => m, m =>
            filas.FirstOrDefault(f => f.Modulo == m) is { } f
                ? new InterruptoresModulo(f.CampanaActiva, f.CorreoActivo && CatalogoNotificaciones.ModuloAdmiteCorreo(m), f.Agrupar)
                : InterruptoresModulo.PorDefecto);

        var intervalo = fila?.IntervaloMinutos
            ?? Math.Clamp(notificaciones.Value.IntervaloHoras * 60, AjustesNotificaciones.MinimoMinutos, AjustesNotificaciones.MaximoMinutos);

        var instantanea = new Instantanea(new AjustesVigentes(intervalo, modulos, fila?.UltimaRevisionUtc), Smtp(fila));
        cache.Set(ClaveCache, instantanea, Vigencia);
        return instantanea;
    }

    private SmtpVigente Smtp(AjustesNotificaciones? fila)
    {
        var s = smtp.Value;
        if (fila is not { SmtpConfigurado: true })
            return new SmtpVigente(s.Host, s.Port, s.EnableSsl, s.Username, s.Password, s.FromEmail, s.FromName,
                s.TimeoutSegundos, DesdePanel: false, HayPassword: !string.IsNullOrEmpty(s.Password), PasswordIlegible: false);

        var password = fila.SmtpPasswordCifrada is { } cifrada ? protector.Descifrar(cifrada) : "";
        return new SmtpVigente(
            fila.SmtpHost ?? "", fila.SmtpPuerto, fila.SmtpSsl, fila.SmtpUsuario ?? "", password ?? "",
            fila.SmtpRemitente ?? "", fila.SmtpNombreRemitente ?? Marca.Nombre, s.TimeoutSegundos,
            DesdePanel: true, HayPassword: fila.SmtpPasswordCifrada is not null, PasswordIlegible: password is null);
    }
}
```

En `src/Infrastructure/DependencyInjection.cs`, junto a `services.AddScoped<INotificacionService, NotificacionService>();`:

```csharp
        services.AddMemoryCache();
        services.AddSingleton<IProtectorSecretos, ProtectorSecretos>();
        services.AddScoped<IAjustesNotificaciones, AjustesNotificacionesProvider>();
        services.AddSingleton<SenalRevision>();
```

Si `NotificacionesOptions` no tiene setter público para `IntervaloHoras` (hoy es `init`), la prueba lo asigna en el inicializador, así que compila igual.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~AjustesNotificacionesProviderTests"`
Expected: PASS (6 pruebas).

- [ ] **Step 5: Commit**

```powershell
git add src/Application/Notificaciones/IAjustesNotificaciones.cs src/Infrastructure/Security/ProtectorSecretos.cs src/Infrastructure/Notifications/AjustesNotificacionesProvider.cs src/Infrastructure/DependencyInjection.cs tests/Application.Tests/Notificaciones/AjustesDePrueba.cs tests/Application.Tests/Notificaciones/AjustesNotificacionesProviderTests.cs
git commit -m "Ajustes de notificaciones desde la base, con la contrasena del correo cifrada"
```

---

### Task 5: La puerta única de los avisos

**Files:**
- Modify: `src/Application/Notificaciones/INotificacionService.cs`
- Modify: `src/Infrastructure/Notifications/NotificacionService.cs`
- Modify: `src/Application/Tickets/Commands/AsignarTicket/AsignarTicketCommand.cs:25-28`
- Modify: `src/Application/Proyectos/EventHandlers/ProyectoEstadoCambiadoEventHandler.cs`
- Modify: `src/Web/Hubs/SoporteHub.cs:152-157`
- Modify: `src/Web/Pages/Shared/_Layout.cshtml` y `src/Web/Pages/Notificaciones/Index.cshtml` (renombrar `FechaCreacion` → `Fecha` del DTO)
- Modify: `tests/Application.Tests/Proyectos/ProyectosTests.cs` (dos `new ProyectoEstadoCambiadoEventHandler(_ctx)`)
- Modify: `tests/Application.Tests/Notificaciones/AjustesDePrueba.cs` (+ `NotificacionesDePrueba`)
- Test: `tests/Application.Tests/Notificaciones/NotificacionServiceTests.cs`

**Interfaces:**
- Consumes: `CatalogoNotificaciones` (T1), las entidades (T2), `IAjustesNotificaciones` (T4).
- Produces:
  - `Task<bool> EncolarAsync(Guid destinatarioId, TipoNotificacion tipo, string titulo, string? url = null, string? clave = null, CancellationToken ct = default)`: true = llegó a la campana; quien llama hace `SaveChanges`.
  - `Task<bool> CrearYGuardarAsync(...)`, con los mismos parámetros, y guarda.
  - `record NotificacionDto(int Id, TipoNotificacion Tipo, string Titulo, string? Url, bool Leida, DateTime Fecha, int Cantidad)`.
  - `NotificacionService(IApplicationDbContext ctx, IUnitOfWork uow, IAjustesNotificaciones ajustes, TimeProvider reloj, IOptions<NotificacionesOptions> opciones)`.
  - En las pruebas: `NotificacionesDePrueba.Servicio(AppDbContext ctx, IAjustesNotificaciones? ajustes = null, TimeProvider? reloj = null, NotificacionesOptions? opciones = null)`.

- [ ] **Step 1: Write the failing tests**

Agregar al final de `tests/Application.Tests/Notificaciones/AjustesDePrueba.cs`:

```csharp
internal static class NotificacionesDePrueba
{
    public static Diger.TramitesEstado.Infrastructure.Notifications.NotificacionService Servicio(
        Diger.TramitesEstado.Infrastructure.Persistence.AppDbContext ctx,
        IAjustesNotificaciones? ajustes = null, TimeProvider? reloj = null, NotificacionesOptions? opciones = null) =>
        new(ctx, ctx, ajustes ?? new AjustesDePrueba(), reloj ?? TimeProvider.System,
            Microsoft.Extensions.Options.Options.Create(opciones ?? new NotificacionesOptions()));
}
```

`tests/Application.Tests/Notificaciones/NotificacionServiceTests.cs`:

```csharp
using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Notificaciones;

public sealed class NotificacionServiceTests : IDisposable
{
    private readonly AppDbContext _ctx = new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());

    private readonly Guid _ana = Guid.NewGuid();
    private readonly AjustesDePrueba _ajustes = new();
    private RelojFijo _reloj = new(RecordatorioBackgroundServiceTests.NueveDeLaManana);

    private Task<bool> AvisarAsync(TipoNotificacion tipo, string url) =>
        NotificacionesDePrueba.Servicio(_ctx, _ajustes, _reloj)
            .CrearYGuardarAsync(_ana, tipo, $"Aviso {url}", url);

    [Fact]
    public async Task Con_la_campana_apagada_no_hay_aviso()
    {
        _ajustes.Con(ModuloNotificacion.Reuniones, campana: false);

        (await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1")).Should().BeFalse();
        (await _ctx.Notificaciones.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Dos_del_mismo_tipo_se_juntan_en_un_aviso()
    {
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1");
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/2");

        var n = await _ctx.Notificaciones.Include(x => x.Elementos).SingleAsync();
        n.Cantidad.Should().Be(2);
        n.Titulo.Should().Be("Tiene 2 compromisos vencidos");
        n.Elementos.Should().HaveCount(2);
    }

    [Fact]
    public async Task Leido_el_aviso_lo_siguiente_abre_uno_nuevo()
    {
        await AvisarAsync(TipoNotificacion.TicketAsignado, "/t/1");
        (await _ctx.Notificaciones.SingleAsync()).MarcarLeida();
        await _ctx.SaveChangesAsync();

        await AvisarAsync(TipoNotificacion.TicketAsignado, "/t/2");

        (await _ctx.Notificaciones.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Con_agrupar_apagado_cada_uno_es_un_aviso()
    {
        _ajustes.Con(ModuloNotificacion.Reuniones, agrupar: false);
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1");
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/2");

        (await _ctx.Notificaciones.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Un_diario_ya_avisado_hoy_no_se_repite_pero_manana_si()
    {
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1");
        (await _ctx.Notificaciones.SingleAsync()).MarcarLeida();
        await _ctx.SaveChangesAsync();

        (await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1")).Should().BeFalse();
        (await _ctx.Notificaciones.CountAsync()).Should().Be(1);

        _reloj = new RelojFijo(RecordatorioBackgroundServiceTests.NueveDeLaManana.AddDays(1));
        (await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1")).Should().BeTrue();
        (await _ctx.Notificaciones.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Dos_avisos_del_mismo_chat_no_se_pierden_ni_cuentan_doble()
    {
        await AvisarAsync(TipoNotificacion.ChatRecibido, "/Chat#4");
        await AvisarAsync(TipoNotificacion.ChatRecibido, "/Chat#4");

        var n = await _ctx.Notificaciones.SingleAsync();
        n.Cantidad.Should().Be(1);
    }

    [Fact]
    public async Task Con_el_correo_encendido_queda_en_la_bandeja()
    {
        _ajustes.Con(ModuloNotificacion.Reuniones, correo: true);
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1");

        (await _ctx.CorreosPendientes.SingleAsync()).Estado.Should().Be(EstadoCorreoPendiente.Pendiente);
    }

    [Fact]
    public async Task Con_el_correo_apagado_no_hay_bandeja()
    {
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1");
        (await _ctx.CorreosPendientes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_recordatorio_manual_no_pasa_por_la_bandeja()
    {
        _ajustes.Con(ModuloNotificacion.Tickets, correo: true);
        await AvisarAsync(TipoNotificacion.RecordatorioManualTicket, "/t/1");
        (await _ctx.CorreosPendientes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Solo_correo_sin_campana_igual_no_repite_en_el_dia()
    {
        _ajustes.Con(ModuloNotificacion.Reuniones, campana: false, correo: true);
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1");
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1");

        (await _ctx.CorreosPendientes.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task La_campana_lista_lo_ultimo_primero_y_el_grupo_lleva_a_su_detalle()
    {
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1");
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/2");

        var lista = await NotificacionesDePrueba.Servicio(_ctx, _ajustes, _reloj).GetNoLeidasAsync(_ana);
        lista.Should().ContainSingle();
        lista[0].Url.Should().Be($"/Notificaciones/{lista[0].Id}");
        lista[0].Cantidad.Should().Be(2);
    }

    public void Dispose() => _ctx.Dispose();
}
```

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~NotificacionServiceTests"`
Expected: falla la compilación: el constructor de `NotificacionService` y `CrearYGuardarAsync(..., url)` no aceptan esos argumentos.

- [ ] **Step 2: Nuevo contrato**

`src/Application/Notificaciones/INotificacionService.cs` completo:

```csharp
namespace Diger.TramitesEstado.Application.Notificaciones;

/// <param name="Url">Adónde lleva: al elemento si es uno solo; a <c>/Notificaciones/{id}</c> si son varios.</param>
/// <param name="Fecha">Cuándo entró el último elemento.</param>
public record NotificacionDto(
    int              Id,
    TipoNotificacion Tipo,
    string           Titulo,
    string?          Url,
    bool             Leida,
    DateTime         Fecha,
    int              Cantidad);

/// <summary>
/// La única puerta de los avisos. Aplica los interruptores del Panel de control (campana, correo,
/// agrupar), evita repetir los avisos diarios y anota en la bandeja lo que tiene que salir por correo.
/// </summary>
public interface INotificacionService
{
    /// <summary>Anota el aviso en el contexto EF; quien llama hace SaveChanges con su operación.</summary>
    /// <param name="clave">Qué identifica al elemento. Por defecto, la URL.</param>
    /// <returns>Si el aviso llegó a la campana (false si el módulo la tiene apagada o ya se avisó hoy).</returns>
    Task<bool> EncolarAsync(Guid destinatarioId, TipoNotificacion tipo, string titulo,
                            string? url = null, string? clave = null, CancellationToken ct = default);

    /// <summary>Igual que <see cref="EncolarAsync"/>, y guarda. Para el revisor y el chat, que no tienen operación propia.</summary>
    Task<bool> CrearYGuardarAsync(Guid destinatarioId, TipoNotificacion tipo, string titulo,
                                  string? url = null, string? clave = null, CancellationToken ct = default);

    Task<IReadOnlyList<NotificacionDto>> GetNoLeidasAsync(Guid usuarioId, int max = 8, CancellationToken ct = default);
    Task<int>                            ConteoNoLeidasAsync(Guid usuarioId, CancellationToken ct = default);
    Task                                 MarcarLeidaAsync(int id, Guid usuarioId, CancellationToken ct = default);
    Task                                 MarcarTodasLeidasAsync(Guid usuarioId, CancellationToken ct = default);
}
```

- [ ] **Step 3: Implementación**

`src/Infrastructure/Notifications/NotificacionService.cs` completo:

```csharp
using Diger.TramitesEstado.Application.Notificaciones;
using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Infrastructure.Notifications;

public sealed class NotificacionService(
    IApplicationDbContext ctx,
    IUnitOfWork uow,
    IAjustesNotificaciones ajustes,
    TimeProvider reloj,
    IOptions<NotificacionesOptions> opciones) : INotificacionService
{
    private readonly TimeZoneInfo _zona = CalendarioLocal.Resolver(opciones.Value.ZonaHoraria).Zona;

    public async Task<bool> EncolarAsync(Guid destinatarioId, TipoNotificacion tipo, string titulo,
                                         string? url = null, string? clave = null, CancellationToken ct = default)
    {
        var def     = CatalogoNotificaciones.De(tipo);
        var inter   = (await ajustes.ObtenerAsync(ct)).De(def.Modulo);
        var ahora   = reloj.GetUtcNow().UtcDateTime;
        var claveEf = Notificacion.Recortar(clave ?? url ?? titulo, NotificacionElemento.LargoClave);

        // Solo los del revisor diario: el resto (un chat, un ticket asignado) puede volver a pasar
        // el mismo día y tiene que llegar.
        if (def.Diario && await YaAvisadoHoyAsync(destinatarioId, tipo, claveEf, ct))
            return false;

        var enCampana = false;
        if (inter.Campana)
        {
            await AnotarEnCampanaAsync(destinatarioId, tipo, titulo, url, claveEf, def, inter.Agrupar, ahora, ct);
            enCampana = true;
        }

        if (inter.Correo && def.AdmiteCorreo && !def.CorreoInmediato)
            ctx.CorreosPendientes.Add(CorreoPendiente.Crear(destinatarioId, def.Modulo, tipo, titulo, url, claveEf, ahora));

        return enCampana;
    }

    public async Task<bool> CrearYGuardarAsync(Guid destinatarioId, TipoNotificacion tipo, string titulo,
                                               string? url = null, string? clave = null, CancellationToken ct = default)
    {
        var llego = await EncolarAsync(destinatarioId, tipo, titulo, url, clave, ct);
        await uow.SaveChangesAsync(ct);
        return llego;
    }

    private async Task AnotarEnCampanaAsync(Guid destinatarioId, TipoNotificacion tipo, string titulo, string? url,
                                            string clave, DefinicionNotificacion def, bool agrupar, DateTime ahora,
                                            CancellationToken ct)
    {
        if (agrupar)
        {
            // Primero lo que todavía no se guardó: dos avisos del mismo tipo en una sola operación
            // tienen que caer en el mismo grupo.
            var abierta = ctx.Notificaciones.Local
                              .FirstOrDefault(n => n.DestinatarioId == destinatarioId && n.Tipo == tipo && !n.Leida)
                          ?? await ctx.Notificaciones
                              .Include(n => n.Elementos)
                              .Where(n => n.DestinatarioId == destinatarioId && n.Tipo == tipo && !n.Leida)
                              .OrderByDescending(n => n.FechaActualizacion)
                              .FirstOrDefaultAsync(ct);

            if (abierta is not null)
            {
                abierta.Sumar(clave, titulo, url, def.Plural, ahora);
                return;
            }
        }

        ctx.Notificaciones.Add(Notificacion.Crear(destinatarioId, tipo, titulo, url, clave, ahora));
    }

    private async Task<bool> YaAvisadoHoyAsync(Guid destinatarioId, TipoNotificacion tipo, string clave, CancellationToken ct)
    {
        var inicio = CalendarioLocal.Dia(reloj.GetUtcNow(), _zona).InicioDeHoyUtc;

        var enCampana = await ctx.NotificacionElementos.AnyAsync(e =>
            e.Clave == clave && e.FechaUtc >= inicio &&
            ctx.Notificaciones.Any(n => n.Id == e.NotificacionId && n.DestinatarioId == destinatarioId && n.Tipo == tipo), ct);
        if (enCampana) return true;

        // Con la campana apagada y el correo encendido no hay elemento: lo dice la bandeja.
        return await ctx.CorreosPendientes.AnyAsync(c =>
            c.DestinatarioId == destinatarioId && c.Tipo == tipo && c.Clave == clave && c.CreadoUtc >= inicio, ct);
    }

    public async Task<IReadOnlyList<NotificacionDto>> GetNoLeidasAsync(Guid usuarioId, int max = 8, CancellationToken ct = default)
        => await ctx.Notificaciones
            .AsNoTracking()
            .Where(n => n.DestinatarioId == usuarioId && !n.Leida)
            .OrderByDescending(n => n.FechaActualizacion)
            .Take(max)
            .Select(n => new NotificacionDto(n.Id, n.Tipo, n.Titulo,
                n.Cantidad > 1 ? "/Notificaciones/" + n.Id : n.Url,
                n.Leida, n.FechaActualizacion, n.Cantidad))
            .ToListAsync(ct);

    public async Task<int> ConteoNoLeidasAsync(Guid usuarioId, CancellationToken ct = default)
        => await ctx.Notificaciones.CountAsync(n => n.DestinatarioId == usuarioId && !n.Leida, ct);

    public async Task MarcarLeidaAsync(int id, Guid usuarioId, CancellationToken ct = default)
    {
        var n = await ctx.Notificaciones.FirstOrDefaultAsync(x => x.Id == id && x.DestinatarioId == usuarioId, ct);
        if (n is null) return;
        n.MarcarLeida();
        await uow.SaveChangesAsync(ct);
    }

    public async Task MarcarTodasLeidasAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var pendientes = await ctx.Notificaciones
            .Where(x => x.DestinatarioId == usuarioId && !x.Leida)
            .ToListAsync(ct);
        foreach (var n in pendientes) n.MarcarLeida();
        if (pendientes.Count > 0) await uow.SaveChangesAsync(ct);
    }
}
```

Llamadores:

`AsignarTicketCommand.cs`, sustituir el `notifSvc.Encolar(...)` por:

```csharp
        if (cmd.UsuarioId.HasValue)
            await notifSvc.EncolarAsync(cmd.UsuarioId.Value, TipoNotificacion.TicketAsignado,
                $"Ticket asignado: [{t.Numero}] {t.Titulo}",
                url: $"/Tickets/Detalle/{t.Id}", ct: ct);
```

`ProyectoEstadoCambiadoEventHandler.cs`, constructor y aviso:

```csharp
public sealed class ProyectoEstadoCambiadoEventHandler(IApplicationDbContext ctx, INotificacionService notifSvc)
    : INotificationHandler<ProyectoEstadoCambiadoEvent>
```

```csharp
            await notifSvc.EncolarAsync(
                destinatario,
                TipoNotificacion.ProyectoCambioEstado,
                $"{e.Codigo} pasó a «{e.EstadoNuevo}»",
                $"/Proyectos/Editor/{e.ProyectoId}", ct: ct);
```

Agregar `using Diger.TramitesEstado.Application.Notificaciones;` si el archivo no lo tiene.

`SoporteHub.cs`, en `NotificarTecnicoAsync`, el último argumento pasa a `ct: ct`:

```csharp
        await notifSvc.CrearYGuardarAsync(
            tecnicoId,
            TipoNotificacion.ChatRecibido,
            $"Nuevo chat de {usuarioNombre}" + (temaNombre != null ? $" · {temaNombre}" : ""),
            $"/Chat#{sesionId}",
            ct: ct);
```

`ProyectosTests.cs`: en las dos líneas, `new ProyectoEstadoCambiadoEventHandler(_ctx)` pasa a `new ProyectoEstadoCambiadoEventHandler(_ctx, Notificaciones.NotificacionesDePrueba.Servicio(_ctx))`. Si el `using` del namespace `...Tests.Notificaciones` no está, agregarlo.

Vistas: `grep -n "FechaCreacion" src/Web/Pages/Shared/_Layout.cshtml src/Web/Pages/Notificaciones/Index.cshtml` y cambiar cada `n.FechaCreacion` / `_n.FechaCreacion` (los del DTO) por `.Fecha`.

En `RecordatorioBackgroundService.cs` y `EnviarRecordatorioManualCommands.cs` el compilador marcará los llamados viejos. Esos se arreglan en las Tasks 7 y 8. Para compilar ahora:
- en `EnviarRecordatorioManualCommands.cs`, cada `notifSvc.Encolar(a, b, c, d);` pasa a `await notifSvc.EncolarAsync(a, b, c, d, ct: ct);`;
- en `RecordatorioBackgroundService.cs`, cada `notifSvc.CrearYGuardarAsync(uid, X, tit, url, ct)` pasa a `notifSvc.CrearYGuardarAsync(uid, X, tit, url, ct: ct)`;
- en `RecordatorioBackgroundServiceTests.cs`, `new NotificacionService(_ctx, _ctx)` pasa a `NotificacionesDePrueba.Servicio(_ctx, reloj: <el RelojFijo que construye Servicio()>)`.

- [ ] **Step 4: Run tests**

Run: `dotnet build Diger.TramitesEstado.sln` y luego `dotnet test tests\Application.Tests`
Expected: compila; `NotificacionServiceTests` PASS (11). Las pruebas de recordatorios que usan `Substitute.For<INotificacionService>()` ahora reciben `false` de `EncolarAsync` y pueden fallar en `AvisosCampana`. Eso se corrige en la Task 8, Step 1: anotar cuáles fallan y seguir.

- [ ] **Step 5: Commit**

```powershell
git add src tests
git commit -m "Una sola puerta para los avisos: interruptores por modulo, agrupacion y bandeja de correo"
```

---

### Task 6: Despachador de correos y SMTP desde el panel

**Files:**
- Modify: `src/Application/Common/Interfaces/IEmailService.cs`
- Create: `src/Application/Notificaciones/IDespachadorCorreos.cs`
- Create: `src/Infrastructure/Notifications/DespachadorCorreos.cs`
- Modify: `src/Infrastructure/Email/SmtpEmailService.cs`
- Modify: `src/Infrastructure/DependencyInjection.cs:100-101`
- Modify: `src/Web/Common/AvisosDeConfiguracion.cs`
- Modify: `tests/Application.Tests/Notificaciones/CorreoSeguridadTests.cs` (dos `new SmtpEmailService(...)`)
- Test: `tests/Application.Tests/Notificaciones/DespachadorCorreosTests.cs`

**Interfaces:**
- Consumes: `CorreoPendiente` (T2), `IAjustesNotificaciones`/`SmtpVigente` (T4), `CatalogoNotificaciones` (T1).
- Produces:
  - `TipoCorreo.ResumenAvisos` y `TipoCorreo.Prueba`.
  - `record ResultadoPruebaCorreo(ResultadoCorreo Resultado, string Detalle)` e `interface IProbadorCorreo { Task<ResultadoPruebaCorreo> ProbarAsync(string destino, CancellationToken ct = default); }`.
  - `interface IDespachadorCorreos { Task<int> DespacharAsync(CancellationToken ct = default); }` y `DespachadorCorreos.MaxIntentos = 3`.
  - `SmtpEmailService(IAjustesNotificaciones ajustes, ILogger<SmtpEmailService> logger) : IEmailService, IProbadorCorreo`.

- [ ] **Step 1: Write the failing test**

`tests/Application.Tests/Notificaciones/DespachadorCorreosTests.cs`:

```csharp
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Common.Models;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Notifications;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Notificaciones;

public sealed class DespachadorCorreosTests : IDisposable
{
    private readonly AppDbContext _ctx = new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());

    private readonly IEmailService _email = Substitute.For<IEmailService>();
    private readonly Usuario _ana = Usuario.Crear("Ana", "ana@prueba.local", "hash");

    public DespachadorCorreosTests()
    {
        _ctx.Usuarios.Add(_ana);
        _ctx.SaveChanges();
    }

    private DespachadorCorreos Despachador() => new(_ctx, _email, Options.Create(new CorreoOptions()),
        TimeProvider.System, NullLogger<DespachadorCorreos>.Instance);

    private void Pendiente(Guid para, string url) => _ctx.CorreosPendientes.Add(CorreoPendiente.Crear(
        para, ModuloNotificacion.Reuniones, TipoNotificacion.CompromisoVencido, $"Compromiso {url}", url, url, DateTime.UtcNow));

    [Fact]
    public async Task Tres_pendientes_de_una_persona_salen_en_un_correo()
    {
        Pendiente(_ana.Id, "/c/1"); Pendiente(_ana.Id, "/c/2"); Pendiente(_ana.Id, "/c/3");
        await _ctx.SaveChangesAsync();
        _email.SendEmailAsync(default!, default!, default!, default, default).ReturnsForAnyArgs(ResultadoCorreo.Enviado);

        (await Despachador().DespacharAsync()).Should().Be(1);

        await _email.Received(1).SendEmailAsync("ana@prueba.local", Arg.Any<string>(),
            Arg.Is<string>(b => b.Contains("Compromiso /c/1") && b.Contains("Compromiso /c/3")),
            TipoCorreo.ResumenAvisos, Arg.Any<CancellationToken>());
        (await _ctx.CorreosPendientes.AllAsync(c => c.Estado == EstadoCorreoPendiente.Enviado)).Should().BeTrue();
    }

    [Fact]
    public async Task Al_tercer_fallo_queda_fallido()
    {
        Pendiente(_ana.Id, "/c/1");
        await _ctx.SaveChangesAsync();
        _email.SendEmailAsync(default!, default!, default!, default, default).ReturnsForAnyArgs(ResultadoCorreo.Fallido);

        for (var i = 0; i < 3; i++) await Despachador().DespacharAsync();

        var c = await _ctx.CorreosPendientes.SingleAsync();
        c.Estado.Should().Be(EstadoCorreoPendiente.Fallido);
        c.Intentos.Should().Be(DespachadorCorreos.MaxIntentos);
    }

    [Fact]
    public async Task Sin_usuario_activo_se_descarta_sin_intentar()
    {
        Pendiente(Guid.NewGuid(), "/c/1");
        await _ctx.SaveChangesAsync();

        await Despachador().DespacharAsync();

        await _email.DidNotReceiveWithAnyArgs().SendEmailAsync(default!, default!, default!, default, default);
        (await _ctx.CorreosPendientes.SingleAsync()).Estado.Should().Be(EstadoCorreoPendiente.Fallido);
    }

    [Fact]
    public async Task El_cuerpo_escapa_lo_que_viene_de_las_personas()
    {
        _ctx.CorreosPendientes.Add(CorreoPendiente.Crear(_ana.Id, ModuloNotificacion.Reuniones,
            TipoNotificacion.CompromisoVencido, "<b>x</b>", "/c/1", "/c/1", DateTime.UtcNow));
        await _ctx.SaveChangesAsync();
        _email.SendEmailAsync(default!, default!, default!, default, default).ReturnsForAnyArgs(ResultadoCorreo.Enviado);

        await Despachador().DespacharAsync();

        await _email.Received(1).SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Is<string>(b => b.Contains("&lt;b&gt;x&lt;/b&gt;") && !b.Contains("<b>x</b>")),
            Arg.Any<TipoCorreo>(), Arg.Any<CancellationToken>());
    }

    public void Dispose() => _ctx.Dispose();
}
```

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~DespachadorCorreosTests"`
Expected: falla la compilación: no existen `DespachadorCorreos` ni `TipoCorreo.ResumenAvisos`.

- [ ] **Step 2: Contratos**

En `IEmailService.cs`, agregar a `enum TipoCorreo` (al final):

```csharp
    /// <summary>Resumen de los avisos de una persona, desde la bandeja de salida.</summary>
    ResumenAvisos,
    /// <summary>El correo de prueba del Panel de control.</summary>
    Prueba,
```

y al final del archivo:

```csharp
/// <summary>Lo que pasó con el correo de prueba, con un motivo que se le puede mostrar al administrador.</summary>
public sealed record ResultadoPruebaCorreo(ResultadoCorreo Resultado, string Detalle);

/// <summary>
/// El correo de prueba del Panel de control. Está aparte de <see cref="IEmailService"/> porque las
/// pruebas Web reemplazan el servicio de correo por uno que solo captura, y la prueba tiene que
/// hablar con el servidor de verdad.
/// </summary>
public interface IProbadorCorreo
{
    Task<ResultadoPruebaCorreo> ProbarAsync(string destino, CancellationToken ct = default);
}
```

`src/Application/Notificaciones/IDespachadorCorreos.cs`:

```csharp
namespace Diger.TramitesEstado.Application.Notificaciones;

/// <summary>Manda lo que espera en la bandeja de salida: un correo por persona.</summary>
public interface IDespachadorCorreos
{
    /// <returns>Cuántos correos salieron.</returns>
    Task<int> DespacharAsync(CancellationToken ct = default);
}
```

- [ ] **Step 3: Implementación**

`src/Infrastructure/Notifications/DespachadorCorreos.cs`:

```csharp
using System.Text;
using Diger.TramitesEstado.Application.Common.Models;
using Diger.TramitesEstado.Application.Notificaciones;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Diger.TramitesEstado.Application.Notificaciones.PlantillasCorreo;

namespace Diger.TramitesEstado.Infrastructure.Notifications;

/// <summary>
/// Junta lo pendiente de cada persona en un solo correo, ordenado por tipo. Lo llama el revisor al
/// final de cada vuelta. Un envío que falla se reintenta en las vueltas siguientes hasta
/// <see cref="MaxIntentos"/>; después queda fallido y el panel lo cuenta.
/// </summary>
public sealed class DespachadorCorreos(
    IApplicationDbContext ctx,
    IEmailService email,
    IOptions<CorreoOptions> correo,
    TimeProvider reloj,
    ILogger<DespachadorCorreos> logger) : IDespachadorCorreos
{
    public const int MaxIntentos = 3;

    public async Task<int> DespacharAsync(CancellationToken ct = default)
    {
        var pendientes = await ctx.CorreosPendientes
            .Where(c => c.Estado == EstadoCorreoPendiente.Pendiente)
            .OrderBy(c => c.CreadoUtc)
            .ToListAsync(ct);
        if (pendientes.Count == 0) return 0;

        var ids = pendientes.Select(p => p.DestinatarioId).Distinct().ToList();
        var usuarios = await ctx.Usuarios.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Nombre, u.Correo, u.Activo })
            .ToDictionaryAsync(u => u.Id, ct);

        var ahora    = reloj.GetUtcNow().UtcDateTime;
        var enviados = 0;

        foreach (var grupo in pendientes.GroupBy(p => p.DestinatarioId))
        {
            if (!usuarios.TryGetValue(grupo.Key, out var u) || !u.Activo || string.IsNullOrWhiteSpace(u.Correo))
            {
                foreach (var p in grupo) p.Descartar("El destinatario no está activo o no tiene correo registrado.");
                continue;
            }

            var items = grupo.ToList();
            var (asunto, cuerpo) = Armar(u.Nombre, items);
            var resultado = await email.SendEmailAsync(u.Correo, asunto, cuerpo, TipoCorreo.ResumenAvisos, ct);

            foreach (var p in items)
            {
                if (resultado == ResultadoCorreo.Enviado) p.MarcarEnviado(ahora);
                else p.RegistrarFallo(resultado == ResultadoCorreo.SinConfigurar
                    ? "El servidor de correo no está configurado."
                    : "El servidor de correo rechazó el envío o no respondió.", MaxIntentos);
            }

            if (resultado == ResultadoCorreo.Enviado) enviados++;
        }

        await ctx.SaveChangesAsync(ct);
        if (enviados > 0) logger.LogInformation("Bandeja de correo: {Enviados} resúmenes enviados.", enviados);
        return enviados;
    }

    private (string Asunto, string Cuerpo) Armar(string nombre, IReadOnlyList<CorreoPendiente> items)
    {
        var sb = new StringBuilder()
            .Append($"<p>Estimado(a) <strong>{E(nombre)}</strong>,</p>")
            .Append($"<p>Tiene {items.Count} {(items.Count == 1 ? "aviso nuevo" : "avisos nuevos")} en {E(Marca.Nombre)}:</p>");

        foreach (var tipo in items.GroupBy(i => i.Tipo))
        {
            sb.Append($"<h3>{E(CatalogoNotificaciones.De(tipo.Key).Etiqueta)}</h3><ul>");
            foreach (var i in tipo)
                sb.Append(i.Url is { } url
                    ? $"<li><a href='{E(correo.Value.Enlace(url))}'>{E(i.Titulo)}</a></li>"
                    : $"<li>{E(i.Titulo)}</li>");
            sb.Append("</ul>");
        }

        var asunto = items.Count == 1 ? items[0].Titulo : $"{items.Count} avisos nuevos — {Marca.Nombre}";
        return (asunto, sb.ToString());
    }
}
```

`src/Infrastructure/Email/SmtpEmailService.cs` completo. Conservar el comentario de cabecera actual y agregarle el párrafo indicado:

```csharp
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Common.Models;
using Diger.TramitesEstado.Application.Notificaciones;
using Microsoft.Extensions.Logging;

namespace Diger.TramitesEstado.Infrastructure.Email;

/// <summary>
/// (… cabecera actual sin cambios …)
///
/// <para>La configuración se pide en cada envío a <see cref="IAjustesNotificaciones"/>: la que se
/// guardó en Panel de control → Notificaciones o, si nunca se guardó, la sección <c>Smtp</c>.
/// Cambiar la contraseña en el panel aplica al siguiente correo, sin reiniciar.</para>
/// </summary>
public sealed class SmtpEmailService(
    IAjustesNotificaciones ajustes,
    ILogger<SmtpEmailService> logger) : IEmailService, IProbadorCorreo
{
    public async Task<ResultadoCorreo> SendEmailAsync(string to, string subject, string bodyHtml, TipoCorreo tipo, CancellationToken ct = default)
        => (await EnviarAsync(to, subject, bodyHtml, tipo, ct)).Resultado;

    public Task<ResultadoPruebaCorreo> ProbarAsync(string destino, CancellationToken ct = default) =>
        EnviarAsync(destino, $"Correo de prueba — {Marca.Nombre}",
            $"<p>Este es un correo de prueba enviado desde el Panel de control de {PlantillasCorreo.E(Marca.Nombre)}.</p>" +
            "<p>Si lo está leyendo, el correo saliente funciona.</p>",
            TipoCorreo.Prueba, ct);

    private async Task<ResultadoPruebaCorreo> EnviarAsync(string to, string subject, string bodyHtml, TipoCorreo tipo, CancellationToken ct)
    {
        var envio = Guid.NewGuid().ToString("N")[..8];
        var s     = await ajustes.SmtpAsync(ct);

        if (s.PasswordIlegible)
        {
            logger.LogWarning(
                "Correo {Tipo} (envío {Envio}) no enviado: la contraseña guardada en el Panel de control no se puede descifrar en este servidor.",
                tipo, envio);
            return new(ResultadoCorreo.SinConfigurar,
                "La contraseña guardada no se puede leer en este servidor: vuelva a escribirla en el Panel de control.");
        }

        if (!s.EstaConfigurado)
        {
            logger.LogWarning(
                "Correo {Tipo} (envío {Envio}) no enviado: el servidor de correo no está configurado (Panel de control o sección Smtp).",
                tipo, envio);
            return new(ResultadoCorreo.SinConfigurar, "El servidor de correo no está configurado.");
        }

        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(s.Remitente, s.NombreRemitente);
            message.To.Add(new MailAddress(to));
            message.Subject = subject;
            message.Body = bodyHtml;
            message.IsBodyHtml = true;

            var limite = TimeSpan.FromSeconds(Math.Max(1, s.TimeoutSegundos));
            using var client = new SmtpClient(s.Host, s.Puerto)
            {
                EnableSsl   = s.Ssl,
                Credentials = new NetworkCredential(s.Usuario, s.Password),
                Timeout     = (int)limite.TotalMilliseconds,
            };

            // SmtpClient.Timeout solo rige el envío sincrónico; el asincrónico se corta con el token.
            using var tiempo = CancellationTokenSource.CreateLinkedTokenSource(ct);
            tiempo.CancelAfter(limite);

            await client.SendMailAsync(message, tiempo.Token);
            logger.LogInformation("Correo {Tipo} (envío {Envio}) enviado.", tipo, envio);
            return new(ResultadoCorreo.Enviado, "El servidor aceptó el correo.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogError(
                "Correo {Tipo} (envío {Envio}) no enviado: el servidor de correo no respondió en {Segundos} s.",
                tipo, envio, s.TimeoutSegundos);
            return new(ResultadoCorreo.Fallido, $"El servidor de correo no respondió en {s.TimeoutSegundos} segundos.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // La excepción describe la falla de conexión o la respuesta del servidor; no lleva el cuerpo.
            logger.LogError(ex, "Correo {Tipo} (envío {Envio}) no enviado.", tipo, envio);
            return new(ResultadoCorreo.Fallido, Explicar(ex));
        }
    }

    /// <summary>Un motivo que el administrador entienda. Va antes el «5.7»: los servidores lo usan
    /// para rechazos de autenticación aunque el código de estado diga otra cosa.</summary>
    internal static string Explicar(Exception ex) => ex switch
    {
        SmtpException se when se.Message.Contains("5.7", StringComparison.Ordinal)
            => "El servidor rechazó el usuario o la contraseña.",
        SmtpException { StatusCode: SmtpStatusCode.MustIssueStartTlsFirst }
            => "El servidor exige conexión segura: active SSL.",
        SmtpException { InnerException: SocketException } or SocketException
            => "No se pudo conectar con el servidor de correo. Revise el servidor y el puerto.",
        SmtpException se
            => $"El servidor de correo respondió: {se.StatusCode}.",
        _   => "No se pudo enviar el correo.",
    };
}
```

En `DependencyInjection.cs`, sustituir `services.AddScoped<IEmailService, SmtpEmailService>();` por:

```csharp
        // Una sola instancia por petición para las dos interfaces. Las pruebas Web reemplazan
        // IEmailService; IProbadorCorreo sigue hablando con el servidor de verdad.
        services.AddScoped<SmtpEmailService>();
        services.AddScoped<IEmailService>(sp => sp.GetRequiredService<SmtpEmailService>());
        services.AddScoped<IProbadorCorreo>(sp => sp.GetRequiredService<SmtpEmailService>());
        services.AddScoped<IDespachadorCorreos, DespachadorCorreos>();
```

`services.Configure<SmtpSettings>(...)` **se queda**: la usa el proveedor como respaldo.

`AvisosDeConfiguracion.cs`, el texto del aviso de SMTP pasa a:

```csharp
            logger.LogWarning(
                "La sección Smtp no está configurada. Si el correo saliente se configuró en Panel de control → " +
                "Notificaciones, ignore este aviso; si no, no saldrá ningún correo (recuperación de contraseña " +
                "ni recordatorios) y los avisos de la campana siguen funcionando.");
```

Correr `dotnet test tests\Web.Tests --filter "FullyQualifiedName~AvisosDeConfiguracionTests"`. Si alguna prueba fija el texto viejo, actualizarla al nuevo.

`CorreoSeguridadTests.cs`: cada `new SmtpEmailService(Options.Create(new SmtpSettings { Host = H, Username = U, ... }), registro)` pasa a `new SmtpEmailService(new AjustesDePrueba { Smtp = AjustesDePrueba.ConSmtp(H, U) }, registro)`, con los mismos `H` y `U` que tenía cada prueba.

- [ ] **Step 4: Run tests**

Run: `dotnet build Diger.TramitesEstado.sln`, luego `dotnet test tests\Application.Tests --filter "FullyQualifiedName~DespachadorCorreosTests|FullyQualifiedName~CorreoSeguridadTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src tests
git commit -m "Bandeja de salida: un correo por persona; el SMTP se lee del Panel de control"
```

---

### Task 7: El revisor con intervalo vivo

**Files:**
- Modify: `src/Infrastructure/Notifications/RecordatorioBackgroundService.cs`
- Modify: `tests/Application.Tests/Notificaciones/RecordatorioBackgroundServiceTests.cs` (método `Servicio`)
- Test: el mismo archivo (+2 pruebas)

**Interfaces:**
- Consumes: `SenalRevision`, `IAjustesNotificaciones` (T4); `IDespachadorCorreos` (T6); `INotificacionService` (T5).
- Produces: `RecordatorioBackgroundService(IServiceScopeFactory, IOptions<NotificacionesOptions>, TimeProvider, SenalRevision, ILogger<RecordatorioBackgroundService>)`.

- [ ] **Step 1: Ajustar el armado de las pruebas y escribir las nuevas**

En `RecordatorioBackgroundServiceTests`, agregar los campos y sustituir el método `Servicio`:

```csharp
    private readonly AjustesDePrueba _ajustes = new();
    private readonly IDespachadorCorreos _despachador = Substitute.For<IDespachadorCorreos>();

    private RecordatorioBackgroundService Servicio(DateTimeOffset ahora, NotificacionesOptions? opciones = null)
    {
        var reloj = new RelojFijo(ahora);
        var opts  = opciones ?? new NotificacionesOptions();
        var services = new ServiceCollection();
        services.AddSingleton<IApplicationDbContext>(_ctx);
        services.AddSingleton<IAjustesNotificaciones>(_ajustes);
        services.AddSingleton<INotificacionService>(NotificacionesDePrueba.Servicio(_ctx, _ajustes, reloj, opts));
        services.AddSingleton(_despachador);
        var proveedor = services.BuildServiceProvider();

        return new RecordatorioBackgroundService(
            proveedor.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(opts), reloj, new SenalRevision(),
            NullLogger<RecordatorioBackgroundService>.Instance);
    }
```

Pruebas nuevas en la misma clase:

```csharp
    [Fact]
    public async Task Cada_revision_queda_anotada_y_despacha_el_correo()
    {
        await Servicio(NueveDeLaManana).ProcesarAsync(CancellationToken.None);

        (await _ctx.AjustesNotificaciones.SingleAsync()).UltimaRevisionUtc.Should().Be(NueveDeLaManana.UtcDateTime);
        await _despachador.Received(1).DespacharAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_primera_revision_crea_la_fila_con_el_intervalo_vigente()
    {
        _ajustes.IntervaloMinutos = 30;

        await Servicio(NueveDeLaManana).ProcesarAsync(CancellationToken.None);

        (await _ctx.AjustesNotificaciones.SingleAsync()).IntervaloMinutos.Should().Be(30);
    }
```

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~RecordatorioBackgroundServiceTests"`
Expected: falla la compilación: el constructor no recibe `SenalRevision`.

- [ ] **Step 2: Implementación**

En `RecordatorioBackgroundService.cs`:

1. Constructor:

```csharp
public sealed class RecordatorioBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificacionesOptions> options,
    TimeProvider reloj,
    SenalRevision senal,
    ILogger<RecordatorioBackgroundService> logger) : BackgroundService
```

2. `EjecutarBucleAsync` completo:

```csharp
    private async Task EjecutarBucleAsync(CancellationToken ct)
    {
        // Esperar al arranque para no competir con las migraciones EF del startup. Guardar el panel
        // durante esa espera la corta, igual que la de cada vuelta.
        await senal.EsperarAsync(TimeSpan.FromMinutes(options.Value.DelayInicialMinutos), ct);

        while (!ct.IsCancellationRequested)
        {
            try { await ProcesarAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error en RecordatorioBackgroundService");
            }

            // El intervalo se lee en cada vuelta: lo cambia el Panel de control sin reiniciar.
            await senal.EsperarAsync(TimeSpan.FromMinutes(await IntervaloAsync(ct)), ct);
        }
    }

    private async Task<int> IntervaloAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            return (await scope.ServiceProvider.GetRequiredService<IAjustesNotificaciones>().ObtenerAsync(ct)).IntervaloMinutos;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "No se pudo leer el intervalo de revisión; se usa el de por defecto.");
            return AjustesNotificaciones.IntervaloPorDefecto;
        }
    }
```

3. En `ProcesarAsync`:
   - Borrar el bloque `// ── Pre-carga de notificaciones ya enviadas hoy (evita duplicados) ──`, desde `var enviadas = ...` hasta el cierre de `bool YaEnviada(...)`. Ese control ahora lo hace `NotificacionService` (Task 5).
   - En cada sitio que hoy dice `if (!YaEnviada(uid, TipoNotificacion.X, url)) await notifSvc.CrearYGuardarAsync(uid, TipoNotificacion.X, tit, url, ct: ct);`, dejar solo `await notifSvc.CrearYGuardarAsync(uid, TipoNotificacion.X, tit, url, ct: ct);`. Ubicarlos con `grep -n "YaEnviada" src/Infrastructure/Notifications/RecordatorioBackgroundService.cs`, que al terminar no debe encontrar nada.
   - Al final de `ProcesarAsync`, antes de la llave de cierre:

```csharp
        // ── Última revisión (la muestra el panel) y correo de la bandeja ──────
        var ajustes = scope.ServiceProvider.GetRequiredService<IAjustesNotificaciones>();
        var fila = await ctx.AjustesNotificaciones.FirstOrDefaultAsync(a => a.Id == AjustesNotificaciones.IdUnico, ct);
        if (fila is null)
        {
            // Sin fila nace con el intervalo vigente (el de appsettings), no con el de por defecto:
            // si no, la primera revisión cambiaría en silencio un intervalo que nadie tocó.
            fila = AjustesNotificaciones.CrearPorDefecto((await ajustes.ObtenerAsync(ct)).IntervaloMinutos);
            ctx.AjustesNotificaciones.Add(fila);
        }
        fila.MarcarRevision(reloj.GetUtcNow().UtcDateTime);
        await ctx.SaveChangesAsync(ct);
        ajustes.Invalidar();

        await scope.ServiceProvider.GetRequiredService<IDespachadorCorreos>().DespacharAsync(ct);
```

- [ ] **Step 3: Run tests**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~RecordatorioBackgroundServiceTests|FullyQualifiedName~Notificaciones"`
Expected: PASS, incluidas las pruebas de «no duplica en el mismo día» que ya existían: ahora las cubre el control por elementos.

Si alguna prueba que ya existía cuenta **un aviso por elemento** para la misma persona (por ejemplo, «dos compromisos vencidos → dos notificaciones»), ahora ve **uno agrupado**. Eso es el comportamiento nuevo, no un error. Para que conserve lo que comprobaba, agregar al inicio de esa prueba `_ajustes.Con(ModuloNotificacion.Reuniones, agrupar: false);` (o el módulo que toque), con un comentario que diga por qué. No cambiar lo que la prueba afirma.

- [ ] **Step 4: Commit**

```powershell
git add src/Infrastructure/Notifications/RecordatorioBackgroundService.cs tests/Application.Tests/Notificaciones/RecordatorioBackgroundServiceTests.cs
git commit -m "El revisor lee el intervalo en cada vuelta, se despierta al guardar y despacha la bandeja"
```

---

### Task 8: Recordatorios manuales bajo el interruptor de correo

**Files:**
- Modify: `src/Application/Notificaciones/Commands/EnviarRecordatorioManual/EnviarRecordatorioManualCommands.cs`
- Modify: `src/Web/Models/AvisoRecordatorio.cs`
- Modify: `tests/Application.Tests/Notificaciones/CorreosEnlacesYNombreTests.cs`, `ResultadoRecordatorioTests.cs`, `EnviarRecordatorioTests.cs`, `CorreosEscapadosTests.cs` (constructores de los handlers)
- Test: `tests/Application.Tests/Notificaciones/RecordatorioCorreoApagadoTests.cs`, `tests/Web.Tests/AvisoRecordatorioCorreoApagadoTests.cs`

**Interfaces:**
- Consumes: `IAjustesNotificaciones` (T4); `EncolarAsync` (T5).
- Produces:
  - `record ResultadoRecordatorio(int AvisosCampana, int CorreosIntentados, int CorreosEnviados, bool ServidorSinConfigurar, bool CorreoApagado = false)`.
  - Los cuatro handlers reciben `IAjustesNotificaciones ajustes` como **último** parámetro del constructor.

- [ ] **Step 1: Pruebas que fallan**

`tests/Application.Tests/Notificaciones/RecordatorioCorreoApagadoTests.cs`. Copiar de `ResultadoRecordatorioTests` cómo siembra el ticket con agente asignado y cómo construye el handler. El agente se llama `agente` y tiene `Correo`:

```csharp
    [Fact]
    public async Task Con_el_correo_de_Tickets_apagado_no_sale_correo_y_el_resultado_lo_dice()
    {
        var notif = Substitute.For<INotificacionService>();
        notif.EncolarAsync(default, default, default!).ReturnsForAnyArgs(true);
        var email = Substitute.For<IEmailService>();
        var ajustes = new AjustesDePrueba(); // correo apagado en todos, como el despliegue

        var r = await new EnviarRecordatorioTicketCommandHandler(_ctx, notif, email, Correo, Inst, ajustes)
            .Handle(new EnviarRecordatorioTicketCommand(_ticketId), CancellationToken.None);

        await email.DidNotReceiveWithAnyArgs().SendEmailAsync(default!, default!, default!, default, default);
        r.CorreoApagado.Should().BeTrue();
        r.AvisosCampana.Should().BeGreaterThan(0);
    }
```

(`_ctx`, `_ticketId`, `Correo` e `Inst` son los mismos campos y la misma siembra de `CorreosEnlacesYNombreTests`; copiar ese constructor.)

`tests/Web.Tests/AvisoRecordatorioCorreoApagadoTests.cs`:

```csharp
using Diger.TramitesEstado.Application.Notificaciones.Commands.EnviarRecordatorioManual;
using Diger.TramitesEstado.Web.Models;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

public sealed class AvisoRecordatorioCorreoApagadoTests
{
    [Fact]
    public void Con_campana_avisa_que_el_correo_esta_apagado_en_el_panel()
    {
        var (nivel, texto) = AvisoRecordatorio.Para(new ResultadoRecordatorio(1, 0, 0, false, CorreoApagado: true), "ok");

        nivel.Should().Be(AvisoRecordatorio.Nivel.Advertencia);
        texto.Should().Contain("Panel de control");
    }

    [Fact]
    public void Sin_campana_ni_correo_es_error()
    {
        var (nivel, _) = AvisoRecordatorio.Para(new ResultadoRecordatorio(0, 0, 0, false, CorreoApagado: true), "ok");
        nivel.Should().Be(AvisoRecordatorio.Nivel.Error);
    }
}
```

Run: `dotnet build Diger.TramitesEstado.sln`
Expected: falla: no existe `CorreoApagado` ni el sexto parámetro.

- [ ] **Step 2: Implementación**

En `EnviarRecordatorioManualCommands.cs`:

1. `ResultadoRecordatorio`:

```csharp
/// <param name="CorreoApagado">El correo del módulo está apagado en el Panel de control: no se intentó.</param>
public sealed record ResultadoRecordatorio(
    int AvisosCampana, int CorreosIntentados, int CorreosEnviados, bool ServidorSinConfigurar, bool CorreoApagado = false)
{
    public int CorreosFallidos => ServidorSinConfigurar ? 0 : CorreosIntentados - CorreosEnviados;
}
```

2. `ConteoCorreos.Resultado`:

```csharp
    public ResultadoRecordatorio Resultado(int avisosCampana, bool correoApagado = false) =>
        new(avisosCampana, Intentados, Enviados, SinConfigurar, correoApagado);
```

3. En cada uno de los cuatro handlers (Ticket, Expediente, Reunión, Compromiso):
   - Agregar `IAjustesNotificaciones ajustes` como último parámetro del constructor primario.
   - Primera línea de `Handle`, con el tipo del handler (`RecordatorioManualTicket`, `RecordatorioManualExpediente`, `RecordatorioManualReunion` o `RecordatorioManualCompromiso`):

     ```csharp
             var correoActivo = (await ajustes.ObtenerAsync(ct))
                 .De(CatalogoNotificaciones.De(TipoNotificacion.RecordatorioManualTicket).Modulo).Correo;
     ```

   - Cada `await notifSvc.EncolarAsync(...); notificados++;` (o el contador equivalente) pasa a `if (await notifSvc.EncolarAsync(..., ct: ct)) notificados++;`.
   - Cada `correos.Anotar(await emailSvc.SendEmailAsync(...));` queda dentro de `if (correoActivo) { ... }`. El armado del cuerpo puede quedar fuera.
   - El `return correos.Resultado(x);` pasa a `return correos.Resultado(x, correoApagado: !correoActivo);`.
   - Comprobar con `grep -n "SendEmailAsync" EnviarRecordatorioManualCommands.cs` que todos quedaron dentro de un `if (correoActivo)`.

En `AvisoRecordatorio.Para`, como **primera** comprobación, justo después de calcular `campana` y `quien`:

```csharp
        if (r.CorreoApagado)
            return campana
                ? (Nivel.Advertencia,
                   "El aviso quedó en la campana de notificaciones. No se envió correo porque el correo de este módulo está apagado en Panel de control → Notificaciones.")
                : (Nivel.Error,
                   $"No se pudo enviar el recordatorio: {quien}, y el correo de este módulo está apagado en Panel de control → Notificaciones.");
```

Pruebas que ya existían. En cada `new EnviarRecordatorio*CommandHandler(... , Correo, Inst)` de `CorreosEnlacesYNombreTests`, `ResultadoRecordatorioTests`, `EnviarRecordatorioTests` y `CorreosEscapadosTests`:
- agregar `AjustesDePrueba.CorreoEncendido()` como último argumento;
- donde el `INotificacionService` sea un `Substitute`, configurarlo con `notif.EncolarAsync(default, default, default!).ReturnsForAnyArgs(true);` para que `AvisosCampana` siga contando como antes.

- [ ] **Step 3: Run tests**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~Notificaciones"` y `dotnet test tests\Web.Tests --filter "FullyQualifiedName~AvisoRecordatorio|FullyQualifiedName~RecordatorioCorreoFallidoTests"`
Expected: PASS.

- [ ] **Step 4: Commit**

```powershell
git add src tests
git commit -m "Los recordatorios manuales respetan el interruptor de correo de su modulo"
```

---

### Task 9: Consultas y comandos del panel

**Files:**
- Create: `src/Application/PanelControl/Notificaciones/PanelNotificaciones.cs`
- Test: `tests/Application.Tests/PanelControl/PanelNotificacionesTests.cs`

**Interfaces:**
- Consumes: T1, T2, T4, `IProbadorCorreo` (T6).
- Produces:
  - `GetPanelNotificacionesQuery → PanelNotificacionesDto`.
  - `GuardarRevisionCommand(int Horas, int Minutos)`.
  - `GuardarModulosCommand(IReadOnlyList<InterruptoresInput> Modulos)` con `record InterruptoresInput(ModuloNotificacion Modulo, bool Campana, bool Correo, bool Agrupar)`.
  - `GuardarCorreoCommand(string Host, int Puerto, bool Ssl, string Usuario, string? Password, bool QuitarPassword, string Remitente, string? NombreRemitente)`.
  - `EnviarCorreoPruebaCommand → ResultadoPruebaCorreo`.
  - DTOs: `ModuloPanelDto(ModuloNotificacion Modulo, string Nombre, bool Campana, bool Correo, bool Agrupar, bool AdmiteCorreo, IReadOnlyList<string> Avisos)`, `CorreoPanelDto(bool DesdePanel, string Host, int Puerto, bool Ssl, string Usuario, string Remitente, string NombreRemitente, bool HayPassword, bool PasswordIlegible)` y `PanelNotificacionesDto(int IntervaloMinutos, DateTime? UltimaRevisionUtc, DateTime? ProximaRevisionUtc, IReadOnlyList<ModuloPanelDto> Modulos, CorreoPanelDto Correo, int CorreosEnEspera, int CorreosFallidos)`.

- [ ] **Step 1: Write the failing tests**

`tests/Application.Tests/PanelControl/PanelNotificacionesTests.cs`:

```csharp
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Application.PanelControl.Notificaciones;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Application.Tests.Notificaciones;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Security;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.PanelControl;

public sealed class PanelNotificacionesTests : IDisposable
{
    private readonly AppDbContext _ctx = new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());

    private readonly AjustesDePrueba _ajustes = new();
    private readonly ProtectorSecretos _protector = new(new EphemeralDataProtectionProvider());
    private readonly SenalRevision _senal = new();
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();

    public PanelNotificacionesTests() => _usuario.Nombre.Returns("Henry Cardona");

    private Task GuardarRevisionAsync(int h, int m) =>
        new GuardarRevisionCommandHandler(_ctx, _ajustes, _senal, _usuario, TimeProvider.System)
            .Handle(new GuardarRevisionCommand(h, m), CancellationToken.None);

    private Task GuardarCorreoAsync(string? password, bool quitar = false) =>
        new GuardarCorreoCommandHandler(_ctx, _ajustes, _protector, _usuario, TimeProvider.System)
            .Handle(new GuardarCorreoCommand("smtp.office365.com", 587, true, "no-reply@diger.gob.hn",
                password, quitar, "no-reply@diger.gob.hn", null), CancellationToken.None);

    [Fact]
    public async Task Quince_minutos_se_guarda_despierta_al_revisor_y_queda_en_la_bitacora()
    {
        var despierto = _senal.EsperarAsync(TimeSpan.FromMinutes(1), CancellationToken.None);

        await GuardarRevisionAsync(0, 15);

        (await _ctx.AjustesNotificaciones.SingleAsync()).IntervaloMinutos.Should().Be(15);
        (await despierto).Should().BeTrue();
        _ajustes.Invalidaciones.Should().Be(1);
        (await _ctx.PanelControlBitacora.SingleAsync()).Detalle.Should().Be("intervalo: 4 h → 15 min");
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(24, 1)]
    public async Task Fuera_de_rango_se_rechaza(int h, int m)
    {
        var act = () => GuardarRevisionAsync(h, m);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public void El_validador_rechaza_cero_horas_cero_minutos()
    {
        new GuardarRevisionValidator().Validate(new GuardarRevisionCommand(0, 0)).IsValid.Should().BeFalse();
        new GuardarRevisionValidator().Validate(new GuardarRevisionCommand(0, 15)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task La_contrasena_queda_cifrada_y_la_bitacora_no_la_dice()
    {
        await GuardarCorreoAsync("Secreta#123");

        var fila = await _ctx.AjustesNotificaciones.SingleAsync();
        fila.SmtpPasswordCifrada.Should().NotBe("Secreta#123");
        _protector.Descifrar(fila.SmtpPasswordCifrada!).Should().Be("Secreta#123");
        (await _ctx.PanelControlBitacora.AnyAsync(b => b.Detalle.Contains("Secreta#123"))).Should().BeFalse();
        (await _ctx.PanelControlBitacora.AnyAsync(b => b.Detalle.Contains("contraseña cambiada"))).Should().BeTrue();
    }

    [Fact]
    public async Task Vacia_conserva_la_anterior_y_quitar_la_borra()
    {
        await GuardarCorreoAsync("Secreta#123");
        var antes = (await _ctx.AjustesNotificaciones.SingleAsync()).SmtpPasswordCifrada;

        await GuardarCorreoAsync(null);
        (await _ctx.AjustesNotificaciones.SingleAsync()).SmtpPasswordCifrada.Should().Be(antes);

        await GuardarCorreoAsync(null, quitar: true);
        (await _ctx.AjustesNotificaciones.SingleAsync()).SmtpPasswordCifrada.Should().BeNull();
    }

    [Fact]
    public async Task La_primera_vez_exige_la_contrasena()
    {
        var act = () => GuardarCorreoAsync(null);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Soporte_no_queda_con_correo_aunque_se_pida()
    {
        await new GuardarModulosCommandHandler(_ctx, _ajustes, _usuario, TimeProvider.System).Handle(
            new GuardarModulosCommand([new InterruptoresInput(ModuloNotificacion.Soporte, true, true, true),
                                       new InterruptoresInput(ModuloNotificacion.Reuniones, true, true, true)]),
            CancellationToken.None);

        (await _ctx.InterruptoresNotificacion.SingleAsync(i => i.Modulo == ModuloNotificacion.Soporte)).CorreoActivo.Should().BeFalse();
        (await _ctx.InterruptoresNotificacion.SingleAsync(i => i.Modulo == ModuloNotificacion.Reuniones)).CorreoActivo.Should().BeTrue();
        (await _ctx.PanelControlBitacora.SingleAsync()).Detalle.Should().Be("Reuniones: correo encendido");
    }

    [Fact]
    public async Task El_panel_nunca_trae_la_contrasena()
    {
        _ajustes.Smtp = AjustesDePrueba.ConSmtp("smtp.office365.com", "u@diger.gob.hn", "Secreta#123");

        var dto = await new GetPanelNotificacionesQueryHandler(_ajustes, _ctx).Handle(new GetPanelNotificacionesQuery(), CancellationToken.None);

        dto.Correo.HayPassword.Should().BeTrue();
        dto.ToString().Should().NotContain("Secreta#123");
        dto.Modulos.Should().HaveCount(5);
    }

    public void Dispose() => _ctx.Dispose();
}
```

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~PanelNotificacionesTests"`
Expected: falla la compilación: no existe el namespace `PanelControl.Notificaciones`.

- [ ] **Step 2: Implementación**

`src/Application/PanelControl/Notificaciones/PanelNotificaciones.cs`:

```csharp
using Diger.TramitesEstado.Application.Notificaciones;
using FluentValidation;

namespace Diger.TramitesEstado.Application.PanelControl.Notificaciones;

// ── Lectura ───────────────────────────────────────────────────────────────
public sealed record ModuloPanelDto(
    ModuloNotificacion Modulo, string Nombre, bool Campana, bool Correo, bool Agrupar,
    bool AdmiteCorreo, IReadOnlyList<string> Avisos);

/// <summary>El correo saliente tal como lo ve la pantalla. <b>Sin la contraseña</b>: solo si hay una.</summary>
public sealed record CorreoPanelDto(
    bool DesdePanel, string Host, int Puerto, bool Ssl, string Usuario,
    string Remitente, string NombreRemitente, bool HayPassword, bool PasswordIlegible);

public sealed record PanelNotificacionesDto(
    int IntervaloMinutos, DateTime? UltimaRevisionUtc, DateTime? ProximaRevisionUtc,
    IReadOnlyList<ModuloPanelDto> Modulos, CorreoPanelDto Correo, int CorreosEnEspera, int CorreosFallidos);

public sealed record GetPanelNotificacionesQuery : IRequest<PanelNotificacionesDto>;

public sealed class GetPanelNotificacionesQueryHandler(IAjustesNotificaciones ajustes, IApplicationDbContext ctx)
    : IRequestHandler<GetPanelNotificacionesQuery, PanelNotificacionesDto>
{
    public async Task<PanelNotificacionesDto> Handle(GetPanelNotificacionesQuery _, CancellationToken ct)
    {
        // El panel muestra lo que hay en la base, no lo que quedó en caché hace un minuto.
        ajustes.Invalidar();
        var a = await ajustes.ObtenerAsync(ct);
        var s = await ajustes.SmtpAsync(ct);

        var modulos = Enum.GetValues<ModuloNotificacion>().Select(m =>
        {
            var i = a.De(m);
            return new ModuloPanelDto(m, CatalogoNotificaciones.Nombre(m), i.Campana, i.Correo, i.Agrupar,
                CatalogoNotificaciones.ModuloAdmiteCorreo(m),
                CatalogoNotificaciones.DelModulo(m).Select(d => d.Etiqueta).ToList());
        }).ToList();

        var espera   = await ctx.CorreosPendientes.AsNoTracking().CountAsync(c => c.Estado == EstadoCorreoPendiente.Pendiente, ct);
        var fallidos = await ctx.CorreosPendientes.AsNoTracking().CountAsync(c => c.Estado == EstadoCorreoPendiente.Fallido, ct);

        return new PanelNotificacionesDto(
            a.IntervaloMinutos, a.UltimaRevisionUtc, a.UltimaRevisionUtc?.AddMinutes(a.IntervaloMinutos), modulos,
            new CorreoPanelDto(s.DesdePanel, s.Host, s.Puerto, s.Ssl, s.Usuario, s.Remitente, s.NombreRemitente,
                s.HayPassword, s.PasswordIlegible),
            espera, fallidos);
    }
}

// ── Ayudantes ─────────────────────────────────────────────────────────────
internal static class PanelNotificaciones
{
    public const string SeccionRevision = "Notificaciones · Revisión";
    public const string SeccionModulos  = "Notificaciones · Módulos";
    public const string SeccionCorreo   = "Notificaciones · Correo";

    /// <summary>La fila única, creada si falta con el intervalo que rige hoy.</summary>
    public static async Task<AjustesNotificaciones> FilaAsync(IApplicationDbContext ctx, IAjustesNotificaciones ajustes, CancellationToken ct)
    {
        var fila = await ctx.AjustesNotificaciones.FirstOrDefaultAsync(a => a.Id == AjustesNotificaciones.IdUnico, ct);
        if (fila is not null) return fila;

        fila = AjustesNotificaciones.CrearPorDefecto((await ajustes.ObtenerAsync(ct)).IntervaloMinutos);
        ctx.AjustesNotificaciones.Add(fila);
        return fila;
    }

    public static string Texto(int minutos) =>
        minutos < 60 ? $"{minutos} min"
        : minutos % 60 == 0 ? $"{minutos / 60} h"
        : $"{minutos / 60} h {minutos % 60} min";

    public static string Actor(ICurrentUserService u) => u.Nombre ?? u.Correo ?? "—";
}

// ── Revisión automática ───────────────────────────────────────────────────
public sealed record GuardarRevisionCommand(int Horas, int Minutos) : IRequest<Unit>;

public sealed class GuardarRevisionValidator : AbstractValidator<GuardarRevisionCommand>
{
    public GuardarRevisionValidator()
    {
        RuleFor(x => x.Horas).InclusiveBetween(0, 24).WithMessage("Las horas van de 0 a 24.");
        RuleFor(x => x.Minutos).InclusiveBetween(0, 59).WithMessage("Los minutos van de 0 a 59.");
        RuleFor(x => x.Horas * 60 + x.Minutos)
            .InclusiveBetween(AjustesNotificaciones.MinimoMinutos, AjustesNotificaciones.MaximoMinutos)
            // Una expresión que no es una propiedad: sin esto FluentValidation no sabe cómo llamarla y lanza.
            .OverridePropertyName("Revision")
            .WithMessage("La revisión automática tiene que ser de entre 5 minutos y 24 horas.");
    }
}

public sealed class GuardarRevisionCommandHandler(
    IApplicationDbContext ctx, IAjustesNotificaciones ajustes, SenalRevision senal,
    ICurrentUserService usuario, TimeProvider reloj) : IRequestHandler<GuardarRevisionCommand, Unit>
{
    public async Task<Unit> Handle(GuardarRevisionCommand cmd, CancellationToken ct)
    {
        var minutos = cmd.Horas * 60 + cmd.Minutos;
        var fila    = await PanelNotificaciones.FilaAsync(ctx, ajustes, ct);
        var antes   = fila.IntervaloMinutos;

        fila.CambiarIntervalo(minutos);
        if (antes != minutos)
            ctx.PanelControlBitacora.Add(PanelControlBitacora.Crear(PanelNotificaciones.SeccionRevision,
                $"intervalo: {PanelNotificaciones.Texto(antes)} → {PanelNotificaciones.Texto(minutos)}",
                PanelNotificaciones.Actor(usuario), reloj.GetUtcNow().UtcDateTime));

        await ctx.SaveChangesAsync(ct);
        ajustes.Invalidar();
        senal.Disparar();
        return Unit.Value;
    }
}

// ── Módulos ───────────────────────────────────────────────────────────────
public sealed record InterruptoresInput(ModuloNotificacion Modulo, bool Campana, bool Correo, bool Agrupar);

public sealed record GuardarModulosCommand(IReadOnlyList<InterruptoresInput> Modulos) : IRequest<Unit>;

public sealed class GuardarModulosCommandHandler(
    IApplicationDbContext ctx, IAjustesNotificaciones ajustes, ICurrentUserService usuario, TimeProvider reloj)
    : IRequestHandler<GuardarModulosCommand, Unit>
{
    public async Task<Unit> Handle(GuardarModulosCommand cmd, CancellationToken ct)
    {
        var filas   = await ctx.InterruptoresNotificacion.ToListAsync(ct);
        var cambios = new List<string>();

        foreach (var input in cmd.Modulos)
        {
            var correo = input.Correo && CatalogoNotificaciones.ModuloAdmiteCorreo(input.Modulo);
            var fila   = filas.FirstOrDefault(f => f.Modulo == input.Modulo);
            if (fila is null)
            {
                fila = InterruptorNotificacion.Crear(input.Modulo);
                ctx.InterruptoresNotificacion.Add(fila);
                filas.Add(fila);
            }

            var (campana0, correo0, agrupar0) = (fila.CampanaActiva, fila.CorreoActivo, fila.Agrupar);
            if (!fila.Cambiar(input.Campana, correo, input.Agrupar)) continue;

            var nombre = CatalogoNotificaciones.Nombre(input.Modulo);
            if (campana0 != input.Campana) cambios.Add($"{nombre}: campana {(input.Campana ? "encendida" : "apagada")}");
            if (correo0  != correo)        cambios.Add($"{nombre}: correo {(correo ? "encendido" : "apagado")}");
            if (agrupar0 != input.Agrupar) cambios.Add($"{nombre}: agrupar {(input.Agrupar ? "encendido" : "apagado")}");
        }

        if (cambios.Count > 0)
            ctx.PanelControlBitacora.Add(PanelControlBitacora.Crear(PanelNotificaciones.SeccionModulos,
                string.Join("; ", cambios), PanelNotificaciones.Actor(usuario), reloj.GetUtcNow().UtcDateTime));

        await ctx.SaveChangesAsync(ct);
        ajustes.Invalidar();
        return Unit.Value;
    }
}

// ── Correo saliente ───────────────────────────────────────────────────────
public sealed record GuardarCorreoCommand(
    string Host, int Puerto, bool Ssl, string Usuario, string? Password, bool QuitarPassword,
    string Remitente, string? NombreRemitente) : IRequest<Unit>
{
    /// <summary>El record imprimiría la contraseña en un registro de MediatR o de una excepción.</summary>
    public override string ToString() => $"GuardarCorreoCommand {{ Host = {Host}, Usuario = {Usuario}, Password = *** }}";
}

public sealed class GuardarCorreoValidator : AbstractValidator<GuardarCorreoCommand>
{
    public GuardarCorreoValidator()
    {
        RuleFor(x => x.Host).NotEmpty().WithMessage("Falta el servidor de correo.").MaximumLength(200);
        RuleFor(x => x.Puerto).InclusiveBetween(1, 65535).WithMessage("El puerto tiene que estar entre 1 y 65535.");
        RuleFor(x => x.Usuario).NotEmpty().WithMessage("Falta el usuario del correo.").MaximumLength(200);
        RuleFor(x => x.Remitente).NotEmpty().EmailAddress().WithMessage("El correo remitente no es una dirección válida.").MaximumLength(200);
        RuleFor(x => x.NombreRemitente).MaximumLength(200);
        RuleFor(x => x.Password).MaximumLength(200);
    }
}

public sealed class GuardarCorreoCommandHandler(
    IApplicationDbContext ctx, IAjustesNotificaciones ajustes, IProtectorSecretos protector,
    ICurrentUserService usuario, TimeProvider reloj) : IRequestHandler<GuardarCorreoCommand, Unit>
{
    public async Task<Unit> Handle(GuardarCorreoCommand cmd, CancellationToken ct)
    {
        var fila = await PanelNotificaciones.FilaAsync(ctx, ajustes, ct);

        if (!fila.SmtpConfigurado && string.IsNullOrEmpty(cmd.Password) && !cmd.QuitarPassword)
            throw new DomainException(
                "Es la primera vez que se guarda el correo desde el panel: escriba la contraseña. " +
                "Si el servidor no la pide, marque «Quitar la contraseña».");

        var host   = cmd.Host.Trim();
        var user   = cmd.Usuario.Trim();
        var rem    = cmd.Remitente.Trim();
        var nombre = string.IsNullOrWhiteSpace(cmd.NombreRemitente) ? null : cmd.NombreRemitente.Trim();

        var cambios = new List<string>();
        if (fila.SmtpHost != host)              cambios.Add($"servidor: {fila.SmtpHost ?? "—"} → {host}");
        if (fila.SmtpPuerto != cmd.Puerto)      cambios.Add($"puerto: {fila.SmtpPuerto} → {cmd.Puerto}");
        if (fila.SmtpSsl != cmd.Ssl)            cambios.Add($"SSL: {(cmd.Ssl ? "activado" : "desactivado")}");
        if (fila.SmtpUsuario != user)           cambios.Add($"usuario: {fila.SmtpUsuario ?? "—"} → {user}");
        if (fila.SmtpRemitente != rem)          cambios.Add($"remitente: {fila.SmtpRemitente ?? "—"} → {rem}");
        if (fila.SmtpNombreRemitente != nombre) cambios.Add($"nombre del remitente: {fila.SmtpNombreRemitente ?? "—"} → {nombre ?? "—"}");

        fila.ConfigurarSmtp(host, cmd.Puerto, cmd.Ssl, user, rem, nombre);

        if (cmd.QuitarPassword)
        {
            if (fila.SmtpPasswordCifrada is not null) cambios.Add("contraseña quitada");
            fila.FijarPasswordCifrada(null);
        }
        else if (!string.IsNullOrEmpty(cmd.Password))
        {
            fila.FijarPasswordCifrada(protector.Cifrar(cmd.Password));
            cambios.Add("contraseña cambiada");
        }

        if (cambios.Count > 0)
            ctx.PanelControlBitacora.Add(PanelControlBitacora.Crear(PanelNotificaciones.SeccionCorreo,
                string.Join("; ", cambios), PanelNotificaciones.Actor(usuario), reloj.GetUtcNow().UtcDateTime));

        await ctx.SaveChangesAsync(ct);
        ajustes.Invalidar();
        return Unit.Value;
    }
}

// ── Correo de prueba ──────────────────────────────────────────────────────
/// <summary>Manda un correo a quien pulsa el botón, con lo que está guardado.</summary>
public sealed record EnviarCorreoPruebaCommand : IRequest<ResultadoPruebaCorreo>;

public sealed class EnviarCorreoPruebaCommandHandler(
    IApplicationDbContext ctx, ICurrentUserService usuario, IProbadorCorreo probador)
    : IRequestHandler<EnviarCorreoPruebaCommand, ResultadoPruebaCorreo>
{
    public async Task<ResultadoPruebaCorreo> Handle(EnviarCorreoPruebaCommand _, CancellationToken ct)
    {
        var uid = usuario.UserId ?? throw new DomainException("No se pudo identificar su usuario.");
        var correo = await ctx.Usuarios.AsNoTracking()
            .Where(u => u.Id == uid).Select(u => u.Correo).FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(correo))
            throw new DomainException("Su usuario no tiene un correo registrado al que mandar la prueba.");

        return await probador.ProbarAsync(correo, ct);
    }
}
```

Si `ICurrentUserService` no expone `Correo` (se usa en `AsignarTicketCommand`, así que debería), cambiar `Actor` a `u.Nombre ?? "—"`.

- [ ] **Step 3: Run tests**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~PanelNotificacionesTests"`
Expected: PASS (10).

- [ ] **Step 4: Commit**

```powershell
git add src/Application/PanelControl tests/Application.Tests/PanelControl
git commit -m "Consultas y comandos del Panel de control de notificaciones"
```

---

### Task 10: Pantallas del Panel de control y menú

**Files:**
- Create: `src/Web/Pages/PanelControl/Index.cshtml`, `Index.cshtml.cs`
- Create: `src/Web/Pages/PanelControl/Notificaciones.cshtml`, `Notificaciones.cshtml.cs`
- Modify: `src/Web/Pages/Shared/_Layout.cshtml` (antes del bloque «Administración», línea ~430)
- Test: `tests/Web.Tests/PanelControlNotificacionesTests.cs`

**Interfaces:**
- Consumes: Task 9 (query y comandos), `AccesoModulosService.PuedeClaveAsync`.
- Produces: rutas `/PanelControl` y `/PanelControl/Notificaciones`; claves `PanelControl.Notificaciones.Ver` y `.Editar`.

- [ ] **Step 1: Write the failing tests**

`tests/Web.Tests/PanelControlNotificacionesTests.cs`:

```csharp
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>El Panel de control de notificaciones servido de verdad: permisos, que la contraseña
/// nunca vuelva al navegador y que el menú solo ofrezca el grupo a quien puede verlo.</summary>
public sealed class PanelControlNotificacionesTests : IAsyncLifetime
{
    private const string Ruta = "/PanelControl/Notificaciones";
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("Consultor", "PanelControl.Notificaciones.Ver");
        await _portal.OtorgarAsync("JefeArea", "PanelControl.Notificaciones.Ver", "PanelControl.Notificaciones.Editar");
    }

    public Task DisposeAsync() { _portal.Dispose(); return Task.CompletedTask; }

    private static string Token(string html) =>
        Regex.Match(html, """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

    private static FormUrlEncodedContent Form(string token, params (string, string)[] campos) =>
        new([new("__RequestVerificationToken", token), .. campos.Select(c => new KeyValuePair<string, string>(c.Item1, c.Item2))]);

    [Fact]
    public async Task Sin_permiso_se_niega_y_el_menu_no_lo_ofrece()
    {
        (await _portal.ClienteComo("Empleado").GetAsync(Ruta)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var html = await _portal.ClienteComo("Empleado").GetStringAsync("/Notificaciones");
        html.Should().NotContain("href=\"/PanelControl");
    }

    [Fact]
    public async Task Con_Ver_se_ve_deshabilitado_y_no_guarda()
    {
        var cliente = _portal.ClienteComo("Consultor");
        var html = await cliente.GetStringAsync(Ruta);
        html.Should().Contain("Revisión automática").And.Contain("disabled");

        var post = await cliente.PostAsync(Ruta + "?handler=Revision", Form(Token(html), ("Horas", "0"), ("Minutos", "15")));
        post.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Con_Editar_se_guarda_la_revision()
    {
        var cliente = _portal.ClienteComo("JefeArea");
        var html = await cliente.GetStringAsync(Ruta);

        var post = await cliente.PostAsync(Ruta + "?handler=Revision", Form(Token(html), ("Horas", "0"), ("Minutos", "15")));
        post.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var despues = await cliente.GetStringAsync(Ruta);
        despues.Should().Contain("name=\"Minutos\"").And.Contain("value=\"15\"");
    }

    [Fact]
    public async Task La_contrasena_guardada_nunca_vuelve_al_navegador()
    {
        var cliente = _portal.ClienteComo("JefeArea");
        var html = await cliente.GetStringAsync(Ruta);

        var post = await cliente.PostAsync(Ruta + "?handler=Correo", Form(Token(html),
            ("Correo.Host", "smtp.office365.com"), ("Correo.Puerto", "587"), ("Correo.Ssl", "true"),
            ("Correo.Usuario", "no-reply@diger.gob.hn"), ("Correo.Password", "Secreta#123"),
            ("Correo.Remitente", "no-reply@diger.gob.hn")));
        post.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var despues = await cliente.GetStringAsync(Ruta);
        despues.Should().NotContain("Secreta#123");
        despues.Should().Contain("Hay una contraseña guardada");
    }

    [Fact]
    public async Task Un_rechazo_conserva_lo_escrito()
    {
        var cliente = _portal.ClienteComo("JefeArea");
        var html = await cliente.GetStringAsync(Ruta);

        var post = await cliente.PostAsync(Ruta + "?handler=Correo", Form(Token(html),
            ("Correo.Host", "smtp.office365.com"), ("Correo.Puerto", "587"),
            ("Correo.Usuario", "no-reply@diger.gob.hn"), ("Correo.Remitente", "no-reply@diger.gob.hn")));

        post.StatusCode.Should().Be(HttpStatusCode.OK);
        var cuerpo = await post.Content.ReadAsStringAsync();
        cuerpo.Should().Contain("smtp.office365.com").And.Contain("escriba la contrase");
    }
}
```

Run: `dotnet test tests\Web.Tests --filter "FullyQualifiedName~PanelControlNotificacionesTests"`
Expected: FAIL (404 en lugar de 403/200).

- [ ] **Step 2: Portada**

`src/Web/Pages/PanelControl/Index.cshtml.cs`:

```csharp
namespace Diger.TramitesEstado.Web.Pages.PanelControl;

/// <summary>Portada del Panel de control: una tarjeta por submódulo. Hoy solo Notificaciones; aquí
/// van a ir los siguientes (tamaño de archivos, etc.), cada uno con su propia clave.</summary>
[Authorize]
[Permission(NotificacionesModel.Clave, AccionModulo.Ver, "Ver la configuración de notificaciones del Panel de control")]
public sealed class IndexModel : PageModel
{
    public void OnGet() { }
}
```

`src/Web/Pages/PanelControl/Index.cshtml`:

```cshtml
@page
@model Diger.TramitesEstado.Web.Pages.PanelControl.IndexModel
@{
    ViewData["Title"] = "Panel de control";
}

<div class="container" style="max-width:1040px">
    <div class="hist-header">
        <div>
            <h2>Panel de control</h2>
            <p>Configuración del portal que antes solo se cambiaba en el servidor. Los cambios aplican sin reiniciar.</p>
        </div>
    </div>

    <div class="cat-grid">
        <a class="cat-card" asp-page="/PanelControl/Notificaciones">
            <span class="cat-ico"><svg class="ico" aria-hidden="true"><use href="#i-accesos"></use></svg></span>
            <span class="cat-titulo">Notificaciones</span>
            <span class="cat-desc">Cada cuánto se revisa, qué módulos avisan por campana y por correo, y desde qué cuenta sale el correo.</span>
        </a>
    </div>
</div>
```

- [ ] **Step 3: Pantalla de Notificaciones**

`src/Web/Pages/PanelControl/Notificaciones.cshtml.cs`:

```csharp
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.PanelControl.Notificaciones;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.PanelControl;

/// <summary>
/// Panel de control → Notificaciones. Un rechazo vuelve a la misma página con lo escrito (no
/// redirige): un redirect borraba el formulario y había que teclearlo todo de nuevo. La contraseña
/// es la excepción: nunca se vuelve a pintar.
/// </summary>
[Authorize]
[Permission(Clave, AccionModulo.Ver, "Ver la configuración de notificaciones del Panel de control")]
public sealed class NotificacionesModel(ISender sender, AccesoModulosService acceso) : PageModel
{
    public const string Clave = "PanelControl.Notificaciones";

    public PanelNotificacionesDto Panel { get; private set; } = default!;
    public bool PuedeEditar { get; private set; }

    [TempData] public string? Aviso { get; set; }
    public string? Error { get; private set; }
    public string? ErrorCorreo { get; private set; }
    public ResultadoPruebaCorreo? Prueba { get; private set; }

    [BindProperty] public int Horas   { get; set; }
    [BindProperty] public int Minutos { get; set; }
    [BindProperty] public List<ModuloForm> Modulos { get; set; } = [];
    [BindProperty] public CorreoForm Correo { get; set; } = new();

    public sealed class ModuloForm
    {
        public ModuloNotificacion Modulo  { get; set; }
        public bool               Campana { get; set; }
        public bool               Correo  { get; set; }
        public bool               Agrupar { get; set; }
    }

    public sealed class CorreoForm
    {
        public string  Host            { get; set; } = "";
        public int     Puerto          { get; set; } = 587;
        public bool    Ssl             { get; set; } = true;
        public string  Usuario         { get; set; } = "";
        public string? Password        { get; set; }
        public bool    QuitarPassword  { get; set; }
        public string  Remitente       { get; set; } = "";
        public string? NombreRemitente { get; set; }
    }

    public async Task OnGetAsync(CancellationToken ct)
    {
        await CargarAsync(ct);
        LlenarDesdePanel(revision: true, modulos: true, correo: true);
    }

    [Permission(Clave, AccionModulo.Editar, "Cambiar la configuración de notificaciones y enviar el correo de prueba")]
    public async Task<IActionResult> OnPostRevisionAsync(CancellationToken ct)
    {
        try
        {
            await sender.Send(new GuardarRevisionCommand(Horas, Minutos), ct);
            Aviso = "Revisión automática guardada. Se aplica desde ahora.";
            return RedirectToPage();
        }
        catch (Exception ex) when (ex is DomainException or FluentValidation.ValidationException)
        {
            Error = Mensaje(ex);
            await CargarAsync(ct);
            LlenarDesdePanel(revision: false, modulos: true, correo: true);
            return Page();
        }
    }

    [Permission(Clave, AccionModulo.Editar, "Cambiar la configuración de notificaciones y enviar el correo de prueba")]
    public async Task<IActionResult> OnPostModulosAsync(CancellationToken ct)
    {
        await sender.Send(new GuardarModulosCommand(
            Modulos.Select(m => new InterruptoresInput(m.Modulo, m.Campana, m.Correo, m.Agrupar)).ToList()), ct);
        Aviso = "Módulos guardados.";
        return RedirectToPage();
    }

    [Permission(Clave, AccionModulo.Editar, "Cambiar la configuración de notificaciones y enviar el correo de prueba")]
    public async Task<IActionResult> OnPostCorreoAsync(CancellationToken ct)
    {
        try
        {
            await sender.Send(new GuardarCorreoCommand(Correo.Host, Correo.Puerto, Correo.Ssl, Correo.Usuario,
                Correo.Password, Correo.QuitarPassword, Correo.Remitente, Correo.NombreRemitente), ct);
            Aviso = "Correo saliente guardado. Use «Enviar correo de prueba» para comprobarlo.";
            return RedirectToPage();
        }
        catch (Exception ex) when (ex is DomainException or FluentValidation.ValidationException)
        {
            ErrorCorreo = Mensaje(ex);
            Correo.Password = null; // la contraseña no se vuelve a pintar, ni siquiera la recién escrita
            await CargarAsync(ct);
            LlenarDesdePanel(revision: true, modulos: true, correo: false);
            return Page();
        }
    }

    [Permission(Clave, AccionModulo.Editar, "Cambiar la configuración de notificaciones y enviar el correo de prueba")]
    public async Task<IActionResult> OnPostProbarAsync(CancellationToken ct)
    {
        try { Prueba = await sender.Send(new EnviarCorreoPruebaCommand(), ct); }
        catch (DomainException ex) { ErrorCorreo = ex.Message; }

        await CargarAsync(ct);
        LlenarDesdePanel(revision: true, modulos: true, correo: true);
        return Page();
    }

    private async Task CargarAsync(CancellationToken ct)
    {
        Panel       = await sender.Send(new GetPanelNotificacionesQuery(), ct);
        PuedeEditar = await acceso.PuedeClaveAsync($"{Clave}.Editar", ct);
    }

    private void LlenarDesdePanel(bool revision, bool modulos, bool correo)
    {
        if (revision) { Horas = Panel.IntervaloMinutos / 60; Minutos = Panel.IntervaloMinutos % 60; }
        if (modulos)
            Modulos = Panel.Modulos.Select(m => new ModuloForm
                { Modulo = m.Modulo, Campana = m.Campana, Correo = m.Correo, Agrupar = m.Agrupar }).ToList();
        if (correo)
            Correo = new CorreoForm
            {
                Host = Panel.Correo.Host, Puerto = Panel.Correo.Puerto, Ssl = Panel.Correo.Ssl,
                Usuario = Panel.Correo.Usuario, Remitente = Panel.Correo.Remitente,
                NombreRemitente = Panel.Correo.NombreRemitente,
            };
    }

    private static string Mensaje(Exception ex) => ex is FluentValidation.ValidationException v
        ? string.Join(" ", v.Errors.Select(e => e.ErrorMessage).Distinct())
        : ex.Message;
}
```

`src/Web/Pages/PanelControl/Notificaciones.cshtml`:

```cshtml
@page
@model Diger.TramitesEstado.Web.Pages.PanelControl.NotificacionesModel
@{
    ViewData["Title"] = "Notificaciones · Panel de control";
    var p   = Model.Panel;
    var dis = Model.PuedeEditar ? null : "disabled";
    string Hora(DateTime? utc) => utc is { } u ? u.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "—";
}

<div class="container" style="max-width:1040px">
    <div class="hist-header">
        <div>
            <h2>Notificaciones</h2>
            <p>Lo que se guarda aquí aplica sin reiniciar el portal.</p>
        </div>
    </div>
    <p class="hint"><a asp-page="/PanelControl/Index">← Panel de control</a></p>

    @if (!string.IsNullOrEmpty(Model.Aviso)) { <div class="alert alert-ok">@Model.Aviso</div> }
    @if (!Model.PuedeEditar) { <div class="alert alert-warn">Puede ver esta configuración, pero no cambiarla.</div> }

    <section class="dash-card" aria-labelledby="pc-rev-t">
        <h3 id="pc-rev-t">Revisión automática</h3>
        <p class="hint">Cada cuánto el sistema revisa plazos y genera los avisos. Cada aviso sale una vez por día y elemento, aunque la revisión sea seguida.</p>
        @if (!string.IsNullOrEmpty(Model.Error)) { <div class="alert alert-error">@Model.Error</div> }
        <form method="post" asp-page-handler="Revision" id="form-revision" style="display:flex;gap:12px;align-items:flex-end;flex-wrap:wrap">
            <div class="field" style="width:110px">
                <label for="pc-horas">Horas</label>
                <input id="pc-horas" name="Horas" type="number" min="0" max="24" value="@Model.Horas" required disabled="@(dis is not null)" />
            </div>
            <div class="field" style="width:110px">
                <label for="pc-min">Minutos</label>
                <input id="pc-min" name="Minutos" type="number" min="0" max="59" value="@Model.Minutos" required disabled="@(dis is not null)" />
            </div>
            <button type="submit" class="btnp" disabled="@(dis is not null)">Guardar</button>
        </form>
        <p class="hint">Última revisión: @Hora(p.UltimaRevisionUtc) · Próxima: @Hora(p.ProximaRevisionUtc)</p>
    </section>

    <section class="dash-card" aria-labelledby="pc-mod-t">
        <h3 id="pc-mod-t">Módulos</h3>
        <p class="hint">Qué módulos avisan en la campana y cuáles también por correo. «Agrupar» junta los avisos parecidos en uno («Tiene 6 compromisos vencidos»).</p>
        <form method="post" asp-page-handler="Modulos">
            <table class="tabla">
                <thead><tr><th>Módulo</th><th>Campana</th><th>Correo</th><th>Agrupar</th></tr></thead>
                <tbody>
                @for (var i = 0; i < Model.Modulos.Count; i++)
                {
                    var info = p.Modulos.First(m => m.Modulo == Model.Modulos[i].Modulo);
                    <tr>
                        <td>
                            <strong>@info.Nombre</strong>
                            <input type="hidden" asp-for="Modulos[i].Modulo" />
                            <div class="hint">@string.Join(" · ", info.Avisos)</div>
                        </td>
                        <td><input type="checkbox" asp-for="Modulos[i].Campana" disabled="@(dis is not null)" aria-label="Campana de @info.Nombre" /></td>
                        <td>
                            @if (info.AdmiteCorreo)
                            {
                                <input type="checkbox" asp-for="Modulos[i].Correo" disabled="@(dis is not null)" aria-label="Correo de @info.Nombre" />
                            }
                            else
                            {
                                <span class="hint" title="Un correo por cada chat sería ruido">no aplica</span>
                            }
                        </td>
                        <td><input type="checkbox" asp-for="Modulos[i].Agrupar" disabled="@(dis is not null)" aria-label="Agrupar en @info.Nombre" /></td>
                    </tr>
                }
                </tbody>
            </table>
            <button type="submit" class="btnp" disabled="@(dis is not null)">Guardar módulos</button>
        </form>
        <p class="hint">Correos en espera: @p.CorreosEnEspera · Fallidos: @p.CorreosFallidos</p>
    </section>

    <section class="dash-card" aria-labelledby="pc-mail-t">
        <h3 id="pc-mail-t">Correo saliente</h3>
        <p class="hint">
            @(p.Correo.DesdePanel ? "Configurado desde este panel." : "Todavía se usa la configuración del servidor (appsettings). El primer guardado aquí toma el control.")
        </p>
        @if (p.Correo.PasswordIlegible)
        {
            <div class="alert alert-error">La contraseña guardada no se puede leer en este servidor; vuelva a escribirla.</div>
        }
        @if (!string.IsNullOrEmpty(Model.ErrorCorreo)) { <div class="alert alert-error">@Model.ErrorCorreo</div> }
        @if (Model.Prueba is { } prueba)
        {
            <div class="alert @(prueba.Resultado == Diger.TramitesEstado.Application.Common.Interfaces.ResultadoCorreo.Enviado ? "alert-ok" : "alert-error")">Correo de prueba: @prueba.Detalle</div>
        }
        <form method="post" asp-page-handler="Correo" id="form-correo">
            <div style="display:flex;gap:12px;flex-wrap:wrap">
                <div class="field" style="flex:2;min-width:220px">
                    <label for="pc-host">Servidor</label>
                    <input id="pc-host" asp-for="Correo.Host" required maxlength="200" disabled="@(dis is not null)" />
                </div>
                <div class="field" style="width:110px">
                    <label for="pc-puerto">Puerto</label>
                    <input id="pc-puerto" asp-for="Correo.Puerto" type="number" min="1" max="65535" required disabled="@(dis is not null)" />
                </div>
                <div class="field" style="width:110px">
                    <label><input type="checkbox" asp-for="Correo.Ssl" disabled="@(dis is not null)" /> SSL</label>
                </div>
            </div>
            <div style="display:flex;gap:12px;flex-wrap:wrap">
                <div class="field" style="flex:1;min-width:220px">
                    <label for="pc-usuario">Usuario</label>
                    <input id="pc-usuario" asp-for="Correo.Usuario" required maxlength="200" autocomplete="off" disabled="@(dis is not null)" />
                </div>
                <div class="field" style="flex:1;min-width:220px">
                    <label for="pc-pass">Contraseña</label>
                    <input id="pc-pass" name="Correo.Password" type="password" maxlength="200" autocomplete="new-password"
                           placeholder="@(p.Correo.HayPassword ? "Hay una contraseña guardada (déjelo vacío para conservarla)" : "Sin contraseña guardada")"
                           disabled="@(dis is not null)" />
                    @if (p.Correo.HayPassword) { <div class="hint">Hay una contraseña guardada.</div> }
                    <label class="hint"><input type="checkbox" asp-for="Correo.QuitarPassword" disabled="@(dis is not null)" /> Quitar la contraseña</label>
                </div>
            </div>
            <div style="display:flex;gap:12px;flex-wrap:wrap">
                <div class="field" style="flex:1;min-width:220px">
                    <label for="pc-rem">Correo remitente</label>
                    <input id="pc-rem" asp-for="Correo.Remitente" type="email" required maxlength="200" disabled="@(dis is not null)" />
                </div>
                <div class="field" style="flex:1;min-width:220px">
                    <label for="pc-nom">Nombre del remitente</label>
                    <input id="pc-nom" asp-for="Correo.NombreRemitente" maxlength="200" placeholder="@Diger.TramitesEstado.Application.Common.Models.Marca.Nombre" disabled="@(dis is not null)" />
                </div>
            </div>
            <div style="display:flex;gap:12px">
                <button type="submit" class="btnp" disabled="@(dis is not null)">Guardar correo</button>
                <button type="submit" class="btns" formaction="?handler=Probar" formnovalidate disabled="@(dis is not null)">Enviar correo de prueba</button>
            </div>
        </form>
    </section>
</div>

@section Scripts {
<script>
    (function () {
        /* 0 h 0 min no se manda: es la regla de GuardarRevisionValidator, revisada antes del POST. */
        var form = document.getElementById('form-revision');
        if (!form || !window._dgPrevalidar) return;
        window._dgPrevalidar.enlazar(form, [
            function () {
                var h = parseInt(document.getElementById('pc-horas').value || '0', 10);
                var m = parseInt(document.getElementById('pc-min').value || '0', 10);
                var total = h * 60 + m;
                if (total < 5 || total > 1440)
                    return { campo: document.getElementById('pc-min'), mensaje: 'La revisión tiene que ser de entre 5 minutos y 24 horas.' };
            }
        ]);
    })();
</script>
}
```

La clase `btns` y el `formaction` con `?handler=Probar`: si el portal usa otro nombre para el botón secundario, buscarlo con `grep -rn 'class="btns\|class="btn-sec' src/Web/Pages | head -3` y usar ese. El texto «Hay una contraseña guardada» tiene que aparecer en el HTML, porque lo comprueba la prueba Web.

- [ ] **Step 4: Menú**

En `_Layout.cshtml`, justo **antes** del comentario `@* «Administración» agrupa lo que se administra...`:

```cshtml
                        @* «Panel de control»: configuración del portal que antes solo se tocaba en el
                           servidor. Se gatea con la clave de su destino, como el resto del menú. *@
                        @if (await Acceso.PuedeClaveAsync("PanelControl.Notificaciones.Ver"))
                        {
                            <div class="nav-group @(EsRuta("/PanelControl") ? "on" : "")">
                                <button type="button" class="nav-group-btn" onclick="toggleNavGroup(this)">Panel de control<span class="nav-caret">&#9662;</span></button>
                                <div class="nav-group-menu">
                                    <a class="nav-item @(EsRuta("/PanelControl/Notificaciones") ? "on" : "")" asp-page="/PanelControl/Notificaciones"><svg class="ico"><use href="#i-accesos"></use></svg>Notificaciones</a>
                                </div>
                            </div>
                        }
```

- [ ] **Step 5: Run tests**

Run: `dotnet test tests\Web.Tests --filter "FullyQualifiedName~PanelControlNotificacionesTests"`
Expected: PASS (5). Si `Con_Editar_se_guarda_la_revision` falla porque el revisor del `PortalFactory` corre a la vez, no es eso: la espera inicial es de 2 min. Revisar entonces que `Minutos` se pinte con `value="15"`.

- [ ] **Step 6: Commit**

```powershell
git add src/Web/Pages/PanelControl src/Web/Pages/Shared/_Layout.cshtml tests/Web.Tests/PanelControlNotificacionesTests.cs
git commit -m "Pantalla Panel de control -> Notificaciones y su grupo en el menu"
```

---

### Task 11: Detalle de un aviso agrupado

**Files:**
- Create: `src/Application/Notificaciones/Queries/GetNotificacionDetalleQuery.cs`
- Create: `src/Web/Pages/Notificaciones/Detalle.cshtml`, `Detalle.cshtml.cs`
- Test: `tests/Web.Tests/NotificacionDetalleTests.cs`

**Interfaces:**
- Consumes: `Notificacion.Elementos` (T2), `INotificacionService.MarcarLeidaAsync`.
- Produces: `GetNotificacionDetalleQuery(int Id) → NotificacionDetalleDto(int Id, string Titulo, TipoNotificacion Tipo, bool Leida, DateTime Fecha, IReadOnlyList<ElementoNotificacionDto> Elementos)` y `ElementoNotificacionDto(string Titulo, string? Url, DateTime FechaUtc)`. Ruta `/Notificaciones/{id:int}`.

- [ ] **Step 1: Write the failing test**

`tests/Web.Tests/NotificacionDetalleTests.cs`:

```csharp
using System.Net;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

public sealed class NotificacionDetalleTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _id;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var empleado = await db.AsignacionesUsuario.Where(a => a.Rol == "Empleado").Select(a => a.UsuarioId).FirstAsync();
        var n = Notificacion.Crear(empleado, TipoNotificacion.CompromisoVencido, "Compromiso vencido: Acta firmada", "/c/1");
        n.Sumar("/c/2", "Compromiso vencido: Informe mensual", "/c/2", x => $"Tiene {x} compromisos vencidos", DateTime.UtcNow);
        db.Notificaciones.Add(n);
        await db.SaveChangesAsync();
        _id = n.Id;
    }

    public Task DisposeAsync() { _portal.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task El_dueno_ve_los_elementos_del_grupo()
    {
        var html = await _portal.ClienteComo("Empleado").GetStringAsync($"/Notificaciones/{_id}");

        html.Should().Contain("Tiene 2 compromisos vencidos");
        html.Should().Contain("Acta firmada").And.Contain("Informe mensual");
        html.Should().Contain("href=\"/c/2\"");
    }

    [Fact]
    public async Task Otra_persona_recibe_404()
    {
        (await _portal.ClienteComo("Consultor").GetAsync($"/Notificaciones/{_id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

Si la columna del rol en `AsignacionUsuario` no se llama `Rol`, ver `AsignacionUsuario.Crear(usuarioId, institucion, area, unidad, rol)` (usado en `InteresadosAutomaticosSyncTests`) y usar ese nombre de propiedad.

Run: `dotnet test tests\Web.Tests --filter "FullyQualifiedName~NotificacionDetalleTests"`
Expected: FAIL (404 para el dueño).

- [ ] **Step 2: Consulta**

`src/Application/Notificaciones/Queries/GetNotificacionDetalleQuery.cs`:

```csharp
namespace Diger.TramitesEstado.Application.Notificaciones.Queries;

public sealed record ElementoNotificacionDto(string Titulo, string? Url, DateTime FechaUtc);

public sealed record NotificacionDetalleDto(
    int Id, string Titulo, TipoNotificacion Tipo, bool Leida, DateTime Fecha,
    IReadOnlyList<ElementoNotificacionDto> Elementos);

/// <summary>Un aviso con sus elementos. Solo para su destinatario: el de otra persona es 404, no 403,
/// para no confirmar que existe.</summary>
public sealed record GetNotificacionDetalleQuery(int Id) : IRequest<NotificacionDetalleDto>;

public sealed class GetNotificacionDetalleQueryHandler(IApplicationDbContext ctx, ICurrentUserService usuario)
    : IRequestHandler<GetNotificacionDetalleQuery, NotificacionDetalleDto>
{
    public async Task<NotificacionDetalleDto> Handle(GetNotificacionDetalleQuery q, CancellationToken ct)
    {
        var uid = usuario.UserId ?? throw new NotFoundException(nameof(Notificacion), q.Id);

        var n = await ctx.Notificaciones.AsNoTracking()
            .Include(x => x.Elementos)
            .FirstOrDefaultAsync(x => x.Id == q.Id && x.DestinatarioId == uid, ct)
            ?? throw new NotFoundException(nameof(Notificacion), q.Id);

        return new NotificacionDetalleDto(n.Id, n.Titulo, n.Tipo, n.Leida, n.FechaActualizacion,
            n.Elementos.OrderByDescending(e => e.FechaUtc)
                       .Select(e => new ElementoNotificacionDto(e.Titulo, e.Url, e.FechaUtc)).ToList());
    }
}
```

- [ ] **Step 3: Página**

`src/Web/Pages/Notificaciones/Detalle.cshtml.cs`:

```csharp
using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Application.Notificaciones.Queries;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.Notificaciones;

[Authorize]
[PermisoNoRequerido("Autoservicio: cada usuario ve el detalle de sus propias notificaciones.")]
public sealed class DetalleModel(ISender sender, INotificacionService notifSvc, ICurrentUserService usuario) : PageModel
{
    public NotificacionDetalleDto Aviso { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        try { Aviso = await sender.Send(new GetNotificacionDetalleQuery(id), ct); }
        catch (NotFoundException) { return NotFound(); }

        // Abrirlo es leerlo: lo nuevo que llegue del mismo tipo empieza otro aviso.
        if (!Aviso.Leida && usuario.UserId is { } uid)
            await notifSvc.MarcarLeidaAsync(id, uid, ct);

        return Page();
    }
}
```

`src/Web/Pages/Notificaciones/Detalle.cshtml`:

```cshtml
@page "/Notificaciones/{id:int}"
@model Diger.TramitesEstado.Web.Pages.Notificaciones.DetalleModel
@{
    ViewData["Title"] = Model.Aviso.Titulo;
}

<div class="container lista" style="max-width:820px">
    <div class="hist-header">
        <div>
            <h2>@Model.Aviso.Titulo</h2>
            <p>Último: @Model.Aviso.Fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm")</p>
        </div>
    </div>
    <p class="hint"><a asp-page="/Notificaciones/Index">← Todas las notificaciones</a></p>

    <ul class="notif-detalle">
        @foreach (var e in Model.Aviso.Elementos)
        {
            <li>
                @if (!string.IsNullOrEmpty(e.Url)) { <a href="@e.Url">@e.Titulo</a> } else { @e.Titulo }
                <span class="hint"> · @e.FechaUtc.ToLocalTime().ToString("dd/MM HH:mm")</span>
            </li>
        }
    </ul>
</div>
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests\Web.Tests --filter "FullyQualifiedName~NotificacionDetalleTests"`
Expected: PASS (2).

- [ ] **Step 5: Commit**

```powershell
git add src/Application/Notificaciones/Queries src/Web/Pages/Notificaciones tests/Web.Tests/NotificacionDetalleTests.cs
git commit -m "Detalle de un aviso agrupado en /Notificaciones/{id}"
```

---

### Task 12: Despliegue, bitácora y verificación completa

**Files:**
- Create: `docs/despliegue/2026-10-22_panel_control_notificaciones.md`
- Modify: `docs/BITACORA.md`

- [ ] **Step 1: Notas de despliegue**

`docs/despliegue/2026-10-22_panel_control_notificaciones.md`, con el formato de `docs/despliegue/2026-10-22_tanda1_entrega2_notificaciones.md` (leerlo primero). Tiene que decir, con estas palabras:
- **Orden:** correr `database/2026-10-22_panel_control_notificaciones.sql` **después** de `2026-10-22_poner_al_dia.sql`. Los controles finales tienen que decir `presente`.
- **El correo arranca apagado en los cinco módulos.** Desde el despliegue, los recordatorios manuales de Tickets, Reuniones y Expedientes **dejan de mandar correo** hasta que alguien encienda «Correo» en Panel de control → Notificaciones. Encenderlo módulo por módulo: con el correo encendido, los avisos automáticos del módulo también salen por correo, uno por persona por revisión como máximo.
- **El intervalo arranca en 4 horas** aunque el servidor tuviera otro `Notificaciones__IntervaloHoras`. Si era distinto, ajustarlo en el panel.
- **La carpeta de claves de Data Protection** (la de `PersistKeysToFileSystem` en `Program.cs`) tiene que conservarse entre despliegues. Si se pierde, la contraseña guardada deja de leerse: el panel lo dice en rojo y hay que volver a escribirla.
- **Permiso:** `PanelControl.Notificaciones` → Ver / Editar en `/Accesos/Permisos`. Los administradores entran sin otorgarlo.
- **Pendiente aparte:** la contraseña de Gmail versionada en `appsettings.json` desde `0462b14`. Cambiarla en la cuenta y escribir la nueva en el panel.

- [ ] **Step 2: Bitácora**

Agregar una entrada en `docs/BITACORA.md` con el formato de las anteriores (leer las últimas): fecha 2026-10-22, «Panel de control → Notificaciones (entrega 1)», qué se hizo, el enlace a la especificación y a este plan, y que la entrega 2 son las reglas de correo.

- [ ] **Step 3: Verificación completa**

Run: `dotnet build Diger.TramitesEstado.sln`
Run: `dotnet test Diger.TramitesEstado.sln`
Expected: 0 errores; todas las suites pasan (Domain, Application, Api, Web). Si algo falla, no seguir: arreglarlo con `superpowers:systematic-debugging`.

Run (dos veces): `sqlcmd -S "localhost\SQL2025" -E -d GestionGD_new -b -I -f 65001 -i database\2026-10-22_panel_control_notificaciones.sql`
Expected: la segunda vuelta dice «ya estaba» en todo y los controles dicen `presente`.

- [ ] **Step 4: Prueba en la app real**

Levantar con `$env:ASPNETCORE_ENVIRONMENT='Development'; dotnet run --project src\Web --launch-profile Diger.TramitesEstado.Web` (memoria «Perfil de lanzamiento correcto»: con otro perfil cae a `GestionGD_TEST`). Entrar como `admin@diger.gob.hn` / `Admin#2026` por `https://localhost:49175` y comprobar con curl y sesión real (memoria «Verificar sin navegador, con curl»):
- `/PanelControl/Notificaciones` responde 200 y muestra «Revisión automática», los cinco módulos y «Correo saliente»;
- el menú tiene «Panel de control»;
- guardar 0 h 15 min deja «Próxima» unos 15 minutos después de «Última» tras la siguiente revisión.

No guardar el bloque de correo contra `GestionGD_new` con una contraseña real.

- [ ] **Step 5: Commit**

```powershell
git add docs/despliegue/2026-10-22_panel_control_notificaciones.md docs/BITACORA.md
git commit -m "Notas de despliegue y bitacora del Panel de control de notificaciones"
```
