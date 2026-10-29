# Calendario de Outlook al portal — plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cada 15 minutos, las citas del calendario de Outlook de cada usuario conectado (hoy a +90 días) entran al portal como reuniones privadas y se mantienen al día; Outlook manda sobre los Generales y sobre los invitados que trajo, que entran como pre-registrados.

**Architecture:** Un `BackgroundService` (Infrastructure) recorre las conexiones vigentes y despacha `SincronizarCalendarioOutlookCommand(usuarioId)`. El handler (Application) lee las citas por el puerto `ICalendarioOutlookLectura`, las traduce con `CitaOutlookTraductor` a `GeneralesOutlook` (Domain) y crea, actualiza o marca canceladas reuniones mediante métodos nuevos de `Reunion`. `GraphCalendarioOutlookLectura` (Infrastructure) implementa el puerto con `me/calendarView`.

**Tech Stack:** .NET 9, EF Core (SQL Server), MediatR, xUnit + FluentAssertions + NSubstitute, EF In-Memory en pruebas.

**Spec:** `docs/superpowers/specs/2026-10-29-calendario-outlook-al-portal-design.md`

## Global Constraints

- Ventana: de hoy (hora de Honduras) a hoy + 90 días. Intervalo: 15 minutos.
- Toda reunión importada entra con `Visibilidad = Privada` y `CreadoPorId` = el usuario dueño del calendario.
- Outlook manda solo sobre: Título, Fecha, Hora, Duración, Lugar y enlace de Teams. Nada más se toca después de crear.
- Una cita cancelada o borrada en Outlook se **marca** (`CanceladaEnOutlookEl`); nunca se borra la reunión.
- Las reuniones importadas **no** son "En Outlook" (`OutlookEventId` queda null): el portal nunca les manda cambios ni cancelaciones a Outlook.
- `OrigenExternoId` mide 60: se guarda `outlook:` + SHA-256 base64url del id del evento (51 caracteres). El id completo va en `OutlookImportadoEventId`.
- Límites de columna existentes: `Titulo` 250, `Lugar` 250, `Hora` 20, `Duracion` 60; `Asistentes.Nombre` 150, `Asistentes.Correo` 200. Truncar al traducir.
- Invitados: entran como `Asistente` con `EsPreregistro = true`, `Confirmado = null`, `DeOutlook = true`. Sin recursos (salas) ni el dueño del calendario. Sin duplicar por correo (sin distinguir mayúsculas). Se quita uno retirado en Outlook solo si `DeOutlook`, `Confirmado == null` y `!AutoRegistro`. La respuesta del invitado (aceptó/rechazó) no se traduce a asistencia.
- Logs: id de usuario y conteos. Nunca tokens, asuntos, lugares ni invitados.
- Todo cambio de base lleva migración EF **y** script `database/2026-10-29_poner_al_dia.sql` (va después de `2026-10-27_poner_al_dia.sql`).
- Comandos: build `dotnet build Diger.TramitesEstado.sln`; si falla solo con `MSB3027`/`MSB3021`, detener `Diger.TramitesEstado.Web` y repetir.

---

### Task 0: Prerrequisito — árbol limpio en los archivos que se tocan

Otra sesión dejó cambios sin confirmar en `src/Domain/Entities/Reunion.cs`, `src/Application/Reuniones/Queries/GetReuniones/GetReunionesQuery.cs`, `src/Web/Pages/Reuniones/Editor.cshtml`, `src/Web/Pages/Reuniones/Index.cshtml` y sus pruebas. Si quedan sin confirmar, la migración de la Task 2 se llevaría cambios de modelo ajenos y los commits mezclarían dos trabajos.

- [ ] **Step 1: Comprobar**

Run: `git status --short src/Domain/Entities/Reunion.cs src/Application/Reuniones src/Web/Pages/Reuniones src/Infrastructure/Persistence`
Expected: sin salida.

- [ ] **Step 2: Si hay salida, DETENERSE** y preguntar al usuario si esos cambios se confirman o se descartan. No seguir con la Task 1 hasta que el comando del Step 1 no imprima nada.

---

### Task 1: Dominio — `GeneralesOutlook` y la reunión importada

**Files:**
- Create: `src/Domain/Entities/GeneralesOutlook.cs` (también `InvitadoImportado`)
- Modify: `src/Domain/Entities/Reunion.cs` (bloque `// ── Outlook (fase 1…`, `PrepararEnvioOutlook` y la clase `Asistente`)
- Test: `tests/Domain.Tests/ReunionCalendarioOutlookTests.cs`

**Interfaces:**
- Produces:
  - `record GeneralesOutlook(string Titulo, DateOnly Fecha, string? Hora, string? Duracion, string? Lugar, string? EnlaceTeams, bool EsOrganizador, string? ICalUId, DateTime? ModificadaElUtc)`
  - `static string Reunion.ClaveOrigenOutlook(string eventId)`
  - `static Reunion Reunion.ImportarDeOutlook(string eventId, GeneralesOutlook g, Guid usuarioId, string? institucionId, string? areaId, string? unidadId)`
  - `bool Reunion.ActualizarDeOutlook(GeneralesOutlook g)` — true si cambió algo
  - `bool Reunion.MarcarCanceladaEnOutlook(DateTime utcAhora)` — true si no estaba marcada
  - `bool Reunion.QuitarCancelacionOutlook()` — true si estaba marcada
  - `void Reunion.RecordarICalUId(string? iCalUId)`
  - `record InvitadoImportado(string Nombre, string Correo)` — ya filtrado y recortado por quien lo arma
  - `bool Reunion.SincronizarInvitadosOutlook(IReadOnlyCollection<InvitadoImportado> invitados)` — true si agregó o quitó alguno
  - `bool Asistente.DeOutlook { get; set; }`
  - propiedades: `OutlookImportadoEventId`, `OutlookICalUId`, `OutlookImportadaPorId`, `OutlookModificadaEl`, `CanceladaEnOutlookEl`, `EsImportadaDeOutlook`

- [ ] **Step 1: Escribir las pruebas que fallan**

```csharp
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Domain.Tests;

/// <summary>La reunión que entra desde el calendario de Outlook: privada, de su dueño, y con
/// Outlook mandando solo sobre los Generales.</summary>
public class ReunionCalendarioOutlookTests
{
    private static readonly Guid Ana = Guid.NewGuid();
    private const string EventId = "AAMkAGI2TG93AAA=";

    private static GeneralesOutlook G(string titulo = "Mesa técnica", string? hora = "09:00", string? lugar = "Sala 2",
        string? teams = null, bool organizador = false) =>
        new(titulo, new DateOnly(2026, 11, 3), hora, "1:00", lugar, teams, organizador, "ical-1",
            new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Importar_crea_privada_del_usuario_con_sus_generales()
    {
        var r = Reunion.ImportarDeOutlook(EventId, G(), Ana, "DIGER", "AREA1", "UNI1");

        r.Titulo.Should().Be("Mesa técnica");
        r.Fecha.Should().Be(new DateOnly(2026, 11, 3));
        r.Hora.Should().Be("09:00");
        r.Duracion.Should().Be("1:00");
        r.Lugar.Should().Be("Sala 2");
        r.Modalidad.Should().Be("Presencial");
        r.Visibilidad.Should().Be(VisibilidadReunion.Privada);
        r.CreadoPorId.Should().Be(Ana);
        r.OutlookImportadaPorId.Should().Be(Ana);
        r.InstitucionDuenaId.Should().Be("DIGER");
        r.AreaId.Should().Be("AREA1");
        r.UnidadId.Should().Be("UNI1");
        r.OrigenExternoId.Should().Be(Reunion.ClaveOrigenOutlook(EventId));
        r.OutlookImportadoEventId.Should().Be(EventId);
        r.OutlookICalUId.Should().Be("ical-1");
        r.EsImportadaDeOutlook.Should().BeTrue();
        r.EstaEnOutlook.Should().BeFalse("el portal nunca le manda cambios a una cita importada");
    }

    [Fact]
    public void La_clave_de_origen_cabe_en_60_caracteres_y_es_estable()
    {
        var largo = new string('A', 400);
        Reunion.ClaveOrigenOutlook(largo).Should().HaveLength(51).And.StartWith("outlook:");
        Reunion.ClaveOrigenOutlook(largo).Should().Be(Reunion.ClaveOrigenOutlook(largo));
        Reunion.ClaveOrigenOutlook(largo).Should().NotBe(Reunion.ClaveOrigenOutlook(largo + "B"));
    }

    [Fact]
    public void Con_Teams_es_virtual_y_solo_el_organizador_queda_como_tal()
    {
        var org = Reunion.ImportarDeOutlook(EventId, G(teams: "https://teams/x", organizador: true), Ana, "DIGER", null, null);
        var inv = Reunion.ImportarDeOutlook(EventId + "2", G(teams: "https://teams/y"), Ana, "DIGER", null, null);

        org.Modalidad.Should().Be("Virtual");
        org.OutlookEnlaceTeams.Should().Be("https://teams/x");
        org.OutlookOrganizadorId.Should().Be(Ana, "habilita bajar la transcripción con su conexión");
        inv.OutlookOrganizadorId.Should().BeNull();
    }

    [Fact]
    public void Actualizar_cambia_solo_generales_y_dice_si_hubo_cambio()
    {
        var r = Reunion.ImportarDeOutlook(EventId, G(), Ana, "DIGER", null, null);
        r.ObjetivoAgenda = "Escrito en el portal";
        r.Visibilidad = VisibilidadReunion.Publica;

        r.ActualizarDeOutlook(G()).Should().BeFalse();
        r.ActualizarDeOutlook(G(titulo: "Mesa técnica (movida)", hora: "10:30")).Should().BeTrue();

        r.Titulo.Should().Be("Mesa técnica (movida)");
        r.Hora.Should().Be("10:30");
        r.ObjetivoAgenda.Should().Be("Escrito en el portal");
        r.Visibilidad.Should().Be(VisibilidadReunion.Publica);
    }

    [Fact]
    public void Cancelar_y_quitar_cancelacion_son_idempotentes()
    {
        var r = Reunion.ImportarDeOutlook(EventId, G(), Ana, "DIGER", null, null);
        var t = new DateTime(2026, 10, 29, 18, 0, 0, DateTimeKind.Utc);

        r.MarcarCanceladaEnOutlook(t).Should().BeTrue();
        r.MarcarCanceladaEnOutlook(t.AddHours(1)).Should().BeFalse();
        r.CanceladaEnOutlookEl.Should().Be(t);

        r.QuitarCancelacionOutlook().Should().BeTrue();
        r.QuitarCancelacionOutlook().Should().BeFalse();
        r.CanceladaEnOutlookEl.Should().BeNull();
    }

    [Fact]
    public void Los_invitados_entran_como_preregistrados_de_Outlook_sin_duplicar_por_correo()
    {
        var r = Reunion.ImportarDeOutlook(EventId, G(), Ana, "DIGER", null, null);
        r.Agregar(new Asistente { Nombre = "Luis (QR)", Correo = "luis@ihtt.gob.hn", AutoRegistro = true });

        r.SincronizarInvitadosOutlook([new("Marta", "marta@copeco.gob.hn"), new("Luis", "LUIS@ihtt.gob.hn")])
            .Should().BeTrue();

        r.Asistentes.Should().HaveCount(2);
        var marta = r.Asistentes.Single(a => a.Correo == "marta@copeco.gob.hn");
        marta.EsPreregistro.Should().BeTrue();
        marta.DeOutlook.Should().BeTrue();
        marta.Confirmado.Should().BeNull();

        r.SincronizarInvitadosOutlook([new("Marta", "marta@copeco.gob.hn"), new("Luis", "luis@ihtt.gob.hn")])
            .Should().BeFalse("nada cambió");
    }

    [Fact]
    public void Un_retirado_en_Outlook_se_quita_solo_si_vino_de_Outlook_y_sigue_pendiente()
    {
        var r = Reunion.ImportarDeOutlook(EventId, G(), Ana, "DIGER", null, null);
        r.SincronizarInvitadosOutlook([new("Pendiente", "p@x.hn"), new("Asistio", "a@x.hn")]);
        r.Asistentes.Single(a => a.Correo == "a@x.hn").Confirmado = true;
        r.Agregar(new Asistente { Nombre = "Manual", Correo = "m@x.hn", EsPreregistro = true });

        r.SincronizarInvitadosOutlook([]).Should().BeTrue();

        r.Asistentes.Select(a => a.Correo).Should().BeEquivalentTo(["a@x.hn", "m@x.hn"]);
    }

    [Fact]
    public void Una_importada_no_se_puede_enviar_a_Outlook()
    {
        var r = Reunion.ImportarDeOutlook(EventId, G(teams: "https://teams/x"), Ana, "DIGER", null, null);

        var act = () => r.PrepararEnvioOutlook(conTeams: true);

        act.Should().Throw<DomainException>().WithMessage("*viene de su Outlook*");
    }
}
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test tests\Domain.Tests --filter "FullyQualifiedName~ReunionCalendarioOutlook"`
Expected: FAIL de compilación (`GeneralesOutlook` y `ImportarDeOutlook` no existen).

