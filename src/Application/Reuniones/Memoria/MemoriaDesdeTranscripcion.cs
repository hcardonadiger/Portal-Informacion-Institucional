using System.Globalization;
using System.Text.Json;

namespace Diger.TramitesEstado.Application.Reuniones.Memoria;

/// <summary>Una clave del JSON que se le pide a la IA y la etiqueta del campo donde cae.</summary>
public sealed record CampoMemoria(string Clave, string Etiqueta);

/// <summary>Lo que se le entrega a la IA para una variante: el prompt, el formato y los campos que llena.</summary>
public sealed record InstruccionesMemoria(string Variante, string Prompt, string Formato, IReadOnlyList<CampoMemoria> Campos);

/// <summary>Un acuerdo leído del JSON. Sin compromiso no hay acuerdo; el responsable va como texto.</summary>
public sealed record AcuerdoPropuesto(string Compromiso, string? Responsable, DateOnly? Plazo);

/// <summary>
/// Lo que el JSON trae para esta reunión, ya filtrado: un campo en <c>null</c> no se toca. Lo
/// descartado no está aquí, está dicho en <see cref="Mensaje"/>.
/// </summary>
public sealed record MemoriaPropuesta(
    string? Objetivo, string? Desarrollo, string? Documentos, string? ValidadorDiger, string? ValidadorInstitucion,
    int? Convocados, int? NumAsistentes, int? PctAsistencia, string? Compromisos,
    IReadOnlyList<AcuerdoPropuesto> Acuerdos, int CamposAplicables, string Mensaje);

/// <summary>
/// La ayuda memoria a partir de una transcripción: qué se le pide a la IA y cómo se lee lo que
/// devuelve. Única copia de estas reglas — la usan el editor (que llena el formulario) y el MCP
/// (que escribe la reunión). Hasta el 2026-10-23 vivían en el JavaScript del editor.
/// </summary>
/// <remarks>
/// «Generales» (título, fecha, tipo, lugar…) nunca se escribe desde el JSON: es la configuración
/// con la que se abre la reunión y la captura una persona. El bloque de capacitación solo cae en
/// una capacitación y la segunda firma solo con la encuesta encendida. Cada descarte se avisa.
/// </remarks>
public static class MemoriaDesdeTranscripcion
{
    public const string TipoCapacitacion = "Capacitación";

    private const string JsonInvalido = "El texto pegado no es un JSON válido.";

    public static bool EsCapacitacion(string? tipo) => tipo == TipoCapacitacion;

    // ── Textos: copiados de Editor.cshtml (CW_*). No reescribir: el prompt ya está probado. ──

    private static readonly CampoMemoria[] CamposMemoria =
    [
        new("objetivo",   "Memoria · Objetivo de la reunión"),
        new("desarrollo", "Memoria · Desarrollo (HTML simple)"),
        new("acuerdos[]", "Memoria · Acuerdos y compromisos"),
    ];

    private static readonly CampoMemoria[] CamposCap =
    [
        new("convocados",     "Capacitación · Convocados"),
        new("num_asistentes", "Capacitación · Nº asistentes"),
        new("pct_asistencia", "Capacitación · % asistencia"),
        new("compromisos",    "Capacitación · Compromisos"),
    ];

    private static readonly CampoMemoria[] CamposCierre =
    [
        new("documentos",      "Cierre · Documentos / recursos"),
        new("validador_diger", "Cierre · Validación DIGER"),
    ];

    // Solo con la encuesta encendida: es la única condición bajo la que el cierre pide la segunda
    // firma, así que pedírsela a la IA cuando no aplica sería pedir un dato que no tiene dónde caer.
    private static readonly CampoMemoria[] CamposEncuesta =
    [
        new("validador_institucion", "Cierre · Validación institución"),
    ];

