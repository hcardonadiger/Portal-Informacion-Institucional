# Ayuda memoria por el MCP — Fase 2, parte A — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que Claude, conectado al MCP del portal, llene la ayuda memoria de una reunión desde una transcripción —con las mismas reglas que hoy aplica el botón «Aplicar» del editor— y que esas reglas vivan en un solo lugar del servidor.

**Architecture:** Las reglas que hoy están en el JavaScript del editor (prompt por tipo, formato del JSON, qué clave llena qué campo, qué se ignora y por qué) pasan a una clase pura de Application, `MemoriaDesdeTranscripcion`. El editor la usa para pintar el prompt y para interpretar el JSON (sigue llenando el formulario; la persona guarda). El MCP la usa a través de `LlenarMemoriaReunionCommand`, que sí escribe en la base. Además, la sincronización con Outlook deja de actualizar citas de reuniones que ya terminaron.

**Tech Stack:** .NET 9, Razor Pages, MediatR, EF Core 9, System.Text.Json, ModelContextProtocol.AspNetCore, xUnit + FluentAssertions + NSubstitute.

**Spec:** `docs/superpowers/specs/2026-10-22-reuniones-a-outlook-design.md`, sección «Fase 2». Este plan cubre su parte A (escribir la memoria); la parte B (descargar la transcripción de Teams) tendrá su propio plan.

## Global Constraints

- Una sola copia de las reglas de interpretación: `MemoriaDesdeTranscripcion`. El editor y el MCP la llaman; ninguno las reescribe.
- «Generales» (titulo, fecha, hora, duracion, modalidad, tipo, institucion, instituciones, lugar, visibilidad) **nunca** se escribe desde el JSON; si viene, se descarta y se avisa.
- El bloque de capacitación (convocados, num_asistentes, pct_asistencia, compromisos) solo se aplica si `Tipo == "Capacitación"`; `validador_institucion` solo si `EncuestaActiva`. `satisfaccion`/`satisfaccion_calificacion` y `objetivo_capacitacion`/`contenido` se descartan siempre, con su aviso.
- El editor sigue **llenando el formulario** (no guarda): lo que la persona tecleó sin guardar no se pierde.
- Sin «sobreescribir», si un campo ya tiene un valor distinto, no se aplica nada (mismo criterio que hoy el editor).
- El MCP **nunca** reemplaza acuerdos que ya tienen seguimiento (Estado distinto de Pendiente, FechaCumplimiento, NotaSeguimiento o comentarios), ni con `sobreescribir=true`.
- `responsable` de un acuerdo se guarda como texto (`AcuerdoReunion.Responsable`); `ResponsableContactoId` queda nulo.
- Una reunión cuya cita ya terminó (fin del evento < ahora en Honduras) no se actualiza en Outlook al guardar; borrarla sigue cancelando la cita.
- Mensajes al usuario en español, idénticos a los del JavaScript actual donde ya existen.
- Permisos MCP: leer instrucciones = `Reuniones.Ver`; escribir = `Reuniones.Editar` con `ExigirEscrituraAsync`.
- Todo `fetch` que escriba lleva el token antiforgery (`RequestVerificationToken`, CLAUDE.md).
- Commands devuelven `Unit` o un id; queries no llaman `SaveChangesAsync`.

## Decisiones (aprobadas por el usuario el 2026-10-23)

1. El editor sigue llenando el formulario; las reglas pasan al servidor (desvía la frase del spec «el botón Aplicar pasa a llamar al comando», que habría escrito en la base y perdido lo no guardado).
2. El MCP no pisa acuerdos con seguimiento.
3. `responsable` como texto.
4. No se actualiza Outlook cuando la reunión ya terminó (hueco de la fase 1: llenar la memoria después cambiaba `ObjetivoAgenda`, que va en el cuerpo de la cita, y avisaba «reunión actualizada» a todos los invitados).

Decisión de planificación: `llenar_memoria_reunion` no sincroniza Outlook. La memoria se llena después de la reunión, y con la decisión 4 esa sincronización no haría nada; si se llenara antes, la próxima vez que se guarde desde el editor la cita se pone al día.

## Mapa de archivos