- [ ] **Step 3: Crear `src/Domain/Entities/GeneralesOutlook.cs`**

```csharp
namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>Un invitado de la cita, ya sin recursos ni el dueño del calendario, y recortado a los
/// largos de Asistentes (Nombre 150, Correo 200). Lo arma CitaOutlookTraductor.</summary>
public sealed record InvitadoImportado(string Nombre, string Correo);

/// <summary>
/// Lo que una cita de Outlook decide sobre su reunión del portal (spec 2026-10-29): Outlook manda
/// solo sobre estos campos. Fecha y Hora en hora de Honduras; Hora y Duracion nulas en una cita de
/// todo el día. <see cref="EsOrganizador"/> dice si el dueño del calendario la organizó: solo
/// entonces su conexión puede bajar la transcripción de Teams.
/// </summary>
public sealed record GeneralesOutlook(
    string Titulo, DateOnly Fecha, string? Hora, string? Duracion, string? Lugar,
    string? EnlaceTeams, bool EsOrganizador, string? ICalUId, DateTime? ModificadaElUtc);
```

- [ ] **Step 4: Agregar a `Reunion.cs`, después de `public string? OutlookError { get; private set; }`**

```csharp
    // ── Calendario de Outlook al portal (spec 2026-10-29) ─────────
    // Sentido contrario al de arriba: la cita nació en Outlook y la reunión es su copia. Outlook
    // manda solo sobre los Generales (GeneralesOutlook); lo demás es del portal. OutlookEventId
    // queda null a propósito: EstaEnOutlook es «el portal manda esta cita», y aquí no manda.

    /// <summary>Id de la cita en el calendario de <see cref="OutlookImportadaPorId"/>. El id es
    /// largo (~150) y OrigenExternoId mide 60: allá va su hash, acá el id para consultarla.</summary>
    public string?   OutlookImportadoEventId { get; private set; }
    /// <summary>Id de la cita común a todos los calendarios donde aparece. Se guarda también al
    /// enviar desde el portal: así la sincronización reconoce las reuniones propias del portal.</summary>
    public string?   OutlookICalUId          { get; private set; }
    /// <summary>Dueño del calendario del que vino. null = no es importada.</summary>
    public Guid?     OutlookImportadaPorId   { get; private set; }
    /// <summary>lastModifiedDateTime de la cita la última vez que cambió algo en el portal (UTC).</summary>
    public DateTime? OutlookModificadaEl     { get; private set; }
    /// <summary>Cuándo se vio cancelada o borrada en Outlook (UTC). La reunión no se borra.</summary>
    public DateTime? CanceladaEnOutlookEl    { get; private set; }

    public bool EsImportadaDeOutlook => OutlookImportadaPorId is not null;

    /// <summary>«outlook:» + SHA-256 en base64url del id: 51 caracteres, cabe en OrigenExternoId
    /// y en su índice único, que es lo que impide importar dos veces la misma cita.</summary>
    public static string ClaveOrigenOutlook(string eventId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(eventId));
        return "outlook:" + Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Crea la copia privada de una cita de Outlook. Sin evento de dominio: importar no
    /// es «alguien creó una reunión» y no debe avisarle a nadie.</summary>
    public static Reunion ImportarDeOutlook(string eventId, GeneralesOutlook g, Guid usuarioId,
        string? institucionId, string? areaId, string? unidadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        if (usuarioId == Guid.Empty) throw new ArgumentException("La cita necesita dueño.", nameof(usuarioId));

        var r = new Reunion
        {
            OrigenExternoId         = ClaveOrigenOutlook(eventId),
            OutlookImportadoEventId = eventId,
            OutlookImportadaPorId   = usuarioId,
            CreadoPorId             = usuarioId,
            Visibilidad             = VisibilidadReunion.Privada,
            InstitucionDuenaId      = institucionId,
            AreaId                  = areaId,
            UnidadId                = unidadId,
            Modalidad               = string.IsNullOrWhiteSpace(g.EnlaceTeams) ? "Presencial" : "Virtual",
            OutlookICalUId          = g.ICalUId,
            OutlookOrganizadorId    = g.EsOrganizador ? usuarioId : null,
        };
        r.AplicarGenerales(g);
        return r;
    }

    /// <summary>Aplica lo que Outlook manda. Devuelve false si todo ya estaba igual, para no
    /// escribir (ni auditar) cada 15 minutos una reunión que no cambió.</summary>
    public bool ActualizarDeOutlook(GeneralesOutlook g)
    {
        if (!EsImportadaDeOutlook)
            throw new DomainException("Solo una reunión que vino de Outlook se actualiza desde Outlook.");

        var igual = Titulo == g.Titulo && Fecha == g.Fecha && Hora == g.Hora && Duracion == g.Duracion
                    && Lugar == g.Lugar && OutlookEnlaceTeams == g.EnlaceTeams;
        if (igual) return false;

        AplicarGenerales(g);
        return true;
    }

    private void AplicarGenerales(GeneralesOutlook g)
    {
        Titulo              = g.Titulo;
        Fecha               = g.Fecha;
        Hora                = g.Hora;
        Duracion            = g.Duracion;
        Lugar               = g.Lugar;
        OutlookEnlaceTeams  = g.EnlaceTeams;
        OutlookModificadaEl = g.ModificadaElUtc;
    }

    public bool MarcarCanceladaEnOutlook(DateTime utcAhora)
    {
        if (CanceladaEnOutlookEl is not null) return false;
        CanceladaEnOutlookEl = utcAhora;
        return true;
    }

    public bool QuitarCancelacionOutlook()
    {
        if (CanceladaEnOutlookEl is null) return false;
        CanceladaEnOutlookEl = null;
        return true;
    }

    /// <summary>
    /// Los invitados de la cita como pre-registrados. Outlook manda solo sobre los que trajo
    /// (DeOutlook): agrega los nuevos y quita los retirados que siguen pendientes. Nunca toca un
    /// auto-registrado (QR), uno con la asistencia marcada ni uno agregado a mano. No duplica por
    /// correo. Devuelve true si agregó o quitó alguno.
    /// </summary>
    public bool SincronizarInvitadosOutlook(IReadOnlyCollection<InvitadoImportado> invitados)
    {
        static string Clave(string correo) => correo.Trim().ToLowerInvariant();

        var porCorreo = new Dictionary<string, InvitadoImportado>();
        foreach (var i in invitados)
            if (!string.IsNullOrWhiteSpace(i.Correo)) porCorreo.TryAdd(Clave(i.Correo), i);

        var cambio = false;
        foreach (var a in _asistentes.Where(a => a.DeOutlook && a.Confirmado is null && !a.AutoRegistro
                                                 && (a.Correo is null || !porCorreo.ContainsKey(Clave(a.Correo)))).ToList())
        {
            _asistentes.Remove(a);
            cambio = true;
        }

        var yaEstan = _asistentes.Where(a => !string.IsNullOrWhiteSpace(a.Correo)).Select(a => Clave(a.Correo!)).ToHashSet();
        foreach (var (clave, i) in porCorreo)
        {
            if (yaEstan.Contains(clave)) continue;
            _asistentes.Add(new Asistente { Nombre = i.Nombre, Correo = i.Correo.Trim(), EsPreregistro = true, DeOutlook = true });
            cambio = true;
        }
        return cambio;
    }

    /// <summary>Lo llama el envío desde el portal con el iCalUId que devuelve Graph.</summary>
    public void RecordarICalUId(string? iCalUId)
    {
        if (!string.IsNullOrWhiteSpace(iCalUId)) OutlookICalUId = iCalUId;
    }
```

Y al inicio de `PrepararEnvioOutlook(bool conTeams)`, antes de `if (EstaEnOutlook)`:

```csharp
        if (EsImportadaDeOutlook)
            throw new DomainException("Esta reunión viene de su Outlook: ya está en su calendario.");
```

En la clase `Asistente` (mismo archivo), después de `public bool? Confirmado { get; set; }`:

```csharp
    /// <summary>true = llegó como invitado de la cita de Outlook (spec 2026-10-29). Solo a estos
    /// los puede quitar la sincronización, y solo mientras sigan pendientes.</summary>
    public bool  DeOutlook     { get; set; }
```

Si `Reunion.cs` no tiene `using Diger.TramitesEstado.Domain.Enums;`, agregarlo (o confirmar que `VisibilidadReunion` ya resuelve por `global using`).

- [ ] **Step 5: Correr y ver que pasa**

Run: `dotnet test tests\Domain.Tests --filter "FullyQualifiedName~ReunionCalendarioOutlook|FullyQualifiedName~ReunionOutlook"`
Expected: PASS (las 8 nuevas y las existentes de `ReunionOutlookTests`).

- [ ] **Step 6: Commit**

```bash
git add src/Domain/Entities/GeneralesOutlook.cs src/Domain/Entities/Reunion.cs tests/Domain.Tests/ReunionCalendarioOutlookTests.cs
git commit -m "Dominio: la reunion que entra desde el calendario de Outlook, con sus invitados"
```

---

### Task 2: Base de datos — columnas, migración y script

**Files:**
- Modify: `src/Infrastructure/Persistence/AppDbContext.cs` (configuración de `Reunion`, junto a `b.Ignore(x => x.EstaEnOutlook);`, y la de `Asistente`, junto a `b.Property(x => x.EsPreregistro).HasDefaultValue(false);`)
- Create: `src/Infrastructure/Persistence/Migrations/<timestamp>_CalendarioOutlookAlPortal.cs` (+ Designer, + snapshot actualizado) — generados
- Create: `database/2026-10-29_poner_al_dia.sql`

**Interfaces:**
- Consumes: las cinco propiedades de `Reunion` y `Asistente.DeOutlook` de la Task 1.
- Produces: columnas `Reuniones.OutlookImportadoEventId nvarchar(400)`, `OutlookICalUId nvarchar(400)`, `OutlookImportadaPorId uniqueidentifier`, `OutlookModificadaEl datetime2`, `CanceladaEnOutlookEl datetime2`, todas NULL; `Asistentes.DeOutlook bit NOT NULL DEFAULT 0`; índices `IX_Reuniones_OutlookImportadaPorId` e `IX_Reuniones_OutlookICalUId`, filtrados `IS NOT NULL`.

- [ ] **Step 1: Configurar EF** — después de `b.Ignore(x => x.EstaEnOutlook);`:

```csharp
        // Calendario de Outlook al portal (spec 2026-10-29).
        b.Property(x => x.OutlookImportadoEventId).HasMaxLength(400);
        b.Property(x => x.OutlookICalUId).HasMaxLength(400);
        b.HasIndex(x => x.OutlookImportadaPorId).HasFilter("[OutlookImportadaPorId] IS NOT NULL");
        b.HasIndex(x => x.OutlookICalUId).HasFilter("[OutlookICalUId] IS NOT NULL");
        b.Ignore(x => x.EsImportadaDeOutlook);
```

Y en la configuración de `Asistente`, después de `b.Property(x => x.EsPreregistro).HasDefaultValue(false);`:

```csharp
        b.Property(x => x.DeOutlook).HasDefaultValue(false);
```

- [ ] **Step 2: Generar la migración** (nunca editar el snapshot a mano)

