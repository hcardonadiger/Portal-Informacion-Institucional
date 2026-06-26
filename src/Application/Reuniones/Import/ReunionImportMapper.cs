using System.Globalization;
using System.Text.Json;
using Diger.TramitesEstado.Application.Reuniones.Common;

namespace Diger.TramitesEstado.Application.Reuniones.Import;

/// <summary>Traduce una <see cref="ReunionImportRow"/> (incl. el blob JSON <c>tema</c>)
/// a las formas que consume el módulo de Reuniones (<see cref="ReunionFormDto"/> + hijos).</summary>
public static class ReunionImportMapper
{
    public static (ReunionFormDto datos, List<AsistenteInput> asistentes, List<AcuerdoInput> acuerdos, string? institucionNombre)
        ToForm(ReunionImportRow row)
    {
        using var doc = ParseTema(row.Tema);
        var t   = doc?.RootElement;
        var cap = t is { } te && te.TryGetProperty("cap", out var c) && c.ValueKind == JsonValueKind.Object
            ? c : (JsonElement?)null;

        var modalidad   = Coalesce(Str(t, "modalidad"), Str(t, "modalidad_otro"));
        var tipo        = Coalesce(Str(t, "tipo_cap"), Str(t, "tipo_cap_otro"));
        var instNombre  = Str(t, "institucion_beneficiaria");

        var datos = new ReunionFormDto
        {
            Titulo    = (row.Titulo ?? "").Trim() is { Length: > 0 } tt ? Cut(tt, 250) : "(sin título)",
            Fecha     = ParseDate(row.Fecha),
            Hora      = Cut(row.Hora, 20),
            Lugar     = Cut(row.Lugar, 250),
            Modalidad = Cut(modalidad, 40),
            Tipo      = Cut(tipo, 60),

            EsCapacitacionPlataforma = Bool(cap, "es_cap_plataforma"),

            ObjetivoAgenda = Cut(Str(cap, "mem_agenda"),    4000),
            Desarrollo     = Cut(Str(cap, "mem_desarrollo"), 4000),

            Tema        = Cut(Str(cap, "tema"),     250),
            ObjetivoCap = Cut(Str(cap, "objetivo"), 2000),
            Contenido   = Cut(JoinLines(cap, "contenido"), 4000),

            EpNombre = Cut(Str(cap, "ep_nombre"), 150),
            EpCargo  = Cut(Str(cap, "ep_cargo"),  150),
            EpCorreo = Cut(Str(cap, "ep_correo"), 200),
            EpTel    = Cut(Str(cap, "ep_tel"),    40),

            FacNombre = Cut(Str(cap, "fac_nombre"), 150),
            FacCargo  = Cut(Str(cap, "fac_cargo"),  150),
            FacCorreo = Cut(Str(cap, "fac_correo"), 200),

            Convocados    = ParseInt(Str(cap, "r_convocados")),
            NumAsistentes = ParseInt(Str(cap, "r_asistentes")),
            PctAsistencia = ParsePct(Str(cap, "r_pct")),
            Satisfaccion  = Cut(Str(cap, "r_satisfaccion"), 60),
            Compromisos   = Cut(JoinLines(cap, "compromisos"), 4000),

            ValDiger     = Cut(Str(cap, "val_diger"), 200),
            ValInst      = Cut(Str(cap, "val_inst"),  200),
            DocsRecursos = Cut(Str(cap, "docs_recursos"), 4000),
            Foto1Url     = Cut(Str(cap, "foto1"),      600),
            Foto1Desc    = Cut(Str(cap, "foto1_desc"), 300),
            Foto2Url     = Cut(Str(cap, "foto2"),      600),
            Foto2Desc    = Cut(Str(cap, "foto2_desc"), 300),
        };

        var acuerdos = new List<AcuerdoInput>();
        if (cap is { } cc && cc.TryGetProperty("mem_acuerdos", out var ma) && ma.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in ma.EnumerateArray())
            {
                var compromiso = Str(a, "compromiso");
                if (string.IsNullOrWhiteSpace(compromiso)) continue;
                acuerdos.Add(new AcuerdoInput
                {
                    Compromiso  = compromiso!.Trim(),
                    Responsable = Str(a, "responsable")?.Trim(),
                    Plazo       = ParseDate(Str(a, "plazo")),
                });
            }
        }

        var asistentes = row.Asistencias
            .Where(x => !string.IsNullOrWhiteSpace(x.Nombre))
            .Select(x => new AsistenteInput
            {
                Nombre      = x.Nombre.Trim(),
                Cargo       = x.Cargo?.Trim(),
                Institucion = x.Institucion?.Trim(),
                Correo      = x.Correo?.Trim(),
                Telefono    = x.Telefono?.Trim(),
            })
            .ToList();

        return (datos, asistentes, acuerdos, string.IsNullOrWhiteSpace(instNombre) ? null : instNombre!.Trim());
    }

    // ── Parsing helpers ───────────────────────────────────────────────────

    static JsonDocument? ParseTema(string? tema)
    {
        if (string.IsNullOrWhiteSpace(tema)) return null;
        try { return JsonDocument.Parse(tema); }
        catch (JsonException) { return null; }
    }

    static string? Str(JsonElement? e, string prop)
    {
        if (e is { } el && el.ValueKind == JsonValueKind.Object &&
            el.TryGetProperty(prop, out var v))
        {
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.ToString(),
                JsonValueKind.True   => "true",
                JsonValueKind.False  => "false",
                _ => null,
            };
        }
        return null;
    }

    static bool Bool(JsonElement? e, string prop)
    {
        if (e is { } el && el.ValueKind == JsonValueKind.Object &&
            el.TryGetProperty(prop, out var v))
            return v.ValueKind == JsonValueKind.True ||
                   (v.ValueKind == JsonValueKind.String && bool.TryParse(v.GetString(), out var b) && b);
        return false;
    }

    static string? JoinLines(JsonElement? e, string prop)
    {
        if (e is { } el && el.ValueKind == JsonValueKind.Object &&
            el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Array)
        {
            var lines = v.EnumerateArray()
                .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString())
                .Select(x => x?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x));
            var joined = string.Join("\n", lines);
            return joined.Length == 0 ? null : joined;
        }
        return null;
    }

    static DateOnly? ParseDate(string? s) =>
        DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    static int? ParseInt(string? s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;

    /// <summary>"100.0%" → 100 ; "85%" → 85 ; "0" → 0.</summary>
    static int? ParsePct(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var clean = s.Replace("%", "").Trim();
        return double.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
            ? (int)Math.Round(d) : null;
    }

    static string? Coalesce(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) ? a : (!string.IsNullOrWhiteSpace(b) ? b : null);

    static string? Cut(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        return s.Length <= max ? s : s[..max];
    }
}