| Archivo | Acción | Responsabilidad |
|---|---|---|
| `src/Application/Reuniones/Memoria/MemoriaDesdeTranscripcion.cs` | Crear | Instrucciones (prompt, formato, campos) e interpretación del JSON — puro |
| `src/Application/Reuniones/Memoria/LlenarMemoriaReunionCommand.cs` | Crear | Escribe la memoria interpretada en la reunión (MCP) |
| `src/Application/Reuniones/Outlook/OutlookCommands.cs` | Modificar | Sincronizar no actualiza reuniones ya terminadas |
| `src/Web/Mcp/ReunionesMemoriaTools.cs` | Crear | `instrucciones_memoria_reunion`, `llenar_memoria_reunion` |
| `src/Web/Program.cs` | Modificar | `.WithTools<ReunionesMemoriaTools>()` |
| `src/Web/Pages/Reuniones/Editor.cshtml(.cs)` | Modificar | Prompt desde el servidor; «Aplicar» interpreta en el servidor |
| `tests/Application.Tests/Reuniones/Memoria/*.cs` | Crear | Reglas e escritura |
| `tests/Application.Tests/Reuniones/Outlook/OutlookCommandsTests.cs` | Modificar | Reloj controlado + regla de reunión terminada |
| `tests/Web.Tests/OutlookPaginasTests.cs` | Modificar | Fechas de reuniones al futuro |
| `tests/Web.Tests/MemoriaEditorTests.cs` | Crear | Handler de interpretación del editor |

---

### Task 1: Las reglas, en el servidor

**Files:**
- Create: `src/Application/Reuniones/Memoria/MemoriaDesdeTranscripcion.cs`
- Test: `tests/Application.Tests/Reuniones/Memoria/MemoriaDesdeTranscripcionTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public sealed record CampoMemoria(string Clave, string Etiqueta);
  public sealed record InstruccionesMemoria(string Variante, string Prompt, string Formato, IReadOnlyList<CampoMemoria> Campos);
  public sealed record AcuerdoPropuesto(string Compromiso, string? Responsable, DateOnly? Plazo);
  public sealed record MemoriaPropuesta(
      string? Objetivo, string? Desarrollo, string? Documentos, string? ValidadorDiger, string? ValidadorInstitucion,
      int? Convocados, int? NumAsistentes, int? PctAsistencia, string? Compromisos,
      IReadOnlyList<AcuerdoPropuesto> Acuerdos, int CamposAplicables, string Mensaje);
  public static class MemoriaDesdeTranscripcion
  {
      public static InstruccionesMemoria Instrucciones(bool esCapacitacion, bool conEncuesta);
      public static MemoriaPropuesta Interpretar(string json, bool esCapacitacion, bool conEncuesta); // DomainException si no es un objeto JSON
      public static bool EsCapacitacion(string? tipo); // tipo == "Capacitación"
  }
  ```

La fuente de verdad del comportamiento a portar es el JavaScript actual de `src/Web/Pages/Reuniones/Editor.cshtml`: constantes `CW_CAMPOS_MEMORIA`, `CW_CAMPOS_CAP`, `CW_CAMPOS_CIERRE`, `CW_CAMPOS_ENCUESTA`, `CW_BLOQUE_MEMORIA`, `CW_BLOQUE_CAP`, `CW_BLOQUE_CIERRE_CON_ENCUESTA`, `CW_BLOQUE_CIERRE_SIN_ENCUESTA`, `CW_REGLAS_COMUNES`, `CW_REGLAS_CAP` (≈ líneas 1258-1323), y las funciones `cwPrompt`, `cwFormato`, `cwLineas`, `aplicarJsonCowork` (≈ 1325-1521). **Copiar los textos carácter por carácter** (prompt, formato, etiquetas, mensajes); el test de igualdad del Step 1 lo comprueba.

- [ ] **Step 1: Write the failing tests**