    private static readonly string[] BloqueMemoria =
    [
        "  \"objetivo\": \"Objetivo principal de la sesión\",",
        "  \"desarrollo\": \"<p>Resumen del desarrollo…</p><ul><li>Punto tratado</li></ul>\",",
        "  \"acuerdos\": [",
        "    { \"compromiso\": \"Qué se hará\", \"responsable\": \"Nombre o institución\", \"plazo\": \"YYYY-MM-DD\" }",
        "  ],",
    ];

    private static readonly string[] BloqueCap =
    [
        "  \"convocados\": 30,",
        "  \"num_asistentes\": 24,",
        "  \"pct_asistencia\": 80,",
        "  \"compromisos\": [\"Compromiso de seguimiento posterior a la capacitación\"],",
    ];

    private static readonly string[] BloqueCierreConEncuesta =
    [
        "  \"documentos\": [\"Presentación institucional.pdf\", \"Lista de asistencia firmada\"],",
        "  \"validador_diger\": \"Nombre de quien valida por la DIGER\",",
        "  \"validador_institucion\": \"Nombre de quien valida por la institución\"",
    ];

    private static readonly string[] BloqueCierreSinEncuesta =
    [
        "  \"documentos\": [\"Presentación institucional.pdf\", \"Lista de asistencia firmada\"],",
        "  \"validador_diger\": \"Nombre de quien valida por la DIGER\"",
    ];

    private static readonly string[] ReglasComunes =
    [
        "1. No inventes nada. Si un dato no aparece en la transcripción, omite esa clave.",
        "2. NO incluyas titulo, fecha, hora, duracion, modalidad, tipo, institucion, lugar ni",
        "   visibilidad. Esos datos ya están capturados en el sistema y no deben modificarse;",
        "   si los devuelves, se descartan.",
        "3. \"desarrollo\" admite HTML simple (<p>, <ul>, <li>, <strong>, <em>). Redáctalo en",
        "   español, en tercera persona y en pasado: discusión, decisiones y conclusiones.",
        "4. \"acuerdos\" son compromisos concretos y accionables. \"responsable\" es el nombre de la",
        "   persona o institución tal como se menciona; \"plazo\" va en formato YYYY-MM-DD y se",
        "   omite si no se acordó una fecha.",
        "5. \"documentos\" lista los archivos, presentaciones o enlaces mencionados.",
        "6. Los \"validador_*\" son nombres de personas, no cargos ni instituciones. Omítelos si la",
        "   transcripción no dice quién valida.",
    ];

    private static readonly string[] ReglasCap =
    [
        "7. Esta actividad es una CAPACITACIÓN: llena también su bloque propio. NO devuelvas un",
        "   objetivo ni un contenido aparte para la capacitación: lo formativo va en \"objetivo\" y",
        "   los temas impartidos en \"desarrollo\", que es donde los lee el acta.",
        "8. \"convocados\" y \"num_asistentes\" son enteros; \"pct_asistencia\" es un entero de 0 a 100",
        "   y solo se calcula si tienes ambas cifras. \"compromisos\" son los acuerdos de",
        "   seguimiento posteriores a la capacitación (replicar, entregar material, evaluar).",
    ];

    // ── Claves que se descartan, en el orden en que el aviso las nombra (el de aplicarJsonCowork). ──

    private static readonly string[] ClavesGenerales =
        ["titulo", "fecha", "hora", "duracion", "modalidad", "tipo", "institucion", "instituciones", "lugar", "visibilidad"];

    private static readonly string[] ClavesSatisfaccion = ["satisfaccion", "satisfaccion_calificacion"];

    private static readonly string[] ClavesCapacitacionViejas = ["objetivo_capacitacion", "contenido"];