Run: `dotnet ef migrations add CalendarioOutlookAlPortal --project src\Infrastructure --startup-project src\Web --output-dir Persistence\Migrations`
Expected: crea `src/Infrastructure/Persistence/Migrations/<timestamp>_CalendarioOutlookAlPortal.cs`. Abrirlo y confirmar que `Up` solo agrega las cinco columnas de `Reuniones`, `Asistentes.DeOutlook` y los dos índices de arriba. Si trae cualquier otra cosa, el modelo tenía cambios ajenos: `dotnet ef migrations remove …` (mismos parámetros) y volver a la Task 0.

- [ ] **Step 3: Escribir `database/2026-10-29_poner_al_dia.sql`**, reemplazando `<MigrationId>` por el nombre del archivo generado sin `.cs` (p. ej. `20261029190000_CalendarioOutlookAlPortal`):

```sql
/* ═══════════════════════════════════════════════════════════════════════════════════════
   Poner al día la base con los cambios al 2026-10-29
   ───────────────────────────────────────────────────────────────────────────────────────
   Va DESPUÉS de 2026-10-27_poner_al_dia.sql. Corra antes 00_diagnostico.sql.

   Qué trae: el calendario de Outlook entra al portal (spec 2026-10-29). Cinco columnas en
   Reuniones, Asistentes.DeOutlook (los invitados de la cita entran como pre-registrados) y
   dos índices, todos de la migración EF <MigrationId>, que se registra al final en
   __EFMigrationsHistory. No hay nada fuera de EF.

   Cada paso comprueba SU condición contra el catálogo; la fila del historial va al final.
   Se puede correr las veces que haga falta: la segunda no hace nada.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-29_poner_al_dia.sql
   ═══════════════════════════════════════════════════════════════════════════════════════ */

SET QUOTED_IDENTIFIER ON;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

PRINT '── 0. Comprobación previa ──────────────────────────────────────────';
IF OBJECT_ID(N'[Reuniones]', 'U') IS NULL
   OR OBJECT_ID(N'[__EFMigrationsHistory]', 'U') IS NULL
   OR COL_LENGTH('Reuniones', 'OutlookEventId') IS NULL
    THROW 50000,
        'Falta Reuniones, __EFMigrationsHistory o Reuniones.OutlookEventId. Corra 00_diagnostico.sql: a esta base le falta 2026-10-27_poner_al_dia.sql o un script anterior.',
        1;

PRINT '── 1. Columnas ─────────────────────────────────────────────────────';
IF COL_LENGTH('Reuniones', 'OutlookImportadoEventId') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookImportadoEventId] nvarchar(400) NULL; PRINT '  OutlookImportadoEventId ... agregada'; END
ELSE PRINT '  OutlookImportadoEventId ... ya estaba';

IF COL_LENGTH('Reuniones', 'OutlookICalUId') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookICalUId] nvarchar(400) NULL; PRINT '  OutlookICalUId .......... agregada'; END
ELSE PRINT '  OutlookICalUId .......... ya estaba';

IF COL_LENGTH('Reuniones', 'OutlookImportadaPorId') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookImportadaPorId] uniqueidentifier NULL; PRINT '  OutlookImportadaPorId ... agregada'; END
ELSE PRINT '  OutlookImportadaPorId ... ya estaba';

IF COL_LENGTH('Reuniones', 'OutlookModificadaEl') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookModificadaEl] datetime2 NULL; PRINT '  OutlookModificadaEl ..... agregada'; END
ELSE PRINT '  OutlookModificadaEl ..... ya estaba';

IF COL_LENGTH('Reuniones', 'CanceladaEnOutlookEl') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [CanceladaEnOutlookEl] datetime2 NULL; PRINT '  CanceladaEnOutlookEl .... agregada'; END
ELSE PRINT '  CanceladaEnOutlookEl .... ya estaba';

IF COL_LENGTH('Asistentes', 'DeOutlook') IS NULL
BEGIN ALTER TABLE [Asistentes] ADD [DeOutlook] bit NOT NULL CONSTRAINT [DF_Asistentes_DeOutlook] DEFAULT CAST(0 AS bit); PRINT '  Asistentes.DeOutlook .... agregada'; END
ELSE PRINT '  Asistentes.DeOutlook .... ya estaba';
GO

PRINT '── 2. Índices ──────────────────────────────────────────────────────';
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[Reuniones]') AND name = N'IX_Reuniones_OutlookImportadaPorId')
BEGIN
    CREATE INDEX [IX_Reuniones_OutlookImportadaPorId] ON [Reuniones] ([OutlookImportadaPorId]) WHERE [OutlookImportadaPorId] IS NOT NULL;
    PRINT '  IX_Reuniones_OutlookImportadaPorId ... creado';
END
ELSE PRINT '  IX_Reuniones_OutlookImportadaPorId ... ya estaba';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[Reuniones]') AND name = N'IX_Reuniones_OutlookICalUId')
BEGIN
    CREATE INDEX [IX_Reuniones_OutlookICalUId] ON [Reuniones] ([OutlookICalUId]) WHERE [OutlookICalUId] IS NOT NULL;
    PRINT '  IX_Reuniones_OutlookICalUId .......... creado';
END
ELSE PRINT '  IX_Reuniones_OutlookICalUId .......... ya estaba';

PRINT '── 3. Historial de EF ──────────────────────────────────────────────';
IF COL_LENGTH('Reuniones', 'CanceladaEnOutlookEl') IS NOT NULL
   AND COL_LENGTH('Asistentes', 'DeOutlook') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[Reuniones]') AND name = N'IX_Reuniones_OutlookICalUId')
   AND NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'<MigrationId>')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES (N'<MigrationId>', N'9.0.0');
    PRINT '  CalendarioOutlookAlPortal ... registrada';
END
ELSE PRINT '  CalendarioOutlookAlPortal ... ya estaba';

COMMIT TRANSACTION;

PRINT '── Verificación: todo debe decir «presente» ────────────────────────';
SELECT 'Reuniones.OutlookImportadoEventId' AS Control, CASE WHEN COL_LENGTH('Reuniones','OutlookImportadoEventId') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL SELECT 'Reuniones.OutlookICalUId',        CASE WHEN COL_LENGTH('Reuniones','OutlookICalUId')        IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT 'Reuniones.OutlookImportadaPorId', CASE WHEN COL_LENGTH('Reuniones','OutlookImportadaPorId') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT 'Reuniones.OutlookModificadaEl',   CASE WHEN COL_LENGTH('Reuniones','OutlookModificadaEl')   IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT 'Reuniones.CanceladaEnOutlookEl',  CASE WHEN COL_LENGTH('Reuniones','CanceladaEnOutlookEl')  IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT 'Asistentes.DeOutlook',            CASE WHEN COL_LENGTH('Asistentes','DeOutlook')            IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT 'IX_Reuniones_OutlookImportadaPorId', CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[Reuniones]') AND name = N'IX_Reuniones_OutlookImportadaPorId') THEN 'presente' ELSE 'FALTA' END
UNION ALL SELECT 'IX_Reuniones_OutlookICalUId',     CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[Reuniones]') AND name = N'IX_Reuniones_OutlookICalUId') THEN 'presente' ELSE 'FALTA' END
UNION ALL SELECT 'Historial <MigrationId>',         CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'<MigrationId>') THEN 'presente' ELSE 'FALTA' END;
```

Nota: el `GO` tras el paso 1 corta la transacción en dos lotes; con `-b` y `XACT_ABORT ON` igual se detiene al primer error. El índice necesita la columna ya creada en un lote anterior.

- [ ] **Step 4: Probar el script dos veces contra la base local**

Run: `sqlcmd -S "localhost\SQL2025" -d GestionGD_new -E -C -b -I -f 65001 -i database\2026-10-29_poner_al_dia.sql`
Expected: primera vez "agregada"/"creado"/"registrada"; nueve filas `presente`. Segunda vez: todo "ya estaba", nueve `presente`.

Si la migración generada nombra el default de otra forma que `DF_Asistentes_DeOutlook`, no importa: el script comprueba la columna, no el nombre de la restricción.

- [ ] **Step 5: Compilar**

Run: `dotnet build Diger.TramitesEstado.sln`
Expected: `0 Errores`.

- [ ] **Step 6: Commit**

```bash
git add src/Infrastructure/Persistence/AppDbContext.cs src/Infrastructure/Persistence/Migrations database/2026-10-29_poner_al_dia.sql
git add src/Infrastructure/Migrations/AppDbContextModelSnapshot.cs
git commit -m "Base: columnas del calendario de Outlook en Reuniones, migracion y script"
```

(El snapshot vive en `src/Infrastructure/Migrations/`; `git status` dice cuál cambió.)

---

### Task 3: Application — la cita, el puerto y el traductor

**Files:**
- Create: `src/Application/Reuniones/Outlook/Calendario/CitaOutlook.cs`
- Create: `src/Application/Reuniones/Outlook/Calendario/CitaOutlookTraductor.cs`
- Test: `tests/Application.Tests/Outlook/CitaOutlookTraductorTests.cs`

**Interfaces:**
- Consumes: `GeneralesOutlook` (Task 1).
- Produces:
  - `record CitaOutlook(string Id, string? ICalUId, string? Asunto, DateTime Inicio, DateTime Fin, bool TodoElDia, string? Ubicacion, string? EnlaceTeams, bool EsOrganizador, bool Cancelada, DateTime? ModificadaElUtc, IReadOnlyList<InvitadoCita>? Invitados = null)` — `Inicio`/`Fin` en hora de Honduras, `Kind = Unspecified`.
  - `record InvitadoCita(string? Nombre, string? Correo, bool EsRecurso)` — tal como viene de Graph.
  - `interface ICalendarioOutlookLectura { Task<IReadOnlyList<CitaOutlook>> ListarAsync(Guid usuarioId, DateOnly desde, DateOnly hasta, CancellationToken ct); Task<CitaOutlook?> ObtenerAsync(Guid usuarioId, string eventId, CancellationToken ct); }`
  - `static GeneralesOutlook CitaOutlookTraductor.Traducir(CitaOutlook c)`
  - `static IReadOnlyList<InvitadoImportado> CitaOutlookTraductor.Invitados(CitaOutlook c, string? correoDueno)`

- [ ] **Step 1: Pruebas que fallan**

```csharp
using Diger.TramitesEstado.Application.Reuniones.Outlook.Calendario;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Outlook;

public class CitaOutlookTraductorTests
{
    private static CitaOutlook Cita(string? asunto = "Mesa técnica", bool todoElDia = false, string? ubicacion = " Sala 2 ",
        string? teams = null, int minutos = 90) =>
        new("AAMk-1", "ical-1", asunto, new DateTime(2026, 11, 3, 9, 5, 0), new DateTime(2026, 11, 3, 9, 5, 0).AddMinutes(minutos),
            todoElDia, ubicacion, teams, EsOrganizador: true, Cancelada: false,
            ModificadaElUtc: new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Traduce_fecha_hora_duracion_y_lugar()
    {
        var g = CitaOutlookTraductor.Traducir(Cita());

        g.Titulo.Should().Be("Mesa técnica");
        g.Fecha.Should().Be(new DateOnly(2026, 11, 3));
        g.Hora.Should().Be("09:05");
        g.Duracion.Should().Be("1:30", "HorarioReunion lee «H:mm»");
        g.Lugar.Should().Be("Sala 2");
        g.EnlaceTeams.Should().BeNull();
        g.EsOrganizador.Should().BeTrue();
        g.ICalUId.Should().Be("ical-1");
    }

    [Fact]
    public void Todo_el_dia_no_lleva_hora_ni_duracion()
    {
        var g = CitaOutlookTraductor.Traducir(Cita(todoElDia: true, minutos: 24 * 60));

        g.Hora.Should().BeNull();
        g.Duracion.Should().BeNull();
    }

    [Fact]
    public void Asunto_vacio_y_textos_largos_se_ajustan_a_la_base()
    {
        CitaOutlookTraductor.Traducir(Cita(asunto: "  ")).Titulo.Should().Be("(Sin asunto)");
        CitaOutlookTraductor.Traducir(Cita(asunto: new string('x', 300))).Titulo.Should().HaveLength(250);
        CitaOutlookTraductor.Traducir(Cita(ubicacion: new string('y', 300))).Lugar.Should().HaveLength(250);
        CitaOutlookTraductor.Traducir(Cita(ubicacion: "")).Lugar.Should().BeNull();
    }

    [Fact]
    public void Invitados_sin_recursos_ni_el_dueno_y_con_nombre_de_respaldo()
    {
        var c = Cita() with
        {
            Invitados =
            [
                new("Marta López", "marta@copeco.gob.hn", false),
                new(null, "  luis@ihtt.gob.hn ", false),
                new("Sala 2", "sala2@diger.gob.hn", true),
                new("Henry", "HCARDONA@diger.gob.hn", false),
                new("Sin correo", null, false),
                new(new string('n', 200), "largo@x.hn", false),
            ]
        };

        var inv = CitaOutlookTraductor.Invitados(c, "hcardona@diger.gob.hn");

        inv.Select(i => i.Correo).Should().Equal("marta@copeco.gob.hn", "luis@ihtt.gob.hn", "largo@x.hn");
        inv[1].Nombre.Should().Be("luis@ihtt.gob.hn", "sin nombre, se usa el correo");
        inv[2].Nombre.Should().HaveLength(150);
    }

    [Fact]
    public void Sin_invitados_es_lista_vacia()
    {
        CitaOutlookTraductor.Invitados(Cita(), "x@diger.gob.hn").Should().BeEmpty();
    }

    [Fact]
    public void El_enlace_de_Teams_pasa_tal_cual()
    {
        CitaOutlookTraductor.Traducir(Cita(teams: "https://teams.microsoft.com/l/x")).EnlaceTeams
            .Should().Be("https://teams.microsoft.com/l/x");
    }
}
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~CitaOutlookTraductor"`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `CitaOutlook.cs`**