```csharp
using Diger.TramitesEstado.Application.Reuniones.Memoria;
using Diger.TramitesEstado.Domain.Common;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones.Memoria;

/// <summary>
/// Las reglas que hasta el 2026-10-23 vivían en el JavaScript del editor. Lo que se protege: que
/// «Generales» nunca se escriba desde un resumen de IA, que el bloque de capacitación y la segunda
/// firma solo caigan donde aplican, y que cada descarte se diga en vez de pasar en silencio.
/// </summary>
public class MemoriaDesdeTranscripcionTests
{
    [Fact]
    public void Las_instrucciones_de_reunion_no_piden_el_bloque_de_capacitacion_ni_la_segunda_firma()
    {
        var i = MemoriaDesdeTranscripcion.Instrucciones(esCapacitacion: false, conEncuesta: false);

        i.Variante.Should().Be("Reunión");
        i.Prompt.Should().StartWith("Eres un asistente que redacta la memoria de una reunión institucional de la DIGER.");
        i.Prompt.Should().Contain(i.Formato);
        i.Formato.Should().NotContain("convocados").And.NotContain("validador_institucion");
        i.Campos.Select(c => c.Clave).Should().Equal("objetivo", "desarrollo", "acuerdos[]", "documentos", "validador_diger");
    }

    [Fact]
    public void Las_instrucciones_de_capacitacion_con_encuesta_piden_todo()
    {
        var i = MemoriaDesdeTranscripcion.Instrucciones(esCapacitacion: true, conEncuesta: true);

        i.Variante.Should().Be("Capacitación");
        i.Prompt.Should().StartWith("Eres un asistente que redacta la memoria de una CAPACITACIÓN impartida por la DIGER.");
        i.Prompt.Should().Contain("7. Esta actividad es una CAPACITACIÓN");
        i.Formato.Should().Contain("\"convocados\": 30,").And.Contain("\"validador_institucion\"");
        i.Campos.Select(c => c.Clave).Should().Equal(
            "objetivo", "desarrollo", "acuerdos[]",
            "convocados", "num_asistentes", "pct_asistencia", "compromisos",
            "documentos", "validador_diger", "validador_institucion");
    }

    [Fact]
    public void Interpreta_memoria_cierre_y_acuerdos()
    {
        var p = MemoriaDesdeTranscripcion.Interpretar("""
            { "objetivo": "Revisar avances", "desarrollo": "<p>Se revisó.</p>",
              "acuerdos": [ { "compromiso": "Enviar informe", "responsable": "IHTT", "plazo": "2026-11-05" },
                            { "compromiso": "Sin plazo" } ],
              "documentos": ["Presentación.pdf", "", "Lista firmada"], "validador_diger": "Ana Paz" }
            """, esCapacitacion: false, conEncuesta: false);

        p.Objetivo.Should().Be("Revisar avances");
        p.Desarrollo.Should().Be("<p>Se revisó.</p>");
        p.Documentos.Should().Be("Presentación.pdf\nLista firmada", "uno por línea, sin vacíos, como cwLineas");
        p.ValidadorDiger.Should().Be("Ana Paz");
        p.Acuerdos.Should().Equal(
            new AcuerdoPropuesto("Enviar informe", "IHTT", new DateOnly(2026, 11, 5)),
            new AcuerdoPropuesto("Sin plazo", null, null));
        p.CamposAplicables.Should().Be(6, "objetivo, desarrollo, documentos, validador y dos acuerdos");
        p.Mensaje.Should().Be("Se llenaron 6 campo(s) de Memoria y Cierre.");
    }

    [Fact]
    public void Generales_se_descarta_y_se_dice()
    {
        var p = MemoriaDesdeTranscripcion.Interpretar(
            """{ "titulo": "Otro", "fecha": "2026-01-01", "lugar": "X", "objetivo": "O" }""", false, false);

        p.Objetivo.Should().Be("O");
        p.Mensaje.Should().Be("Se llenaron 1 campo(s) de Memoria y Cierre. Se ignoró titulo, fecha, lugar: «Generales» no se modifica desde el JSON.");
    }

    [Fact]
    public void El_bloque_de_capacitacion_solo_cae_en_una_capacitacion()
    {
        const string json = """{ "convocados": 30, "num_asistentes": "24", "pct_asistencia": 80, "compromisos": ["Replicar", "Evaluar"] }""";

        var reunion = MemoriaDesdeTranscripcion.Interpretar(json, esCapacitacion: false, conEncuesta: false);
        reunion.Convocados.Should().BeNull();
        reunion.Compromisos.Should().BeNull();
        reunion.Mensaje.Should().Be("El JSON no traía ningún campo de Memoria, Capacitación ni Cierre. Se ignoró convocados, num_asistentes, pct_asistencia, compromisos: el tipo de esta reunión no es Capacitación.");

        var cap = MemoriaDesdeTranscripcion.Interpretar(json, esCapacitacion: true, conEncuesta: false);
        cap.Convocados.Should().Be(30);
        cap.NumAsistentes.Should().Be(24, "un número como texto también vale");
        cap.PctAsistencia.Should().Be(80);
        cap.Compromisos.Should().Be("Replicar\nEvaluar");
        cap.Mensaje.Should().Be("Se llenaron 4 campo(s) de Memoria, Capacitación y Cierre.");
    }

    [Fact]
    public void La_segunda_firma_solo_cae_con_la_encuesta_encendida()
    {
        const string json = """{ "validador_institucion": "Luis" }""";

        MemoriaDesdeTranscripcion.Interpretar(json, false, conEncuesta: true).ValidadorInstitucion.Should().Be("Luis");

        var sin = MemoriaDesdeTranscripcion.Interpretar(json, false, conEncuesta: false);
        sin.ValidadorInstitucion.Should().BeNull();
        sin.Mensaje.Should().EndWith("Se ignoró validador_institucion: la encuesta de esta reunión está apagada.");
    }

    [Fact]
    public void Satisfaccion_y_detalle_viejo_de_capacitacion_se_descartan_con_su_aviso()
    {
        var p = MemoriaDesdeTranscripcion.Interpretar(
            """{ "satisfaccion": "buena", "contenido": "x", "objetivo": "O" }""", true, false);

        p.Mensaje.Should().Contain("Se ignoró satisfaccion: la satisfacción ya no se captura en el cierre.");
        p.Mensaje.Should().Contain("Se ignoró contenido: el detalle de capacitación se quitó; use «objetivo» y «desarrollo».");
    }

    [Theory]
    [InlineData("no es json")]
    [InlineData("[1,2]")]
    [InlineData("")]
    public void Lo_que_no_es_un_objeto_JSON_se_rechaza(string json)
    {
        var acto = () => MemoriaDesdeTranscripcion.Interpretar(json, false, false);

        acto.Should().Throw<DomainException>().WithMessage("El texto pegado no es un JSON válido.");
    }

    [Theory]
    [InlineData("""{ "pct_asistencia": 140 }""")]
    [InlineData("""{ "pct_asistencia": "mucho" }""")]
    public void Un_porcentaje_imposible_no_se_aplica(string json)
    {
        MemoriaDesdeTranscripcion.Interpretar(json, esCapacitacion: true, conEncuesta: false)
            .PctAsistencia.Should().BeNull();
    }

    [Fact]
    public void Un_plazo_ilegible_deja_el_acuerdo_sin_plazo()
    {
        var p = MemoriaDesdeTranscripcion.Interpretar(
            """{ "acuerdos": [ { "compromiso": "X", "plazo": "el viernes" }, { "responsable": "sin compromiso" } ] }""", false, false);

        p.Acuerdos.Should().Equal(new AcuerdoPropuesto("X", null, null));
    }

    [Theory]
    [InlineData("Capacitación", true)]
    [InlineData("Taller", false)]
    [InlineData(null, false)]
    public void Es_capacitacion_solo_con_el_tipo_exacto(string? tipo, bool esperado) =>
        MemoriaDesdeTranscripcion.EsCapacitacion(tipo).Should().Be(esperado);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~MemoriaDesdeTranscripcionTests"`