    public static InstruccionesMemoria Instrucciones(bool esCapacitacion, bool conEncuesta)
    {
        // cwFormato
        var formato = string.Join("\n", new[] { "{" }
            .Concat(BloqueMemoria)
            .Concat(esCapacitacion ? BloqueCap : [])
            .Concat(conEncuesta ? BloqueCierreConEncuesta : BloqueCierreSinEncuesta)
            .Append("}"));

        // cwPrompt
        var encabezado = esCapacitacion
            ? "Eres un asistente que redacta la memoria de una CAPACITACIÓN impartida por la DIGER."
            : "Eres un asistente que redacta la memoria de una reunión institucional de la DIGER.";
        var prompt = string.Join("\n", new[]
            {
                encabezado,
                "Te comparto la transcripción de la sesión. Devuelve ÚNICAMENTE un objeto JSON válido:",
                "sin texto antes ni después, sin bloques de código y sin comentarios, con esta forma:",
                "",
                formato,
                "",
                "Reglas:",
            }
            .Concat(ReglasComunes)
            .Concat(esCapacitacion ? ReglasCap : [])
            .Concat(["", "Transcripción:", "\"\"\"", "(pegue aquí la transcripción)", "\"\"\""]));

        // El mismo orden que la lista de coworkRender.
        var campos = CamposMemoria
            .Concat(esCapacitacion ? CamposCap : [])
            .Concat(CamposCierre)
            .Concat(conEncuesta ? CamposEncuesta : [])
            .ToList();

        return new InstruccionesMemoria(esCapacitacion ? TipoCapacitacion : "Reunión", prompt, formato, campos);
    }

    public static MemoriaPropuesta Interpretar(string json, bool esCapacitacion, bool conEncuesta)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new DomainException(JsonInvalido);

        JsonElement d;
        try
        {
            using var doc = JsonDocument.Parse(json);
            d = doc.RootElement.Clone();
        }
        catch (JsonException) { throw new DomainException(JsonInvalido); }
        if (d.ValueKind != JsonValueKind.Object) throw new DomainException(JsonInvalido);

        // Única lista de campos escribibles. «Generales» no aparece a propósito, ni la satisfacción:
        // de una transcripción no se deduce qué tan satisfechos quedaron los asistentes.
        var objetivo       = Texto(d, "objetivo");
        var documentos     = Lineas(d, "documentos");
        var validadorDiger = Texto(d, "validador_diger");
        var validadorInst  = conEncuesta ? Texto(d, "validador_institucion") : null;
        var compromisos    = esCapacitacion ? Lineas(d, "compromisos") : null;
        var convocados     = esCapacitacion ? Entero(d, "convocados") : null;
        var numAsistentes  = esCapacitacion ? Entero(d, "num_asistentes") : null;
        var pctAsistencia  = esCapacitacion ? Porcentaje(d, "pct_asistencia") : null;
        var desarrollo     = Texto(d, "desarrollo");
        var acuerdos       = Acuerdos(d);

        var aplicables = new object?[]
            {
                objetivo, documentos, validadorDiger, validadorInst, compromisos,
                convocados, numAsistentes, pctAsistencia, desarrollo,
            }
            .Count(v => v is not null) + acuerdos.Count;

        var msg = aplicables == 0
            ? "El JSON no traía ningún campo de Memoria, Capacitación ni Cierre."
            : "Se llenaron " + aplicables + " campo(s) de Memoria" + (esCapacitacion ? ", Capacitación" : "") + " y Cierre.";

        // Se avisa de lo descartado en vez de callarlo: así se ve por qué el JSON traía más de lo
        // que se llenó. Dos motivos distintos, dos avisos distintos: la satisfacción salió del cierre
        // para siempre, y la segunda firma solo no aplica mientras la encuesta esté apagada.
        msg += Aviso(d, ClavesGenerales, "«Generales» no se modifica desde el JSON.");
        if (!esCapacitacion)
            msg += Aviso(d, CamposCap.Select(c => c.Clave), "el tipo de esta reunión no es Capacitación.");
        msg += Aviso(d, ClavesSatisfaccion, "la satisfacción ya no se captura en el cierre.");
        msg += Aviso(d, ClavesCapacitacionViejas, "el detalle de capacitación se quitó; use «objetivo» y «desarrollo».");
        if (!conEncuesta)
            msg += Aviso(d, CamposEncuesta.Select(c => c.Clave), "la encuesta de esta reunión está apagada.");

