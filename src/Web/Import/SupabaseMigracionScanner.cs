using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Diger.TramitesEstado.Application.Reuniones.Import;
using Diger.TramitesEstado.Web.Pages.Expedientes;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Import;

/// <summary>Una colección de origen en Supabase y su estado frente a lo ya migrado.</summary>
public sealed record FuenteMigracion(
    string Fuente,
    string Descripcion,
    int EnOrigen,
    int? YaMigrados,
    int? Pendientes,
    bool Soportado,
    string Nota);

public sealed record MigracionScan(IReadOnlyList<FuenteMigracion> Fuentes)
{
    /// <summary>Total pendiente de traer entre las fuentes que sí tienen importador.</summary>
    public int TotalPendiente => Fuentes.Where(f => f.Soportado).Sum(f => f.Pendientes ?? 0);
}

/// <summary>
/// Revisa las distintas tablas del portal demo (Supabase) y reporta qué información
/// falta por traer: la tabla relacional <c>reuniones</c>/<c>asistencias</c> y las
/// colecciones JSON guardadas en <c>diger_tram</c> (key/value).
/// </summary>
public sealed class SupabaseMigracionScanner(
    HttpClient http,
    IConfiguration config,
    IReunionImportSource reunionSource,
    IReunionRepository reunionRepo,
    IExpedienteRepository expedienteRepo,
    IInstitucionRepository instRepo,
    IApplicationDbContext ctx)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <summary>Colecciones de <c>diger_tram</c> con importador, y a qué entidad van.</summary>
    private static readonly Dictionary<string, string> ConMapeo = new(StringComparer.OrdinalIgnoreCase)
    {
        ["instituciones"]         = "Catálogo de instituciones → Institucion.",
        ["levantamientos_estado"] = "Levantamientos de campo → Levantamiento (+ equipo y checklist).",
        ["cal_digitalizacion"]    = "Eventos del calendario de digitalización → Reunion.",
        ["cal_proyectos"]         = "Eventos del calendario de proyectos → Reunion.",
    };

    /// <summary>Colecciones que por decisión no se migran.</summary>
    private static readonly Dictionary<string, string> Descartadas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tram"]         = "Avance de etapas del demo: sus ids no corresponden a expedientes del portal.",
        ["diger_access"] = "Accesos del demo: los usuarios se dan de alta desde Administración.",
    };

    public async Task<MigracionScan> RevisarAsync(CancellationToken ct = default)
    {
        var (baseUrl, apiKey) = LeerConfig();
        PrepararHeaders(apiKey);

        var fuentes = new List<FuenteMigracion>();

        // ── Reuniones + asistencias (tabla relacional) ─────────────────────────
        var reuniones = await reunionSource.ObtenerReunionesAsync(ct);
        var yaReuniones = await reunionRepo.GetOrigenExternoIdsAsync(ct);
        var pendReuniones = reuniones.Count(r => !yaReuniones.Contains(r.Id));

        fuentes.Add(new FuenteMigracion(
            "reuniones", "Reuniones del portal demo (con sus asistencias).",
            reuniones.Count, reuniones.Count - pendReuniones, pendReuniones, true,
            pendReuniones == 0 ? "Todo migrado." : $"{pendReuniones} por traer."));

        fuentes.Add(new FuenteMigracion(
            "asistencias", "Asistentes ligados a cada reunión (se traen junto con la reunión).",
            reuniones.Sum(r => r.Asistencias.Count), null, null, true,
            "Se importan dentro de cada reunión."));

        // ── Colecciones JSON en diger_tram ─────────────────────────────────────
        var filas = await http.GetFromJsonAsync<List<DigerTramRaw>>(
            $"{baseUrl}/rest/v1/diger_tram?select=key,value", JsonOpts, ct) ?? [];

        foreach (var fila in filas.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(fila.Key)) continue;
            var total = ContarElementos(fila.Value);

            if (string.Equals(fila.Key, "expedientes", StringComparison.OrdinalIgnoreCase))
            {
                var yaExp = await expedienteRepo.GetOrigenExternoIdsAsync(ct);
                var expedientes = Deserializar(fila.Value);
                var pend = expedientes.Count(o => !yaExp.Contains($"{o.Inst?.Trim()}|{o.Ts}"));

                fuentes.Add(new FuenteMigracion(
                    "diger_tram → expedientes", "Expedientes de digitalización (blob JSON).",
                    expedientes.Count, expedientes.Count - pend, pend, true,
                    pend == 0 ? "Todo migrado." : $"{pend} por traer."));
                continue;
            }

            if (Descartadas.TryGetValue(fila.Key, out var motivo))
            {
                fuentes.Add(new FuenteMigracion(
                    $"diger_tram → {fila.Key}", motivo, total, null, null, false, "No se migra (decisión)."));
                continue;
            }

            if (ConMapeo.TryGetValue(fila.Key, out var destino))
            {
                var pend = await ContarPendientesAsync(fila.Key, fila.Value, ct);
                fuentes.Add(new FuenteMigracion(
                    $"diger_tram → {fila.Key}", destino, total, total - pend, pend, true,
                    pend == 0 ? "Todo migrado." : $"{pend} por traer."));
                continue;
            }

            fuentes.Add(new FuenteMigracion(
                $"diger_tram → {fila.Key}", "Colección no reconocida.", total, null, null, false,
                "Sin importador — requiere definir el mapeo."));
        }

        return new MigracionScan(fuentes);
    }

    /// <summary>Cuántos elementos de la colección aún no existen en el portal.</summary>
    private async Task<int> ContarPendientesAsync(string clave, JsonElement value, CancellationToken ct)
    {
        if (value.ValueKind != JsonValueKind.Array) return 0;

        if (clave.Equals("instituciones", StringComparison.OrdinalIgnoreCase))
        {
            var actuales = (await instRepo.GetAllAsync(ct))
                .Select(i => i.Nombre).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return value.EnumerateArray()
                .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString()?.Trim() : null)
                .Count(n => !string.IsNullOrWhiteSpace(n)
                         && !SupabaseCatalogosImporter.NoInstituciones.Contains(n!)
                         && !actuales.Contains(n!)
                         && !(SupabaseCatalogosImporter.AliasInstituciones.TryGetValue(n!, out var can)
                              && actuales.Contains(can)));
        }

        if (clave.Equals("levantamientos_estado", StringComparison.OrdinalIgnoreCase))
        {
            var existentes = (await ctx.Levantamientos.AsNoTracking()
                    .Select(l => new { l.Institucion, l.Encargado }).ToListAsync(ct))
                .Select(x => $"{x.Institucion?.Trim()}|{x.Encargado?.Trim()}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return value.EnumerateArray()
                .Count(e => !existentes.Contains($"{Prop(e, "inst")}|{Prop(e, "enc")}"));
        }

        // cal_digitalizacion / cal_proyectos → reuniones con OrigenExternoId "cal:<id>"
        var externos = (await ctx.Reuniones.AsNoTracking()
                .Where(r => r.OrigenExternoId != null)
                .Select(r => r.OrigenExternoId!).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return value.EnumerateArray()
            .Count(e => !externos.Contains($"cal:{Prop(e, "id") ?? Prop(e, "titulo")}"));
    }

    private static string? Prop(JsonElement el, string prop) =>
        el.ValueKind == JsonValueKind.Object
        && el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()?.Trim() : null;

    private (string baseUrl, string apiKey) LeerConfig()
    {
        var baseUrl = config["Supabase:Url"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("Falta configuración 'Supabase:Url'.");
        var apiKey = config["Supabase:AnonKey"]
            ?? throw new InvalidOperationException("Falta configuración 'Supabase:AnonKey'.");
        return (baseUrl, apiKey);
    }

    private void PrepararHeaders(string apiKey)
    {
        http.DefaultRequestHeaders.Remove("apikey");
        http.DefaultRequestHeaders.Add("apikey", apiKey);
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
    }

    /// <summary>Cuenta elementos de una colección JSON (array = longitud; objeto = nº de claves).</summary>
    private static int ContarElementos(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Array  => value.GetArrayLength(),
        JsonValueKind.Object => value.EnumerateObject().Count(),
        JsonValueKind.Undefined or JsonValueKind.Null => 0,
        _ => 1
    };

    private static List<OriginalExpedienteDto> Deserializar(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array) return [];
        try
        {
            return value.Deserialize<List<OriginalExpedienteDto>>(JsonOpts) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed class DigerTramRaw
    {
        [JsonPropertyName("key")]   public string?     Key   { get; set; }
        [JsonPropertyName("value")] public JsonElement Value { get; set; }
    }
}