Expected: build FAIL — `MemoriaDesdeTranscripcion` no existe.

- [ ] **Step 3: Implement**

`src/Application/Reuniones/Memoria/MemoriaDesdeTranscripcion.cs` — estructura (los textos se copian del JavaScript, ver arriba):

```csharp
using System.Globalization;
using System.Text.Json;

namespace Diger.TramitesEstado.Application.Reuniones.Memoria;

public sealed record CampoMemoria(string Clave, string Etiqueta);
public sealed record InstruccionesMemoria(string Variante, string Prompt, string Formato, IReadOnlyList<CampoMemoria> Campos);
public sealed record AcuerdoPropuesto(string Compromiso, string? Responsable, DateOnly? Plazo);
public sealed record MemoriaPropuesta(
    string? Objetivo, string? Desarrollo, string? Documentos, string? ValidadorDiger, string? ValidadorInstitucion,
    int? Convocados, int? NumAsistentes, int? PctAsistencia, string? Compromisos,
    IReadOnlyList<AcuerdoPropuesto> Acuerdos, int CamposAplicables, string Mensaje);

/// <summary>
/// La ayuda memoria a partir de una transcripción: qué se le pide a la IA y cómo se lee lo que
/// devuelve. Única copia de estas reglas — la usan el editor (que llena el formulario) y el MCP
/// (que escribe la reunión). Hasta el 2026-10-23 vivían en el JavaScript del editor.
/// </summary>
public static class MemoriaDesdeTranscripcion
{
    public const string TipoCapacitacion = "Capacitación";

    public static bool EsCapacitacion(string? tipo) => tipo == TipoCapacitacion;

    // ── Textos: copiados de Editor.cshtml (CW_*). No reescribir: el prompt ya está probado. ──
    private static readonly CampoMemoria[] CamposMemoria = [ new("objetivo", "Memoria · Objetivo de la reunión"), new("desarrollo", "Memoria · Desarrollo (HTML simple)"), new("acuerdos[]", "Memoria · Acuerdos y compromisos") ];
    private static readonly CampoMemoria[] CamposCap = [ new("convocados", "Capacitación · Convocados"), new("num_asistentes", "Capacitación · Nº asistentes"), new("pct_asistencia", "Capacitación · % asistencia"), new("compromisos", "Capacitación · Compromisos") ];
    private static readonly CampoMemoria[] CamposCierre = [ new("documentos", "Cierre · Documentos / recursos"), new("validador_diger", "Cierre · Validación DIGER") ];
    private static readonly CampoMemoria[] CamposEncuesta = [ new("validador_institucion", "Cierre · Validación institución") ];
    // BloqueMemoria, BloqueCap, BloqueCierreConEncuesta, BloqueCierreSinEncuesta, ReglasComunes, ReglasCap:
    // string[] con las mismas líneas que CW_BLOQUE_* y CW_REGLAS_* del JavaScript.

    public static InstruccionesMemoria Instrucciones(bool esCapacitacion, bool conEncuesta)
    {
        // Formato = cwFormato; Prompt = cwPrompt; Campos = el mismo orden que coworkRender.
        …
    }

    public static MemoriaPropuesta Interpretar(string json, bool esCapacitacion, bool conEncuesta)
    {
        JsonElement d;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "x" : json);
            d = doc.RootElement.Clone();
        }
        catch (JsonException) { throw new DomainException("El texto pegado no es un JSON válido."); }
        if (d.ValueKind != JsonValueKind.Object) throw new DomainException("El texto pegado no es un JSON válido.");

        // Texto(d, "objetivo"), Lineas(d, "documentos") (= cwLineas: arreglo sin vacíos unido por \n, o el texto),
        // Entero(d, "convocados") (número o texto numérico, ≥ 0), Porcentaje (0..100), Acuerdos (compromiso
        // obligatorio; plazo yyyy-MM-dd o nulo); un campo cuenta en CamposAplicables solo si tiene valor y le toca
        // a esta variante; los acuerdos cuentan uno por acuerdo. Mensaje = exactamente el armado de aplicarJsonCowork,
        // con las listas de ignorados en el mismo orden de claves que el JavaScript.
        …
    }
}
```