```csharp
namespace Diger.TramitesEstado.Application.Reuniones.Outlook.Calendario;

/// <summary>Una cita del calendario de Outlook tal como la entrega Graph, sin tipos de Graph.
/// Inicio y Fin en hora de Honduras (Kind = Unspecified): Graph las manda ya en esa zona.</summary>
public sealed record CitaOutlook(
    string Id, string? ICalUId, string? Asunto, DateTime Inicio, DateTime Fin, bool TodoElDia,
    string? Ubicacion, string? EnlaceTeams, bool EsOrganizador, bool Cancelada, DateTime? ModificadaElUtc,
    IReadOnlyList<InvitadoCita>? Invitados = null);

/// <summary>Un invitado tal como lo trae Graph. <see cref="EsRecurso"/> = sala o equipo
/// (attendee type «resource»): no es una persona y no se pre-registra.</summary>
public sealed record InvitadoCita(string? Nombre, string? Correo, bool EsRecurso);

/// <summary>
/// Lectura del calendario de un usuario con SU conexión. Los fallos llegan como
/// <see cref="OutlookConexionVencidaException"/> o <see cref="OutlookNoDisponibleException"/>.
/// </summary>
public interface ICalendarioOutlookLectura
{
    /// <summary>Citas entre las 00:00 de <paramref name="desde"/> y las 23:59 de <paramref name="hasta"/>,
    /// con las recurrencias ya expandidas.</summary>
    Task<IReadOnlyList<CitaOutlook>> ListarAsync(Guid usuarioId, DateOnly desde, DateOnly hasta, CancellationToken ct);

    /// <summary>Una cita por su id, o null si ya no existe (borrada en Outlook).</summary>
    Task<CitaOutlook?> ObtenerAsync(Guid usuarioId, string eventId, CancellationToken ct);
}
```

- [ ] **Step 4: Crear `CitaOutlookTraductor.cs`**

```csharp
using System.Globalization;

namespace Diger.TramitesEstado.Application.Reuniones.Outlook.Calendario;

/// <summary>De cita de Outlook a los Generales que Outlook manda sobre la reunión. Puro: sin base,
/// sin reloj. Recorta a los largos de columna (Titulo y Lugar, 250) en vez de dejar que falle el guardado.</summary>
public static class CitaOutlookTraductor
{
    private const int LargoTitulo = 250;
    private const int LargoLugar  = 250;
    private const int LargoNombre = 150;   // Asistentes.Nombre
    private const int LargoCorreo = 200;   // Asistentes.Correo

    /// <summary>Los invitados que se pre-registran: sin recursos, sin el dueño del calendario
    /// (<paramref name="correoDueno"/>, su Upn) y sin los que no traen correo. El orden es el de Outlook.</summary>
    public static IReadOnlyList<InvitadoImportado> Invitados(CitaOutlook c, string? correoDueno)
    {
        var dueno = correoDueno?.Trim();
        return (c.Invitados ?? [])
            .Where(i => !i.EsRecurso && !string.IsNullOrWhiteSpace(i.Correo))
            .Select(i => (Nombre: i.Nombre?.Trim(), Correo: i.Correo!.Trim()))
            .Where(i => !string.Equals(i.Correo, dueno, StringComparison.OrdinalIgnoreCase))
            .Select(i => new InvitadoImportado(
                Recortar(string.IsNullOrWhiteSpace(i.Nombre) ? i.Correo : i.Nombre, LargoNombre),
                Recortar(i.Correo, LargoCorreo)))
            .ToList();
    }

    public static GeneralesOutlook Traducir(CitaOutlook c)
    {
        var titulo = string.IsNullOrWhiteSpace(c.Asunto) ? "(Sin asunto)" : Recortar(c.Asunto.Trim(), LargoTitulo);
        var lugar  = string.IsNullOrWhiteSpace(c.Ubicacion) ? null : Recortar(c.Ubicacion.Trim(), LargoLugar);
        var teams  = string.IsNullOrWhiteSpace(c.EnlaceTeams) ? null : c.EnlaceTeams.Trim();

        return new GeneralesOutlook(
            Titulo:          titulo,
            Fecha:           DateOnly.FromDateTime(c.Inicio),
            Hora:            c.TodoElDia ? null : c.Inicio.ToString("HH:mm", CultureInfo.InvariantCulture),
            Duracion:        c.TodoElDia ? null : Duracion(c.Fin - c.Inicio),
            Lugar:           lugar,
            EnlaceTeams:     teams,
            EsOrganizador:   c.EsOrganizador,
            ICalUId:         c.ICalUId,
            ModificadaElUtc: c.ModificadaElUtc);
    }

    /// <summary>«H:mm», el formato que HorarioReunion lee sin ambigüedad.</summary>
    private static string? Duracion(TimeSpan d)
    {
        var minutos = (int)Math.Round(d.TotalMinutes);
        return minutos <= 0 ? null : $"{minutos / 60}:{minutos % 60:00}";
    }

    private static string Recortar(string s, int largo) => s.Length <= largo ? s : s[..largo];
}
```

`GeneralesOutlook` vive en `Diger.TramitesEstado.Domain.Entities`; si Application no lo tiene en `global using`, agregar `using Diger.TramitesEstado.Domain.Entities;` en los dos archivos.

- [ ] **Step 5: Correr y ver que pasa**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~CitaOutlookTraductor"`
Expected: PASS (6).

- [ ] **Step 6: Commit**

```bash
git add src/Application/Reuniones/Outlook/Calendario tests/Application.Tests/Outlook/CitaOutlookTraductorTests.cs
git commit -m "Application: la cita de Outlook, el puerto de lectura y su traductor"
```

---

### Task 4: Application — `SincronizarCalendarioOutlookCommand`

**Files:**
- Create: `src/Application/Reuniones/Outlook/Calendario/SincronizarCalendarioOutlookCommand.cs`
- Test: `tests/Application.Tests/Outlook/SincronizarCalendarioOutlookTests.cs`

**Interfaces:**
- Consumes: `ICalendarioOutlookLectura`, `CitaOutlook`, `InvitadoCita`, `CitaOutlookTraductor.Traducir` e `Invitados` (Task 3); `ConexionMicrosoft.Upn`; `Reunion.ImportarDeOutlook`, `ActualizarDeOutlook`, `SincronizarInvitadosOutlook`, `MarcarCanceladaEnOutlook`, `QuitarCancelacionOutlook`, `ClaveOrigenOutlook` (Task 1); `HoraHonduras.Ahora(TimeProvider)`.
- Produces:
  - `record SincronizarCalendarioOutlookCommand(Guid UsuarioId) : IRequest<ResultadoSincronizacionCalendario>`
  - `record ResultadoSincronizacionCalendario(int Creadas, int Actualizadas, int Canceladas, int Saltadas)`
  - `const int SincronizarCalendarioOutlookCommandHandler.VentanaDias = 90`

- [ ] **Step 1: Pruebas que fallan**