        return new MemoriaPropuesta(
            objetivo, desarrollo, documentos, validadorDiger, validadorInst,
            convocados, numAsistentes, pctAsistencia, compromisos,
            acuerdos, aplicables, msg);
    }

    // ── Lectura del JSON ──

    /// <summary>Presente = existe y no es <c>null</c>, igual que <c>d[k] != null</c> en el JavaScript.</summary>
    private static bool Presente(JsonElement d, string clave) =>
        d.TryGetProperty(clave, out var v) && v.ValueKind != JsonValueKind.Null;

    private static string Aviso(JsonElement d, IEnumerable<string> claves, string motivo)
    {
        var presentes = claves.Where(k => Presente(d, k)).ToList();
        return presentes.Count == 0 ? "" : " Se ignoró " + string.Join(", ", presentes) + ": " + motivo;
    }

    /// <summary>Un texto, o un número/booleano como su texto (lo que haría <c>String(v)</c>). Vacío = no viene.</summary>
    private static string? Texto(JsonElement d, string clave) =>
        d.TryGetProperty(clave, out var v) ? Escalar(v) : null;

    private static string? Escalar(JsonElement v)
    {
        var s = v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => v.GetRawText(),
            _ => null,
        };
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    /// <summary>
    /// cwLineas: la IA devuelve listas y los campos son de «uno por línea». Un arreglo se une con
    /// saltos de línea sin sus elementos vacíos; un texto pasa tal cual.
    /// </summary>
    private static string? Lineas(JsonElement d, string clave)
    {
        if (!d.TryGetProperty(clave, out var v)) return null;
        if (v.ValueKind != JsonValueKind.Array) return Escalar(v);

        var lineas = v.EnumerateArray().Select(Escalar).Where(x => x is not null).ToList();
        return lineas.Count == 0 ? null : string.Join("\n", lineas);
    }

    /// <summary>
    /// Un entero no negativo, como número o como texto numérico («24»). La IA a veces escribe el
    /// mismo entero como «80.0», «3e1» o «"80%"»: valen. Una fracción («24.5»), un negativo o algo
    /// que no cabe en un <c>int</c> no son una cifra de asistencia y no se aplican.
    /// </summary>
    private static int? Entero(JsonElement d, string clave)
    {
        if (!d.TryGetProperty(clave, out var v)) return null;
        var x = 0d;
        var ok = v.ValueKind == JsonValueKind.Number
            ? v.TryGetDouble(out x)
            : v.ValueKind == JsonValueKind.String
              && double.TryParse(v.GetString()?.Trim().TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out x);
        return ok && x >= 0 && x <= int.MaxValue && x == Math.Floor(x) ? (int)x : null;
    }

    /// <summary>Un entero de 0 a 100; fuera de rango no se aplica.</summary>
    private static int? Porcentaje(JsonElement d, string clave) =>
        Entero(d, clave) is int n && n <= 100 ? n : null;

    /// <summary>Los acuerdos con compromiso; el plazo solo si es una fecha yyyy-MM-dd.</summary>
    private static List<AcuerdoPropuesto> Acuerdos(JsonElement d)
    {
        var acuerdos = new List<AcuerdoPropuesto>();
        if (!d.TryGetProperty("acuerdos", out var lista) || lista.ValueKind != JsonValueKind.Array) return acuerdos;

        foreach (var a in lista.EnumerateArray())
        {
            if (a.ValueKind != JsonValueKind.Object) continue;
            var compromiso = Texto(a, "compromiso");
            if (compromiso is null) continue;

            DateOnly? plazo = Texto(a, "plazo") is string p
                && DateOnly.TryParseExact(p.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f)
                    ? f : null;
            acuerdos.Add(new AcuerdoPropuesto(compromiso, Texto(a, "responsable"), plazo));
        }
        return acuerdos;
    }
}