Reglas exactas de `Interpretar` (del JavaScript):
- Campos de esta variante: `objetivo`, `documentos`, `validador_diger`; + `validador_institucion` si `conEncuesta`; + `compromisos`, `convocados`, `num_asistentes`, `pct_asistencia` si `esCapacitacion`; + `desarrollo`; + `acuerdos`.
- Valor nulo/ausente/vacío → no cuenta y queda `null`.
- `CamposAplicables` = nº de campos con valor + nº de acuerdos válidos.
- Mensaje base: `0` → `"El JSON no traía ningún campo de Memoria, Capacitación ni Cierre."`; si no → `"Se llenaron {n} campo(s) de Memoria" + (esCapacitacion ? ", Capacitación" : "") + " y Cierre."`.
- Luego, en este orden, cada uno solo si hay claves que nombrar (unidas con `", "`), precedido de un espacio:
  `"Se ignoró {claves}: «Generales» no se modifica desde el JSON."` (titulo, fecha, hora, duracion, modalidad, tipo, institucion, instituciones, lugar, visibilidad) ·
  `"Se ignoró {claves}: el tipo de esta reunión no es Capacitación."` (convocados, num_asistentes, pct_asistencia, compromisos — solo si no es capacitación) ·
  `"Se ignoró {claves}: la satisfacción ya no se captura en el cierre."` (satisfaccion, satisfaccion_calificacion) ·
  `"Se ignoró {claves}: el detalle de capacitación se quitó; use «objetivo» y «desarrollo»."` (objetivo_capacitacion, contenido) ·
  `"Se ignoró {claves}: la encuesta de esta reunión está apagada."` (validador_institucion — solo si la encuesta está apagada).