```csharp
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Reuniones.Outlook.Calendario;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Outlook;

/// <summary>Las reglas del spec 2026-10-29 sobre la base en memoria y un calendario simulado.
/// «Hoy» es el 2026-11-02 en Honduras (18:00 UTC = 12:00 local).</summary>
public class SincronizarCalendarioOutlookTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly ICalendarioOutlookLectura _cal = Substitute.For<ICalendarioOutlookLectura>();
    private readonly FakeTimeProvider _reloj = new(new DateTimeOffset(2026, 11, 2, 18, 0, 0, TimeSpan.Zero));
    private readonly Usuario _ana = Usuario.Crear("Ana", "ana@diger.gob.hn", "hash");
    private static readonly DateOnly Hoy = new(2026, 11, 2);

    public SincronizarCalendarioOutlookTests()
    {
        var usuario = Substitute.For<ICurrentUserService>();
        usuario.EsGlobal.Returns(true);
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _ctx = new AppDbContext(opts, usuario, Substitute.For<MediatR.IPublisher>());
        _ctx.Usuarios.Add(_ana);
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(_ana.Id, "DIGER", "AREA1", "UNI1", "Empleado"));
        _ctx.ConexionesMicrosoft.Add(ConexionMicrosoft.Crear(_ana.Id, "ana@diger.gob.hn", "h", [1], DateTime.UtcNow));
        _ctx.SaveChanges();
    }

    public void Dispose() => _ctx.Dispose();

    private static CitaOutlook Cita(string id, string titulo = "Mesa", int dias = 1, string? ical = null,
        bool cancelada = false, string hora = "09:00", params string[] invitados) =>
        new(id, ical ?? "ical-" + id, titulo, Hoy.AddDays(dias).ToDateTime(TimeOnly.Parse(hora)),
            Hoy.AddDays(dias).ToDateTime(TimeOnly.Parse(hora)).AddHours(1), false, "Sala", null, false, cancelada,
            new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            invitados.Select(c => new InvitadoCita(null, c, false)).ToList());

    private void Calendario(params CitaOutlook[] citas) =>
        _cal.ListarAsync(_ana.Id, Hoy, Hoy.AddDays(90), Arg.Any<CancellationToken>()).Returns(citas);

    private Task<ResultadoSincronizacionCalendario> Sincronizar() =>
        new SincronizarCalendarioOutlookCommandHandler(_ctx, _cal, _ctx, _reloj)
            .Handle(new SincronizarCalendarioOutlookCommand(_ana.Id), default);

    private Task<List<Reunion>> Importadas() =>
        _ctx.Reuniones.IgnoreQueryFilters().Include(r => r.Asistentes)
            .Where(r => r.OutlookImportadaPorId == _ana.Id).ToListAsync();

    [Fact]
    public async Task Crea_privadas_con_la_asignacion_del_usuario_y_no_duplica()
    {
        Calendario(Cita("e1"), Cita("e2"));

        (await Sincronizar()).Should().Be(new ResultadoSincronizacionCalendario(2, 0, 0, 0));
        (await Sincronizar()).Should().Be(new ResultadoSincronizacionCalendario(0, 0, 0, 0));

        var rs = await Importadas();
        rs.Should().HaveCount(2);
        rs.Should().OnlyContain(r => r.Visibilidad == VisibilidadReunion.Privada && r.CreadoPorId == _ana.Id
                                     && r.InstitucionDuenaId == "DIGER" && r.AreaId == "AREA1" && r.UnidadId == "UNI1");
    }

    [Fact]
    public async Task Actualiza_generales_y_nunca_toca_lo_del_portal()
    {
        Calendario(Cita("e1"));
        await Sincronizar();
        var r = (await Importadas()).Single();
        r.ObjetivoAgenda = "Lo escribió Ana";
        await _ctx.SaveChangesAsync();

        Calendario(Cita("e1", titulo: "Mesa (movida)", hora: "15:00"));
        (await Sincronizar()).Actualizadas.Should().Be(1);

        r = (await Importadas()).Single();
        r.Titulo.Should().Be("Mesa (movida)");
        r.Hora.Should().Be("15:00");
        r.ObjetivoAgenda.Should().Be("Lo escribió Ana");
    }

    [Fact]
    public async Task Salta_las_reuniones_que_el_portal_envio_por_iCalUId_y_por_eventId()
    {
        var delPortal = Reunion.Crear("Del portal");
        delPortal.RegistrarEnvioOutlook("evento-del-portal", _ana.Id, null, "huella", DateTime.UtcNow);
        delPortal.RecordarICalUId("ical-compartido");
        _ctx.Reuniones.Add(delPortal);
        await _ctx.SaveChangesAsync();

        Calendario(Cita("otro-buzon", ical: "ical-compartido"), Cita("evento-del-portal", ical: "sin-registro"));

        (await Sincronizar()).Should().Be(new ResultadoSincronizacionCalendario(0, 0, 0, 2));
        (await Importadas()).Should().BeEmpty();
    }

    [Fact]
    public async Task Cancelada_en_Outlook_se_marca_y_no_se_borra()
    {
        Calendario(Cita("e1"));
        await Sincronizar();

        Calendario(Cita("e1", cancelada: true));
        (await Sincronizar()).Canceladas.Should().Be(1);

        var r = (await Importadas()).Single();
        r.CanceladaEnOutlookEl.Should().NotBeNull();
        r.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Desaparecida_y_borrada_en_Outlook_se_marca_cancelada()
    {
        Calendario(Cita("e1"));
        await Sincronizar();
        Calendario();
        _cal.ObtenerAsync(_ana.Id, "e1", Arg.Any<CancellationToken>()).Returns((CitaOutlook?)null);

        (await Sincronizar()).Canceladas.Should().Be(1);
        (await Importadas()).Single().CanceladaEnOutlookEl.Should().NotBeNull();
    }

    [Fact]
    public async Task Desaparecida_porque_se_movio_mas_alla_de_la_ventana_solo_cambia_de_fecha()
    {
        Calendario(Cita("e1"));
        await Sincronizar();
        Calendario();
        _cal.ObtenerAsync(_ana.Id, "e1", Arg.Any<CancellationToken>()).Returns(Cita("e1", dias: 120));

        var res = await Sincronizar();

        res.Canceladas.Should().Be(0);
        res.Actualizadas.Should().Be(1);
        var r = (await Importadas()).Single();
        r.CanceladaEnOutlookEl.Should().BeNull();
        r.Fecha.Should().Be(Hoy.AddDays(120));
    }

    [Fact]
    public async Task Las_de_fecha_pasada_se_congelan()
    {
        var pasada = Reunion.ImportarDeOutlook("vieja", CitaOutlookTraductor.Traducir(Cita("vieja", dias: -3)), _ana.Id, "DIGER", null, null);
        _ctx.Reuniones.Add(pasada);
        await _ctx.SaveChangesAsync();
        Calendario();

        (await Sincronizar()).Should().Be(new ResultadoSincronizacionCalendario(0, 0, 0, 0));
        await _cal.DidNotReceive().ObtenerAsync(Arg.Any<Guid>(), "vieja", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reaparecida_quita_la_cancelacion()
    {
        Calendario(Cita("e1"));
        await Sincronizar();
        Calendario(Cita("e1", cancelada: true));
        await Sincronizar();

        Calendario(Cita("e1"));
        (await Sincronizar()).Actualizadas.Should().Be(1);

        (await Importadas()).Single().CanceladaEnOutlookEl.Should().BeNull();
    }

    [Fact]
    public async Task Los_invitados_entran_preregistrados_sin_la_duena_del_calendario()
    {
        Calendario(Cita("e1", invitados: ["marta@copeco.gob.hn", "ANA@diger.gob.hn"]));

        await Sincronizar();

        var a = (await Importadas()).Single().Asistentes.Should().ContainSingle().Subject;
        a.Correo.Should().Be("marta@copeco.gob.hn");
        a.EsPreregistro.Should().BeTrue();
        a.DeOutlook.Should().BeTrue();
    }

    [Fact]
    public async Task Cambio_de_invitados_actualiza_y_respeta_la_asistencia_marcada()
    {
        Calendario(Cita("e1", invitados: ["p@x.hn", "a@x.hn"]));
        await Sincronizar();
        (await Importadas()).Single().Asistentes.Single(x => x.Correo == "a@x.hn").Confirmado = true;
        await _ctx.SaveChangesAsync();

        Calendario(Cita("e1", invitados: ["n@x.hn"]));
        (await Sincronizar()).Actualizadas.Should().Be(1);

        (await Importadas()).Single().Asistentes.Select(x => x.Correo)
            .Should().BeEquivalentTo(["a@x.hn", "n@x.hn"]);
    }

    [Fact]
    public async Task Borrada_por_el_usuario_no_se_vuelve_a_crear()
    {
        Calendario(Cita("e1"));
        await Sincronizar();
        var r = (await Importadas()).Single();
        r.IsDeleted = true;
        await _ctx.SaveChangesAsync();

        (await Sincronizar()).Should().Be(new ResultadoSincronizacionCalendario(0, 0, 0, 1));
        (await Importadas()).Should().HaveCount(1);
    }
}
```

Si `FakeTimeProvider` no está disponible, agregar el paquete `Microsoft.Extensions.TimeProvider.Testing` a `tests/Application.Tests/Diger.TramitesEstado.Application.Tests.csproj` (`dotnet add tests\Application.Tests package Microsoft.Extensions.TimeProvider.Testing`). Si otra prueba del proyecto ya usa un reloj fijo propio, usar ese en su lugar.

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~SincronizarCalendarioOutlook"`
Expected: FAIL de compilación.

- [ ] **Step 3: Implementar `SincronizarCalendarioOutlookCommand.cs`**

```csharp
namespace Diger.TramitesEstado.Application.Reuniones.Outlook.Calendario;

public sealed record ResultadoSincronizacionCalendario(int Creadas, int Actualizadas, int Canceladas, int Saltadas);

/// <summary>
/// Trae el calendario de un usuario al portal (spec 2026-10-29). Lo despacha el servicio en segundo
/// plano, sin sesión: por eso todas las lecturas van con IgnoreQueryFilters y el dueño viene en el
/// comando, nunca de ICurrentUserService.
/// </summary>
public sealed record SincronizarCalendarioOutlookCommand(Guid UsuarioId) : IRequest<ResultadoSincronizacionCalendario>;

public sealed class SincronizarCalendarioOutlookCommandHandler(
    IApplicationDbContext ctx,
    ICalendarioOutlookLectura calendario,
    IUnitOfWork uow,
    TimeProvider reloj)
    : IRequestHandler<SincronizarCalendarioOutlookCommand, ResultadoSincronizacionCalendario>
{
    public const int VentanaDias = 90;

    public async Task<ResultadoSincronizacionCalendario> Handle(SincronizarCalendarioOutlookCommand cmd, CancellationToken ct)
    {
        var usuarioId = cmd.UsuarioId;
        var hoy       = DateOnly.FromDateTime(HoraHonduras.Ahora(reloj));
        var hasta     = hoy.AddDays(VentanaDias);
        var utcAhora  = reloj.GetUtcNow().UtcDateTime;

        var citas = await calendario.ListarAsync(usuarioId, hoy, hasta, ct);

        // Las reuniones que nacieron en el portal y ya están en Outlook: su cita (y la copia en el
        // calendario de cada invitado, mismo iCalUId) no se importa otra vez.
        var delPortal = await ctx.Reuniones.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.OutlookImportadaPorId == null && (r.OutlookICalUId != null || r.OutlookEventId != null))
            .Select(r => new { r.OutlookICalUId, r.OutlookEventId })
            .ToListAsync(ct);
        var icalsDelPortal   = delPortal.Where(x => x.OutlookICalUId != null).Select(x => x.OutlookICalUId!).ToHashSet();
        var eventosDelPortal = delPortal.Where(x => x.OutlookEventId != null).Select(x => x.OutlookEventId!).ToHashSet();

        // Con las borradas: una reunión que el usuario borró no debe volver a nacer. Con sus
        // asistentes: la sincronización de invitados agrega y quita sobre la colección cargada.
        var mias = await ctx.Reuniones.IgnoreQueryFilters()
            .Include(r => r.Asistentes)
            .Where(r => r.OutlookImportadaPorId == usuarioId)
            .ToListAsync(ct);

        // El dueño del calendario aparece entre los invitados de las citas a las que lo invitaron.
        var correoDueno = await ctx.ConexionesMicrosoft.AsNoTracking()
            .Where(x => x.Id == usuarioId).Select(x => x.Upn).FirstOrDefaultAsync(ct);
        var porClave = mias.ToDictionary(r => r.OrigenExternoId!);

        var asignacion = await ctx.AsignacionesUsuario.AsNoTracking()
            .Where(a => a.UsuarioId == usuarioId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new { a.InstitucionId, a.AreaId, a.UnidadId })
            .FirstOrDefaultAsync(ct);

        int creadas = 0, actualizadas = 0, canceladas = 0, saltadas = 0;
        var vistas = new HashSet<string>();

        foreach (var c in citas)
        {
            if ((c.ICalUId is not null && icalsDelPortal.Contains(c.ICalUId)) || eventosDelPortal.Contains(c.Id))
            {
                saltadas++;
                continue;
            }

            var clave = Reunion.ClaveOrigenOutlook(c.Id);
            vistas.Add(clave);

            if (porClave.TryGetValue(clave, out var r))
            {
                if (r.IsDeleted) { saltadas++; continue; }
                if (c.Cancelada)
                {
                    if (r.MarcarCanceladaEnOutlook(utcAhora)) canceladas++;
                    continue;
                }
                var quitoCancelacion = r.QuitarCancelacionOutlook();
                var cambio = r.ActualizarDeOutlook(CitaOutlookTraductor.Traducir(c));
                var cambioInvitados = r.SincronizarInvitadosOutlook(CitaOutlookTraductor.Invitados(c, correoDueno));
                if (cambio || cambioInvitados || quitoCancelacion) actualizadas++;
            }
            else
            {
                if (c.Cancelada) { saltadas++; continue; }
                var nueva = Reunion.ImportarDeOutlook(c.Id, CitaOutlookTraductor.Traducir(c), usuarioId,
                    asignacion?.InstitucionId, asignacion?.AreaId, asignacion?.UnidadId);
                nueva.SincronizarInvitadosOutlook(CitaOutlookTraductor.Invitados(c, correoDueno));
                ctx.Reuniones.Add(nueva);
                porClave[clave] = nueva;
                creadas++;
            }
        }

        // Las que estaban en la ventana y ya no vinieron: borradas en Outlook, o movidas fuera
        // de la ventana. Se pregunta por cada una; las de fecha pasada no se tocan (congeladas).
        foreach (var r in mias.Where(r => !r.IsDeleted && r.CanceladaEnOutlookEl is null
                                          && r.Fecha >= hoy && r.Fecha <= hasta
                                          && !vistas.Contains(r.OrigenExternoId!)))
        {
            var c = await calendario.ObtenerAsync(usuarioId, r.OutlookImportadoEventId!, ct);
            if (c is null || c.Cancelada)
            {
                if (r.MarcarCanceladaEnOutlook(utcAhora)) canceladas++;
            }
            else
            {
                var cambio = r.ActualizarDeOutlook(CitaOutlookTraductor.Traducir(c));
                var cambioInvitados = r.SincronizarInvitadosOutlook(CitaOutlookTraductor.Invitados(c, correoDueno));
                if (cambio || cambioInvitados) actualizadas++;
            }
        }

        if (creadas + actualizadas + canceladas > 0)
            await uow.SaveChangesAsync(ct);

        return new ResultadoSincronizacionCalendario(creadas, actualizadas, canceladas, saltadas);
    }
}
```