- Una clave «presente» para los avisos = existe y no es `null` (igual que `d[k] != null` en JS).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests\Application.Tests --filter "FullyQualifiedName~MemoriaDesdeTranscripcionTests"`
Expected: PASS.

- [ ] **Step 5: Commit** — `git commit -m "Las reglas de la ayuda memoria desde una transcripcion pasan del JavaScript al servidor"` (+ trailer Co-Authored-By).

---

### Task 2: Escribir la memoria (para el MCP)

**Files:**
- Create: `src/Application/Reuniones/Memoria/LlenarMemoriaReunionCommand.cs`
- Test: `tests/Application.Tests/Reuniones/Memoria/LlenarMemoriaReunionCommandTests.cs`

**Interfaces:**
- Consumes: `MemoriaDesdeTranscripcion` (Task 1); `IReunionRepository.GetByIdWithDetailsAsync` (incluye Acuerdos); `IApplicationDbContext.ComentariosCompromisos`.
- Produces: `public sealed record LlenarMemoriaReunionCommand(int ReunionId, string Json, bool Sobreescribir) : IRequest<Unit>;` — lanza `NotFoundException` / `DomainException`.

Comportamiento:
1. Carga la reunión (rastreada, con acuerdos); `esCap = EsCapacitacion(r.Tipo)`, `conEnc = r.EncuestaActiva`; `p = Interpretar(json, esCap, conEnc)`.
2. Sin `Sobreescribir`: si cualquier campo de `p` con valor choca con un valor existente distinto y no vacío (`ObjetivoAgenda`, `DocsRecursos`, `ValDiger`, `ValInst`, `Compromisos`, `Convocados`, `NumAsistentes`, `PctAsistencia`, `Desarrollo` — comparando con `Trim()`), o `p.Acuerdos` no está vacío y la reunión ya tiene acuerdos → `DomainException("Ya hay campos con información. Use «sobreescribir» para borrar los campos existentes.")` sin tocar nada.
3. Con o sin `Sobreescribir`: si `p.Acuerdos` no está vacío y algún acuerdo existente tiene seguimiento (`Estado != EstadoCompromiso.Pendiente || FechaCumplimiento != null || !string.IsNullOrWhiteSpace(NotaSeguimiento)` o algún `ComentarioCompromiso` suyo) → `DomainException("La reunión ya tiene acuerdos en seguimiento; no se reemplazan desde una transcripción. Agregue los nuevos desde el editor.")`.
4. Aplica cada campo de `p` con valor; si `p.Acuerdos` no está vacío, reemplaza los acuerdos (quita los existentes —ninguno tiene seguimiento por el punto 3— y agrega uno por propuesto, `Orden` 0..n-1, `Responsable` = texto, `ResponsableContactoId = null`, `Plazo`). Usar las mismas operaciones que ya usa el agregado para reemplazar acuerdos (revisar `ReunionMapper.Aplicar` / `Reunion.LimpiarHijos` — si `LimpiarHijos` también vacía asistentes y adjuntos, NO usarlo: quitar solo los acuerdos; si el agregado no expone cómo, agregar a `Reunion` un método `ReemplazarAcuerdos(IEnumerable<AcuerdoReunion>)` con su prueba de dominio).
5. `SaveChangesAsync`.

- [ ] **Step 1: Write the failing tests** — en el patrón de `tests/Application.Tests/Reuniones/Outlook/OutlookCommandsTests.cs` (AppDbContext InMemory, `ICurrentUserService` sustituido con `EsGlobal = true`, `ReunionRepository` real). Casos:
  - `Llena_una_reunion_vacia_y_guarda` — objetivo, desarrollo, documentos, validador, 2 acuerdos → todo persistido (releer con `AsNoTracking`), acuerdos con `Responsable` texto y `ResponsableContactoId` nulo.
  - `Generales_no_se_toca` — JSON con `titulo` y `fecha` distintos → `Titulo`/`Fecha` sin cambio.
  - `Sin_sobreescribir_un_valor_distinto_no_aplica_nada` — `ObjetivoAgenda` ya con otro texto → `DomainException` «*sobreescribir*» y nada cambió (tampoco `Desarrollo`).
  - `Con_sobreescribir_reemplaza_campos_y_acuerdos_sin_seguimiento`.
  - `Acuerdos_con_seguimiento_no_se_reemplazan_ni_sobreescribiendo` — un acuerdo con `Estado = EnProceso` (o el valor no-Pendiente que tenga el enum) → `DomainException` «*seguimiento*», con `Sobreescribir = true`, y el acuerdo sigue ahí.
  - `El_bloque_de_capacitacion_solo_en_capacitacion` — `Tipo = "Taller"` + `convocados` → `Convocados` sin cambio.
  - `Reunion_inexistente` → `NotFoundException`.
  - `Llenar_no_borra_asistentes_ni_adjuntos` — reunión con un pre-registrado → sigue ahí tras reemplazar acuerdos.
- [ ] **Step 2: Run, see them fail** (`--filter "FullyQualifiedName~LlenarMemoriaReunionCommandTests"`).
- [ ] **Step 3: Implement** según «Comportamiento».
- [ ] **Step 4: Run, see them pass**; luego `dotnet test tests\Application.Tests` y `tests\Domain.Tests`.
- [ ] **Step 5: Commit** — `"La memoria interpretada se escribe en la reunion sin tocar Generales ni acuerdos en seguimiento"`.

---

### Task 3: Outlook no actualiza reuniones que ya terminaron

**Files:**
- Modify: `src/Application/Reuniones/Outlook/OutlookCommands.cs` (`SincronizarReunionOutlookCommandHandler`)
- Modify: `tests/Application.Tests/Reuniones/Outlook/OutlookCommandsTests.cs`
- Modify: `tests/Web.Tests/OutlookPaginasTests.cs` (y cualquier otra prueba Web que siembre reuniones enviadas a Outlook)

**Interfaces:**
- `SincronizarReunionOutlookCommandHandler(IApplicationDbContext ctx, IReunionRepository repo, IOutlookCalendario outlook, IUnitOfWork uow, TimeProvider reloj)` — `TimeProvider` ya está registrado (`services.TryAddSingleton(TimeProvider.System)` en Infrastructure/DependencyInjection.cs).

Regla: después de construir el evento y **antes** de comparar la huella, si `evento.Fin` (hora local de Honduras, Kind Unspecified) es anterior a «ahora en Honduras», no se llama a Graph, no se marca pendiente y no se guarda nada: `return Unit.Value`. «Ahora en Honduras» = `TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), zona).DateTime` con `zona` = `FindSystemTimeZoneById("Central America Standard Time")`, y si no existe `"America/Tegucigalpa"`, y si tampoco, desfase fijo UTC−6 (Honduras no tiene horario de verano). Comentario que diga por qué: llenar la memoria después de la reunión cambia `ObjetivoAgenda`, que va en el cuerpo de la cita, y cada PATCH avisa «reunión actualizada» a todos los invitados.

`Enviar` no cambia; `Cancelar` no cambia (borrar sigue cancelando).

- [ ] **Step 1: Tests**
  - En `OutlookCommandsTests`: agregar una clase anidada `RelojFijo(DateTimeOffset ahora) : TimeProvider { public override DateTimeOffset GetUtcNow() => ahora; }`; el helper `Sincronizar()` pasa `new RelojFijo(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero))` (las reuniones de esas pruebas son del 2026-10-23: futuras respecto del reloj). Todas las pruebas existentes deben seguir pasando sin otro cambio.
  - Nueva: `Una_reunion_que_ya_termino_no_se_actualiza_en_Outlook` — reunión enviada, reloj en `2026-10-24T12:00Z`, cambiar `Hora` → Graph no recibe `ActualizarAsync`, `OutlookPendiente` sigue `false`.
  - Nueva: `Moverla_al_futuro_si_la_actualiza` — reloj `2026-10-24T12:00Z`, mover `Fecha` a `2026-10-30` → `ActualizarAsync` recibido una vez.
  - En `tests/Web.Tests/OutlookPaginasTests.cs`: las reuniones sembradas con fecha fija (`new DateOnly(2026, 10, 23)` y similares) pasan a `DateOnly.FromDateTime(DateTime.Today.AddDays(30))`. Buscar con `git grep -n "DateOnly(2026" -- tests/Web.Tests` todas las que intervienen en pruebas de sincronización.
- [ ] **Step 2: Run** — la nueva `Una_reunion_que_ya_termino…` falla; las demás pasan.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `tests\Application.Tests` y `tests\Web.Tests` completos — todo verde.
- [ ] **Step 5: Commit** — `"Una reunion que ya termino no se actualiza en Outlook: llenar la memoria no avisa a los invitados"`.

---

### Task 4: Las herramientas del MCP

**Files:**
- Create: `src/Web/Mcp/ReunionesMemoriaTools.cs`
- Modify: `src/Web/Program.cs` (junto a `.WithTools<…VinculadosTools>()`)
- Test: `tests/Web.Tests/McpMemoriaReunionTests.cs`

**Interfaces:**
- Consumes: `GetReunionByIdQuery` (devuelve `Datos.Tipo`, `Datos.EncuestaActiva` — verificar nombres en `ReunionFormDto`), `MemoriaDesdeTranscripcion`, `LlenarMemoriaReunionCommand`, `McpPermisos`.
- Produces:
  ```csharp
  [McpServerToolType]
  public sealed class ReunionesMemoriaTools(ISender sender, McpPermisos permisos)
  {
      [McpServerTool(Name = "instrucciones_memoria_reunion")]
      // → { reunionId, titulo, tipo, esCapacitacion, encuestaActiva, prompt, formato, campos[] }
      [McpServerTool(Name = "llenar_memoria_reunion")]
      // (int id, string json, bool sobreescribir = false) → { mensaje }  ; DomainException → mensaje de error legible
  }
  ```

Seguir exactamente el patrón de `src/Web/Mcp/VinculadosTools.cs` y `ProyectosTools.cs`: `[Description]` en español en la clase, cada método y cada parámetro; `permisos.ExigirAsync("Reuniones.Ver")` para instrucciones; `permisos.ExigirEscrituraAsync("Reuniones.Editar")` para llenar; reunión fuera de alcance → `McpPermisoDenegadoException($"No se encontró la reunión {id}, o su cuenta no la alcanza.")` como hace `ProyectosTools`. Descripción de `llenar_memoria_reunion` que le diga a la IA: primero pedir `instrucciones_memoria_reunion`, redactar el JSON con esas reglas a partir de la transcripción que le dio la persona, y no usar `sobreescribir` salvo que la persona lo pida. `llenar` devuelve `MemoriaDesdeTranscripcion.Interpretar(...).Mensaje` tras el comando.

- [ ] **Step 1: Tests** (patrón `tests/Web.Tests/McpEstructuraProyectoTests.cs`: llamar a los mismos `ISender`/herramienta desde un scope del `PortalFactory`; si instanciar la herramienta necesita `McpPermisos` con un usuario, seguir cómo lo hacen las pruebas MCP existentes — `git grep -n "McpPermisos" -- tests/Web.Tests`):
  - `Las_instrucciones_dependen_del_tipo_de_la_reunion` — capacitación con encuesta → `esCapacitacion` true y `formato` con `validador_institucion`.
  - `Llenar_por_el_MCP_escribe_y_devuelve_el_mensaje`.
  - `Sin_Reuniones_Editar_no_escribe` — rol sin la clave → excepción de permiso y nada escrito.
- [ ] **Step 2-4:** fallar, implementar (incluye el registro en Program.cs), pasar; correr `tests\Web.Tests` completo.
- [ ] **Step 5: Commit** — `"El MCP llena la ayuda memoria de una reunion con las reglas del portal"`.

---

### Task 5: El editor usa las reglas del servidor

**Files:**
- Modify: `src/Web/Pages/Reuniones/Editor.cshtml.cs` (handler nuevo + datos para la vista)
- Modify: `src/Web/Pages/Reuniones/Editor.cshtml` (JavaScript del panel «Llenar automáticamente…»)
- Test: `tests/Web.Tests/MemoriaEditorTests.cs`

**Interfaces:**
- `EditorModel.VariantesMemoria` : `IReadOnlyDictionary<string, InstruccionesMemoria>` con las cuatro variantes, claves `"cap=0|enc=0"`, `"cap=0|enc=1"`, `"cap=1|enc=0"`, `"cap=1|enc=1"`.
- `public IActionResult OnPostInterpretarMemoria([FromForm] string json, [FromForm] bool esCapacitacion, [FromForm] bool conEncuesta)` → `JsonResult` con la `MemoriaPropuesta` (camelCase) o `{ error: "<mensaje>" }` con 400 si `DomainException`. No toca la base. Hereda `[Permission("Reuniones", AccionModulo.Editar)]` de la clase.

Cambios en el JavaScript (todo lo demás del panel queda igual):
- Borrar `CW_CAMPOS_*`, `CW_BLOQUE_*`, `CW_REGLAS_*`, `cwPrompt`, `cwFormato`, `cwLineas`; en su lugar `var CW_VARIANTES = @Html.Raw(JsonSerializer.Serialize(Model.VariantesMemoria, camelCase));` y `coworkRender()` toma `CW_VARIANTES['cap=' + (esCap?1:0) + '|enc=' + (conEncuesta?1:0)]` para el badge (`Variante`), el prompt, el formato y la lista de campos (mismo marcado que hoy).
- `aplicarJsonCowork(sobreescribir)` pasa a `async`: `fetch('?handler=InterpretarMemoria', { method: 'POST', headers: { 'RequestVerificationToken': csrf }, body: FormData(json, esCapacitacion, conEncuesta) })` con `csrf` leído del meta `csrf-token` del layout (CLAUDE.md); con la respuesta: el mismo chequeo de conflictos de hoy (contra los valores propuestos, mismos nombres de campo `Datos.*`, `dev-val`, `#acuerdos-list .acuerdo-item`), el mismo llenado (inputs, `dev-rte` + `syncDev()`, `addAcuerdo()` por acuerdo + `actualizarResponsables()`), y el mensaje = `Mensaje` de la respuesta. Error 400 → `errBox` con `error`; fallo de red → `errBox` «No se pudo interpretar el JSON. Vuelva a intentarlo.».
- Mapeo propuesta → campo: `objetivo`→`Datos.ObjetivoAgenda`, `documentos`→`Datos.DocsRecursos`, `validadorDiger`→`Datos.ValDiger`, `validadorInstitucion`→`Datos.ValInst`, `compromisos`→`Datos.Compromisos`, `convocados`→`Datos.Convocados`, `numAsistentes`→`Datos.NumAsistentes`, `pctAsistencia`→`Datos.PctAsistencia`; `desarrollo`→RTE; `acuerdos[]`→ `compromiso`, `plazo` (yyyy-MM-dd), `responsable` (al `dataset.selected` del select, como hoy).