`HoraHonduras` está en `Diger.TramitesEstado.Application.Reuniones.Outlook` (namespace padre: resuelve sin `using`).

- [ ] **Step 4: Correr y ver que pasa**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~SincronizarCalendarioOutlook"`
Expected: PASS (11).

- [ ] **Step 5: Commit**

```bash
git add src/Application/Reuniones/Outlook/Calendario/SincronizarCalendarioOutlookCommand.cs tests/Application.Tests/Outlook/SincronizarCalendarioOutlookTests.cs tests/Application.Tests/Diger.TramitesEstado.Application.Tests.csproj
git commit -m "Application: sincronizar el calendario de Outlook de un usuario"
```

---

### Task 5: Infrastructure — leer el calendario con Graph y recordar el iCalUId al enviar

**Files:**
- Create: `src/Infrastructure/Outlook/GraphCalendarioOutlookLectura.cs`
- Modify: `src/Application/Reuniones/Outlook/IOutlookCalendario.cs` (`ResultadoOutlook`)
- Modify: `src/Infrastructure/Outlook/GraphOutlookCalendario.cs` (`LeerAsync`)
- Modify: `src/Application/Reuniones/Outlook/OutlookCommands.cs` (`EnviarReunionAOutlookCommandHandler`)
- Modify: `src/Infrastructure/DependencyInjection.cs`
- Test: `tests/Application.Tests/Outlook/GraphCalendarioOutlookLecturaTests.cs`, `tests/Application.Tests/Outlook/GraphOutlookCalendarioTests.cs`

**Interfaces:**
- Consumes: `ICalendarioOutlookLectura`, `CitaOutlook` (Task 3); `IConexionMicrosoft.ObtenerTokenAsync`; `GraphOutlookCalendario.CodigoDeErrorAsync` (ya `internal static`).
- Produces: `GraphCalendarioOutlookLectura : ICalendarioOutlookLectura`; `ResultadoOutlook(string EventId, string? EnlaceTeams, string? ICalUId = null)`.

- [ ] **Step 1: Pruebas que fallan** — `GraphCalendarioOutlookLecturaTests.cs`:

```csharp
using System.Net;
using Diger.TramitesEstado.Application.Reuniones.Outlook;
using Diger.TramitesEstado.Application.Reuniones.Outlook.Calendario;
using Diger.TramitesEstado.Infrastructure.Outlook;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Outlook;

public class GraphCalendarioOutlookLecturaTests
{
    private sealed class Graph(params (HttpStatusCode Codigo, string Cuerpo)[] respuestas) : HttpMessageHandler
    {
        private int _i;
        public readonly List<HttpRequestMessage> Pedidos = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Pedidos.Add(r);
            var (codigo, cuerpo) = respuestas[Math.Min(_i++, respuestas.Length - 1)];
            return Task.FromResult(new HttpResponseMessage(codigo) { Content = new StringContent(cuerpo) });
        }
    }

    private static readonly Guid Ana = Guid.NewGuid();

    private static (GraphCalendarioOutlookLectura l, Graph g) Armar(params (HttpStatusCode, string)[] respuestas)
    {
        var g = new Graph(respuestas);
        var conexion = Substitute.For<IConexionMicrosoft>();
        conexion.ObtenerTokenAsync(Ana, Arg.Any<CancellationToken>()).Returns("tok-cal");
        var http = new HttpClient(g) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        return (new GraphCalendarioOutlookLectura(http, conexion, NullLogger<GraphCalendarioOutlookLectura>.Instance), g);
    }

    private const string Evento = """
        {"id":"AAMk-1","iCalUId":"ical-1","subject":"Mesa","isAllDay":false,"isOrganizer":true,"isCancelled":false,
         "lastModifiedDateTime":"2026-10-29T18:00:00Z",
         "start":{"dateTime":"2026-11-03T09:30:00.0000000","timeZone":"Central America Standard Time"},
         "end":{"dateTime":"2026-11-03T10:30:00.0000000","timeZone":"Central America Standard Time"},
         "location":{"displayName":"Sala 2"},"onlineMeeting":{"joinUrl":"https://teams/x"},
         "attendees":[{"type":"required","emailAddress":{"name":"Marta","address":"marta@copeco.gob.hn"}},
                      {"type":"resource","emailAddress":{"name":"Sala 2","address":"sala2@diger.gob.hn"}}]}
        """;

    [Fact]
    public async Task Pide_la_ventana_en_hora_de_Honduras_y_sigue_las_paginas()
    {
        var (l, g) = Armar(
            (HttpStatusCode.OK, $$"""{"value":[{{Evento}}],"@odata.nextLink":"https://graph.microsoft.com/v1.0/me/calendarView?$skiptoken=abc"}"""),
            (HttpStatusCode.OK, """{"value":[]}"""));

        var citas = await l.ListarAsync(Ana, new DateOnly(2026, 11, 2), new DateOnly(2027, 1, 31), default);

        citas.Should().ContainSingle();
        var c = citas[0];
        // BeEquivalentTo y no Be: el record lleva una lista, y la igualdad de record la compara por referencia.
        c.Should().BeEquivalentTo(new CitaOutlook("AAMk-1", "ical-1", "Mesa", new DateTime(2026, 11, 3, 9, 30, 0), new DateTime(2026, 11, 3, 10, 30, 0),
            false, "Sala 2", "https://teams/x", true, false, new DateTime(2026, 10, 29, 18, 0, 0, DateTimeKind.Utc),
            [new InvitadoCita("Marta", "marta@copeco.gob.hn", false), new InvitadoCita("Sala 2", "sala2@diger.gob.hn", true)]));

        var primero = Uri.UnescapeDataString(g.Pedidos[0].RequestUri!.ToString());
        primero.Should().Contain("me/calendarView?startDateTime=2026-11-02T00:00:00-06:00&endDateTime=2027-01-31T23:59:59-06:00");
        g.Pedidos[0].Headers.GetValues("Prefer").Single().Should().Be("outlook.timezone=\"Central America Standard Time\"");
        g.Pedidos[0].Headers.Authorization!.ToString().Should().Be("Bearer tok-cal");
        g.Pedidos[1].RequestUri!.ToString().Should().EndWith("$skiptoken=abc");
    }

    [Fact]
    public async Task Obtener_devuelve_null_si_la_cita_ya_no_existe()
    {
        var (l, _) = Armar((HttpStatusCode.NotFound, """{"error":{"code":"ErrorItemNotFound"}}"""));

        (await l.ObtenerAsync(Ana, "AAMk-1", default)).Should().BeNull();
    }

    [Fact]
    public async Task Un_401_es_conexion_vencida()
    {
        var (l, _) = Armar((HttpStatusCode.Unauthorized, "{}"));

        var act = () => l.ListarAsync(Ana, new DateOnly(2026, 11, 2), new DateOnly(2027, 1, 31), default);

        await act.Should().ThrowAsync<OutlookConexionVencidaException>();
    }
}
```

Y en `GraphOutlookCalendarioTests.cs` agregar:

```csharp
    [Fact]
    public async Task Crear_devuelve_tambien_el_iCalUId()
    {
        var (cal, _) = Armar(HttpStatusCode.Created, """{"id":"AAMk-1","iCalUId":"ical-9"}""");

        (await cal.CrearAsync(Org, Evento, Guid.NewGuid(), default)).ICalUId.Should().Be("ical-9");
    }
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~GraphCalendarioOutlookLectura|FullyQualifiedName~GraphOutlookCalendario"`
Expected: FAIL de compilación.

- [ ] **Step 3: `ResultadoOutlook`** en `IOutlookCalendario.cs`:

```csharp
public sealed record ResultadoOutlook(string EventId, string? EnlaceTeams, string? ICalUId = null);
```

En `GraphOutlookCalendario.LeerAsync`, antes de `return new ResultadoOutlook(id, enlace);`:

```csharp
            string? ical = raiz.TryGetProperty("iCalUId", out var ic) && ic.ValueKind == JsonValueKind.String ? ic.GetString() : null;
            return new ResultadoOutlook(id, enlace, ical);
```

(y borrar el `return new ResultadoOutlook(id, enlace);` anterior).

En `EnviarReunionAOutlookCommandHandler.Handle`, después de `r.RegistrarEnvioOutlook(...)`:

```csharp
        r.RecordarICalUId(res.ICalUId);
```

- [ ] **Step 4: Crear `GraphCalendarioOutlookLectura.cs`**

```csharp
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Diger.TramitesEstado.Application.Reuniones.Outlook;
using Diger.TramitesEstado.Application.Reuniones.Outlook.Calendario;
using Microsoft.Extensions.Logging;

namespace Diger.TramitesEstado.Infrastructure.Outlook;

/// <summary>
/// Lee el calendario de un usuario con su token de calendario (Calendars.ReadWrite ya concedido
/// cubre la lectura). Pide las horas en la zona de Honduras para no convertir nada. Como el resto de
/// Outlook/: sin SDK de Graph, y los logs llevan estado, código y ruta, nunca cuerpo ni cabeceras.
/// La ruta de calendarView no lleva datos de la cita; la de un evento lleva su id, que no es secreto.
/// </summary>
public sealed class GraphCalendarioOutlookLectura(
    HttpClient http, IConexionMicrosoft conexion, ILogger<GraphCalendarioOutlookLectura> log) : ICalendarioOutlookLectura
{
    private const string Campos =
        "id,iCalUId,subject,start,end,isAllDay,location,onlineMeeting,isOrganizer,isCancelled,lastModifiedDateTime,attendees";
    private const string Zona = "Central America Standard Time";

    public async Task<IReadOnlyList<CitaOutlook>> ListarAsync(Guid usuarioId, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var token = await conexion.ObtenerTokenAsync(usuarioId, ct);
        var inicio = desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00:00-06:00";
        var fin    = hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T23:59:59-06:00";
        string? ruta = $"me/calendarView?startDateTime={Uri.EscapeDataString(inicio)}&endDateTime={Uri.EscapeDataString(fin)}&$select={Campos}&$top=100";

        var citas = new List<CitaOutlook>();
        while (ruta is not null)
        {
            using var resp = await EnviarAsync(token, ruta, ct)
                ?? throw new OutlookNoDisponibleException("Graph no encontró el calendario");
            using var doc = await LeerJsonAsync(resp, ct);
            if (doc.RootElement.TryGetProperty("value", out var valores) && valores.ValueKind == JsonValueKind.Array)
                foreach (var e in valores.EnumerateArray())
                    if (Leer(e) is { } c) citas.Add(c);

            // El nextLink es absoluto: HttpClient lo usa tal cual aunque tenga BaseAddress.
            ruta = doc.RootElement.TryGetProperty("@odata.nextLink", out var sig) && sig.ValueKind == JsonValueKind.String
                ? sig.GetString() : null;
        }
        return citas;
    }

    public async Task<CitaOutlook?> ObtenerAsync(Guid usuarioId, string eventId, CancellationToken ct)
    {
        var token = await conexion.ObtenerTokenAsync(usuarioId, ct);
        using var resp = await EnviarAsync(token, $"me/events/{Uri.EscapeDataString(eventId)}?$select={Campos}", ct);
        if (resp is null) return null;
        using var doc = await LeerJsonAsync(resp, ct);
        return Leer(doc.RootElement);
    }

    /// <summary>null en 404 (la cita no existe); cualquier otro fallo, excepción del puerto.</summary>
    private async Task<HttpResponseMessage?> EnviarAsync(string token, string ruta, CancellationToken ct)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Get, ruta);
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        pedido.Headers.TryAddWithoutValidation("Prefer", $"outlook.timezone=\"{Zona}\"");

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
        if (codigo == HttpStatusCode.NotFound) { resp.Dispose(); return null; }

        var codigoGraph = await GraphOutlookCalendario.CodigoDeErrorAsync(resp, ct);
        resp.Dispose();
        log.LogWarning("Graph respondió {Estado} ({Codigo}) leyendo el calendario", (int)codigo, codigoGraph ?? "sin código");
        throw codigo == HttpStatusCode.Unauthorized
            ? new OutlookConexionVencidaException("Graph rechazó el token (401)")
            : new OutlookNoDisponibleException($"Graph respondió {(int)codigo}");
    }

    private static async Task<JsonDocument> LeerJsonAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try { return await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct); }
        catch (JsonException) { throw new OutlookNoDisponibleException("Graph devolvió una respuesta ilegible"); }
    }

    /// <summary>null si falta lo indispensable (id o inicio): esa cita se ignora, no tumba el ciclo.</summary>
    private static CitaOutlook? Leer(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object) return null;
        var id = Texto(e, "id");
        var inicio = Hora(e, "start");
        if (id is null || inicio is null) return null;
        var fin = Hora(e, "end") ?? inicio.Value;

        string? Sub(string obj, string prop) =>
            e.TryGetProperty(obj, out var o) && o.ValueKind == JsonValueKind.Object ? Texto(o, prop) : null;

        var modificada = Texto(e, "lastModifiedDateTime") is { } m
                         && DateTimeOffset.TryParse(m, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto)
            ? dto.UtcDateTime : (DateTime?)null;

        var invitados = new List<InvitadoCita>();
        if (e.TryGetProperty("attendees", out var att) && att.ValueKind == JsonValueKind.Array)
            foreach (var a in att.EnumerateArray().Where(a => a.ValueKind == JsonValueKind.Object))
            {
                var hayCorreo = a.TryGetProperty("emailAddress", out var em) && em.ValueKind == JsonValueKind.Object;
                invitados.Add(new InvitadoCita(
                    Nombre:    hayCorreo ? Texto(em, "name") : null,
                    Correo:    hayCorreo ? Texto(em, "address") : null,
                    EsRecurso: string.Equals(Texto(a, "type"), "resource", StringComparison.OrdinalIgnoreCase)));
            }

        return new CitaOutlook(
            Id: id, ICalUId: Texto(e, "iCalUId"), Asunto: Texto(e, "subject"),
            Inicio: inicio.Value, Fin: fin, TodoElDia: Bool(e, "isAllDay"),
            Ubicacion: Sub("location", "displayName"), EnlaceTeams: Sub("onlineMeeting", "joinUrl"),
            EsOrganizador: Bool(e, "isOrganizer"), Cancelada: Bool(e, "isCancelled"), ModificadaElUtc: modificada,
            Invitados: invitados);
    }

    private static DateTime? Hora(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var o) && o.ValueKind == JsonValueKind.Object && Texto(o, "dateTime") is { } s
        && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? DateTime.SpecifyKind(d, DateTimeKind.Unspecified) : null;

    private static string? Texto(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static bool Bool(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.True;
}
```

- [ ] **Step 5: Registrar** en `DependencyInjection.cs`, junto a `AddHttpClient<IOutlookCalendario, GraphOutlookCalendario>`:

```csharp
        services.AddHttpClient<Application.Reuniones.Outlook.Calendario.ICalendarioOutlookLectura, GraphCalendarioOutlookLectura>(c =>
        {
            c.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/");
            c.Timeout = TimeSpan.FromSeconds(30);
        });
```

- [ ] **Step 6: Correr y ver que pasa**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~Outlook"`
Expected: PASS (todas las de Outlook, incluidas las 4 nuevas).

- [ ] **Step 7: Commit**

```bash
git add src/Infrastructure/Outlook/GraphCalendarioOutlookLectura.cs src/Infrastructure/Outlook/GraphOutlookCalendario.cs src/Application/Reuniones/Outlook/IOutlookCalendario.cs src/Application/Reuniones/Outlook/OutlookCommands.cs src/Infrastructure/DependencyInjection.cs tests/Application.Tests/Outlook/GraphCalendarioOutlookLecturaTests.cs tests/Application.Tests/Outlook/GraphOutlookCalendarioTests.cs
git commit -m "Infrastructure: leer el calendario con Graph; el envio recuerda el iCalUId"
```

---

### Task 6: El servicio en segundo plano

**Files:**
- Create: `src/Infrastructure/Outlook/SincronizacionCalendarioOutlookService.cs`
- Modify: `src/Infrastructure/DependencyInjection.cs` (junto a `AddHostedService<RecordatorioBackgroundService>()`)
- Test: `tests/Application.Tests/Outlook/SincronizacionCalendarioOutlookServiceTests.cs`

**Interfaces:**
- Consumes: `SincronizarCalendarioOutlookCommand` (Task 4); `MicrosoftGraphOptions.EstaCompleta`; `ConexionesMicrosoft`.
- Produces: `SincronizacionCalendarioOutlookService : BackgroundService` con `public Task CicloAsync(CancellationToken ct)`.

- [ ] **Step 1: Prueba que falla**

```csharp
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Reuniones.Outlook;
using Diger.TramitesEstado.Application.Reuniones.Outlook.Calendario;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Outlook;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Outlook;

public class SincronizacionCalendarioOutlookServiceTests
{
    [Fact]
    public async Task Sincroniza_a_cada_conectado_vigente_y_un_fallo_no_detiene_a_los_demas()
    {
        var ana = Guid.NewGuid(); var beto = Guid.NewGuid(); var vencida = Guid.NewGuid();
        var nombreDb = Guid.NewGuid().ToString();
        var sender = Substitute.For<ISender>();
        sender.Send(new SincronizarCalendarioOutlookCommand(ana), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OutlookNoDisponibleException("Graph respondió 503"));
        sender.Send(new SincronizarCalendarioOutlookCommand(beto), Arg.Any<CancellationToken>())
            .Returns(new ResultadoSincronizacionCalendario(1, 0, 0, 0));

        var servicios = new ServiceCollection();
        servicios.AddSingleton(Substitute.For<ICurrentUserService>());
        servicios.AddSingleton(Substitute.For<IPublisher>());
        servicios.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(nombreDb));
        servicios.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        servicios.AddSingleton(sender);
        var sp = servicios.BuildServiceProvider();

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ConexionesMicrosoft.Add(ConexionMicrosoft.Crear(ana, "ana@diger.gob.hn", "h", [1], DateTime.UtcNow));
            db.ConexionesMicrosoft.Add(ConexionMicrosoft.Crear(beto, "beto@diger.gob.hn", "h", [1], DateTime.UtcNow));
            var v = ConexionMicrosoft.Crear(vencida, "v@diger.gob.hn", "h", [1], DateTime.UtcNow);
            v.MarcarVencida("invalid_grant");
            db.ConexionesMicrosoft.Add(v);
            await db.SaveChangesAsync();
        }

        var opciones = Options.Create(new MicrosoftGraphOptions { TenantId = "t", ClientId = "c", ClientSecret = "s" });
        var servicio = new SincronizacionCalendarioOutlookService(sp.GetRequiredService<IServiceScopeFactory>(), opciones,
            NullLogger<SincronizacionCalendarioOutlookService>.Instance);

        await servicio.CicloAsync(default);

        await sender.Received(1).Send(new SincronizarCalendarioOutlookCommand(ana), Arg.Any<CancellationToken>());
        await sender.Received(1).Send(new SincronizarCalendarioOutlookCommand(beto), Arg.Any<CancellationToken>());
        await sender.DidNotReceive().Send(new SincronizarCalendarioOutlookCommand(vencida), Arg.Any<CancellationToken>());
    }
}
```

Si el constructor de `AppDbContext` no se resuelve así desde el contenedor (tiene parámetros propios), registrar `servicios.AddScoped(_ => new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(nombreDb).Options, Substitute.For<ICurrentUserService>(), Substitute.For<IPublisher>()))` en lugar de `AddDbContext`.

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~SincronizacionCalendarioOutlookService"`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear el servicio**

```csharp
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Reuniones.Outlook;
using Diger.TramitesEstado.Application.Reuniones.Outlook.Calendario;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Infrastructure.Outlook;

/// <summary>
/// Cada 15 minutos trae el calendario de Outlook de cada usuario con conexión vigente (spec
/// 2026-10-29). Un scope por usuario: un fallo —Graph caído, conexión vencida— se registra y
/// sigue con el siguiente. Sin TenantId/ClientId/ClientSecret no hace nada.
/// </summary>
public sealed class SincronizacionCalendarioOutlookService(
    IServiceScopeFactory scopes,
    IOptions<MicrosoftGraphOptions> opciones,
    ILogger<SincronizacionCalendarioOutlookService> log) : BackgroundService
{
    internal static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(15);
    // Como RecordatorioBackgroundService: no competir con las migraciones del arranque.
    private static readonly TimeSpan EsperaInicial = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!opciones.Value.EstaCompleta)
        {
            log.LogInformation("Outlook no está configurado: no se sincronizan calendarios.");
            return;
        }

        try
        {
            await Task.Delay(EsperaInicial, ct);
            while (!ct.IsCancellationRequested)
            {
                try { await CicloAsync(ct); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Falló el ciclo de sincronización de calendarios");
                }
                await Task.Delay(Intervalo, ct);
            }
        }
        catch (OperationCanceledException) { /* apagado del host */ }
    }

    /// <summary>Una vuelta completa. Pública para probarla sin esperar al intervalo.</summary>
    public async Task CicloAsync(CancellationToken ct)
    {
        List<Guid> usuarios;
        await using (var scope = scopes.CreateAsyncScope())
        {
            usuarios = await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().ConexionesMicrosoft
                .AsNoTracking().Where(c => !c.RequiereReconexion).Select(c => c.Id).ToListAsync(ct);
        }

        foreach (var usuarioId in usuarios)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var r = await scope.ServiceProvider.GetRequiredService<ISender>()
                    .Send(new SincronizarCalendarioOutlookCommand(usuarioId), ct);
                if (r.Creadas + r.Actualizadas + r.Canceladas > 0)
                    log.LogInformation("Calendario de {UsuarioId}: {Creadas} nuevas, {Actualizadas} actualizadas, {Canceladas} canceladas",
                        usuarioId, r.Creadas, r.Actualizadas, r.Canceladas);
            }
            catch (OutlookConexionVencidaException)
            {
                log.LogInformation("Calendario de {UsuarioId}: la conexión venció; se salta hasta que reconecte", usuarioId);
            }
            catch (OutlookNoDisponibleException ex)
            {
                log.LogWarning("Calendario de {UsuarioId}: Microsoft no respondió ({Motivo}); se reintenta en el próximo ciclo",
                    usuarioId, ex.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Calendario de {UsuarioId}: error inesperado", usuarioId);
            }
        }
    }
}
```

- [ ] **Step 4: Registrar** en `DependencyInjection.cs`, junto a `services.AddHostedService<RecordatorioBackgroundService>();`:

```csharp
        services.AddHostedService<Outlook.SincronizacionCalendarioOutlookService>();
```

Comprobar que las pruebas web (`tests/Web.Tests`, `PortalFactory`) no lo arrancan contra Graph: sin `MicrosoftGraph` completo en su configuración, `ExecuteAsync` sale de inmediato. Si `PortalFactory` sí trae las tres claves, quitar el servicio ahí como ya se haga con `RecordatorioBackgroundService` (buscar `RemoveAll<IHostedService>` o equivalente en `tests/Web.Tests/PortalFactory.cs`).

- [ ] **Step 5: Correr y ver que pasa**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~SincronizacionCalendarioOutlookService"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Infrastructure/Outlook/SincronizacionCalendarioOutlookService.cs src/Infrastructure/DependencyInjection.cs tests/Application.Tests/Outlook/SincronizacionCalendarioOutlookServiceTests.cs
git commit -m "Infrastructure: servicio que sincroniza los calendarios cada 15 minutos"
```

---

### Task 7: El portal muestra y protege las reuniones importadas

**Files:**
- Modify: `src/Application/Reuniones/Common/ReunionDtos.cs` (`ReunionFormDto`)
- Modify: `src/Application/Reuniones/Common/ReunionMapper.cs` (`Aplicar` y `ToForm`)
- Modify: `src/Application/Reuniones/Queries/GetReuniones/GetReunionesQuery.cs` (`ReunionListItemDto` y su `Select`)
- Modify: `src/Web/Pages/Reuniones/Editor.cshtml` (tarjeta de Outlook y los cinco inputs)
- Modify: `src/Web/Pages/Reuniones/Index.cshtml` (celda del título, junto a `hilo-badge`)
- Test: `tests/Application.Tests/Reuniones/ReunionImportadaDeOutlookTests.cs`

**Interfaces:**
- Consumes: `Reunion.EsImportadaDeOutlook`, `CanceladaEnOutlookEl`, `ImportarDeOutlook` (Task 1).
- Produces: `ReunionFormDto.EsImportadaDeOutlook`, `ReunionFormDto.CanceladaEnOutlookEl`; `ReunionListItemDto(..., bool DeOutlook = false, bool CanceladaEnOutlook = false)`.

- [ ] **Step 1: Prueba que falla**

```csharp
using Diger.TramitesEstado.Application.Reuniones.Common;
using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones;