- [ ] **Step 1: Tests** (`PortalFactory`, rol con `Reuniones.Editar`, patrón de `OutlookPaginasTests` para token antiforgery):
  - `Interpretar_devuelve_la_propuesta_sin_tocar_la_base` — POST con JSON de reunión → 200, JSON con `objetivo` y `mensaje`; la reunión en la base sin cambios.
  - `Un_json_invalido_devuelve_400_con_el_motivo`.
  - `El_editor_trae_las_cuatro_variantes` — el HTML del editor contiene `CW_VARIANTES` y el encabezado de capacitación.
- [ ] **Step 2-4:** fallar, implementar, pasar; `tests\Web.Tests` completo.
- [ ] **Step 5: Commit** — `"El editor interpreta el JSON de la memoria con las reglas del servidor"`.
- [ ] **Step 6 (controlador, no el implementador):** verificación en navegador real del panel: cambiar Tipo a Capacitación y encender la encuesta cambia el prompt; «Aplicar al formulario» con un JSON de ejemplo llena los campos y acuerdos; «Sobreescribir» con campos llenos; JSON inválido muestra el error.

---

### Task 6: Documentación

**Files:** `docs/superpowers/specs/2026-10-22-reuniones-a-outlook-design.md` (fase 2: parte A hecha, las cuatro decisiones, la regla de reunión terminada en la fase 1), `CLAUDE.md` (una línea en «Reuniones a Outlook»: las reglas de la memoria viven en `MemoriaDesdeTranscripcion`; no reescribirlas en el JavaScript; y la regla de reunión terminada).

- [ ] Editar, commit `"Fase 2 parte A en la spec y en CLAUDE.md"`.