/// <summary>Outlook manda sobre los Generales de una reunión importada: guardar desde el editor no
/// puede pisarlos, aunque el formulario los traiga (no basta con deshabilitar el input).</summary>
public class ReunionImportadaDeOutlookTests
{
    private static Reunion Importada() => Reunion.ImportarDeOutlook("AAMk-1",
        new GeneralesOutlook("Mesa", new DateOnly(2026, 11, 3), "09:00", "1:00", "Sala 2", null, false, "ical", null),
        Guid.NewGuid(), "DIGER", null, null);

    [Fact]
    public void Aplicar_no_pisa_los_generales_de_una_importada_pero_si_lo_demas()
    {
        var r = Importada();
        var (datos, asistentes, acuerdos) = ReunionMapper.ToForm(r);
        datos.Titulo = "Cambiado en el portal";
        datos.Fecha = new DateOnly(2027, 1, 1);
        datos.Hora = "18:00";
        datos.Duracion = "3 horas";
        datos.Lugar = "Otra sala";
        datos.ObjetivoAgenda = "Objetivo nuevo";

        ReunionMapper.Aplicar(r, datos, asistentes, acuerdos);

        r.Titulo.Should().Be("Mesa");
        r.Fecha.Should().Be(new DateOnly(2026, 11, 3));
        r.Hora.Should().Be("09:00");
        r.Duracion.Should().Be("1:00");
        r.Lugar.Should().Be("Sala 2");
        r.ObjetivoAgenda.Should().Be("Objetivo nuevo");
    }

    [Fact]
    public void ToForm_dice_que_es_importada_y_si_esta_cancelada()
    {
        var r = Importada();
        r.MarcarCanceladaEnOutlook(new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));

        var (datos, _, _) = ReunionMapper.ToForm(r);

        datos.EsImportadaDeOutlook.Should().BeTrue();
        datos.CanceladaEnOutlookEl.Should().Be(new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));
    }
}
```

- [ ] **Step 2: Correr y ver que falla**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~ReunionImportadaDeOutlook"`
Expected: FAIL de compilación (`EsImportadaDeOutlook` no existe en el DTO).

- [ ] **Step 3: DTO** — en `ReunionFormDto`, después de `public string? Lugar { get; set; }`:

```csharp
    /// <summary>Solo lectura para la vista: la regla vive en ReunionMapper.Aplicar, que mira la
    /// reunión y no este campo (el formulario no lo envía y no se puede falsificar).</summary>
    public bool      EsImportadaDeOutlook { get; set; }
    public DateTime? CanceladaEnOutlookEl { get; set; }
```

- [ ] **Step 4: Mapper** — en `ReunionMapper.Aplicar`, reemplazar las tres primeras líneas:

```csharp
        r.EstablecerTitulo(d.Titulo);
        r.Fecha = d.Fecha; r.Hora = d.Hora?.Trim(); r.Duracion = d.Duracion?.Trim();
        r.Modalidad = d.Modalidad?.Trim(); r.Lugar = d.Lugar?.Trim();
```

por:

```csharp
        // En una reunión que vino de Outlook, Outlook manda sobre estos cinco (spec 2026-10-29):
        // el editor los muestra de solo lectura y aquí se ignoran aunque lleguen en el formulario.
        if (!r.EsImportadaDeOutlook)
        {
            r.EstablecerTitulo(d.Titulo);
            r.Fecha = d.Fecha; r.Hora = d.Hora?.Trim(); r.Duracion = d.Duracion?.Trim();
            r.Lugar = d.Lugar?.Trim();
        }
        r.Modalidad = d.Modalidad?.Trim();
```

Y en `ToForm`, dentro del inicializador de `ReunionFormDto`, después de `Modalidad = r.Modalidad, Lugar = r.Lugar,`:

```csharp
            EsImportadaDeOutlook = r.EsImportadaDeOutlook, CanceladaEnOutlookEl = r.CanceladaEnOutlookEl,
```

- [ ] **Step 5: Correr y ver que pasa**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~ReunionImportadaDeOutlook"`
Expected: PASS (2).

- [ ] **Step 6: Lista** — `ReunionListItemDto` en `GetReunionesQuery.cs`:

```csharp
public sealed record ReunionListItemDto(
    int Id, string Titulo, DateOnly? Fecha, string? Institucion, string? Tipo, int NumAsistentes,
    VisibilidadReunion Visibilidad, Guid? HiloId = null, DateTime CreadaEl = default,
    bool DeOutlook = false, bool CanceladaEnOutlook = false);
```

y su proyección:

```csharp
            .Select(r => new ReunionListItemDto(
                r.Id, r.Titulo, r.Fecha, r.Institucion, r.Tipo, r.Asistentes.Count, r.Visibilidad, r.HiloId, r.CreatedAt,
                r.OutlookImportadaPorId != null, r.CanceladaEnOutlookEl != null))
```

(`EsImportadaDeOutlook` está ignorada en EF y no se traduce a SQL: por eso la proyección pregunta por las columnas).

- [ ] **Step 7: `Index.cshtml`** — en las DOS celdas de título (la tabla paginada, `@foreach (var r in Model.Resultado.Items)`, y la agrupada, `@foreach (var r in grupo)`), justo después del `@if (r.HiloId is Guid h) { … }`:

```cshtml
@if (r.CanceladaEnOutlook) { <span class="hilo-badge" title="La cita se canceló o se borró en Outlook">cancelada en Outlook</span> }
else if (r.DeOutlook) { <span class="hilo-badge" title="Viene de su calendario de Outlook">Outlook</span> }
```

- [ ] **Step 8: `Editor.cshtml`** — los cinco inputs de Generales llevan `readonly` cuando la reunión es importada:

```cshtml
<input asp-for="Datos.Titulo" placeholder="Ej: Taller de inducción a la plataforma" required readonly="@(d.EsImportadaDeOutlook ? "readonly" : null)" />
<div class="field"><label>Fecha</label><input asp-for="Datos.Fecha" type="date" readonly="@(d.EsImportadaDeOutlook ? "readonly" : null)" /></div>
<div class="field"><label>Hora</label><input asp-for="Datos.Hora" type="time" readonly="@(d.EsImportadaDeOutlook ? "readonly" : null)" /></div>
<div class="field"><label>Duración</label><input asp-for="Datos.Duracion" placeholder="2 horas" readonly="@(d.EsImportadaDeOutlook ? "readonly" : null)" /></div>
<div class="field"><label>Lugar</label><input asp-for="Datos.Lugar" placeholder="Sede, sala o enlace de videollamada" readonly="@(d.EsImportadaDeOutlook ? "readonly" : null)" /></div>
```

(Razor omite el atributo cuando el valor es `null`. Confirmar que `d` es la variable de `Model.Datos` ya usada en la tarjeta de Outlook: `d.Modalidad`.)

En la tarjeta de Outlook, reemplazar `@if (o.EnOutlook)` por una rama previa:

```cshtml
        @if (d.EsImportadaDeOutlook)
        {
            <span class="hint">Viene de su calendario de Outlook: el título, la fecha, la hora, la duración y el lugar
                se cambian allá y llegan solos en unos minutos.</span>
            @if (d.CanceladaEnOutlookEl is { } cancelada)
            {
                <div class="alert-warn" style="flex-basis:100%">La cita se canceló o se borró en Outlook
                    (visto el @cancelada.ToLocalTime().ToString("dd/MM/yyyy HH:mm")). La reunión se conserva con lo que
                    se haya llenado; si no la necesita, puede borrarla.</div>
            }
        }
        else if (o.EnOutlook)
```

(el resto de la cadena `else if …` queda igual).

- [ ] **Step 9: Compilar y correr toda la suite**

Run: `dotnet build Diger.TramitesEstado.sln` y luego `dotnet test Diger.TramitesEstado.sln`
Expected: `0 Errores`; todas las pruebas correctas.

- [ ] **Step 10: Commit**

```bash
git add src/Application/Reuniones/Common/ReunionDtos.cs src/Application/Reuniones/Common/ReunionMapper.cs src/Application/Reuniones/Queries/GetReuniones/GetReunionesQuery.cs src/Web/Pages/Reuniones/Editor.cshtml src/Web/Pages/Reuniones/Index.cshtml tests/Application.Tests/Reuniones/ReunionImportadaDeOutlookTests.cs
git commit -m "Reuniones: las importadas de Outlook se ven marcadas y sus Generales no se editan"
```

---

### Task 8: Doctrina, spec y prueba real

**Files:**
- Modify: `CLAUDE.md` (sección `### Reuniones a Outlook`)
- Modify: `docs/superpowers/specs/2026-10-29-calendario-outlook-al-portal-design.md` (estado y desvíos)

- [ ] **Step 1: CLAUDE.md** — en `### Reuniones a Outlook`, reemplazar `Portal → Outlook, nunca al revés.` por:

```markdown
Dos sentidos que no se cruzan: **Portal → Outlook** para las reuniones creadas en el portal, y
**Outlook → Portal, solo Generales**, para el resto del calendario de cada usuario conectado
(`SincronizacionCalendarioOutlookService`, cada 15 min, de hoy a +90 días; spec
`docs/superpowers/specs/2026-10-29-calendario-outlook-al-portal-design.md`). Una reunión importada
entra privada, no está «En Outlook» (`OutlookEventId` null: el portal nunca le manda cambios), y sus
Generales los ignora `ReunionMapper.Aplicar`. Sus invitados entran como pre-registrados
(`Asistente.DeOutlook`); la sincronización solo quita a los suyos que siguen pendientes.
```

- [ ] **Step 2: Spec** — cambiar `**Estado:** aprobado en conversación, pendiente de revisión escrita` por `**Estado:** implementado (2026-10-29)` y agregar al final:

```markdown
## Desvíos durante la implementación

- `OrigenExternoId` mide 60 y un id de evento de Graph pasa de 150: allí va `outlook:` + SHA-256
  base64url (51), y el id completo en la columna nueva `OutlookImportadoEventId` (cinco columnas, no cuatro).
- El servicio vive en `Infrastructure/Outlook/`, junto a `RecordatorioBackgroundService`, no en Web.
- La prueba "el editor no deja cambiar Generales" es de `ReunionMapper.Aplicar` (Application): es ahí
  donde vive la regla; el `readonly` del input es solo aviso.
```

- [ ] **Step 3: Prueba real** (con el usuario; no la hace un subagente)

1. Correr `database/2026-10-29_poner_al_dia.sql` en la base local (ya hecho en la Task 2).
2. Reiniciar el portal desde consola: `ASPNETCORE_ENVIRONMENT=Development ./bin/Debug/net9.0/Diger.TramitesEstado.Web.exe` desde `src/Web` (puertos 49175/49176/49177).
3. Esperar ~2 minutos (espera inicial) y revisar el registro: `Calendario de <id>: N nuevas…`.
4. Comprobar en la base: `SELECT COUNT(*), SUM(CASE WHEN Visibilidad='Privada' THEN 1 ELSE 0 END) FROM Reuniones WHERE OutlookImportadaPorId IS NOT NULL` → ambos iguales y > 0.
5. Que la reunión 2062 (enviada desde el portal) **no** aparezca duplicada.
6. Abrir una importada con invitados: aparecen como pre-registrados, sin salas ni el propio usuario.
7. Pedir al usuario: mover una cita en Outlook y, tras ≤ 15 min, ver la hora nueva en el portal; cancelar otra y ver "cancelada en Outlook".

- [ ] **Step 4: Commit**

```bash
git add CLAUDE.md docs/superpowers/specs/2026-10-29-calendario-outlook-al-portal-design.md
git commit -m "Docs: el calendario de Outlook entra al portal (doctrina y desvios)"
```
