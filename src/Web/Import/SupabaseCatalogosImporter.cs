using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Import;

public sealed record ImportarCatalogosResult(
    int InstitucionesCreadas,  int InstitucionesOmitidas,
    int LevantamientosCreados, int LevantamientosOmitidos,
    int EventosCreados,        int EventosOmitidos,
    IReadOnlyList<string> Errores);

/// <summary>
/// Trae al portal las colecciones JSON de <c>diger_tram</c> que no cubrían los importadores
/// previos: catálogo de <c>instituciones</c>, <c>levantamientos_estado</c> y los eventos de
/// <c>cal_digitalizacion</c>/<c>cal_proyectos</c> (que entran como reuniones).
/// Es idempotente: lo ya traído se omite.
/// </summary>
public sealed class SupabaseCatalogosImporter(
    HttpClient http,
    IConfiguration config,
    IApplicationDbContext ctx,
    IInstitucionRepository instRepo)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <summary>Entradas del catálogo del demo que no representan una institución real.</summary>
    public static readonly HashSet<string> NoInstituciones =
        new(StringComparer.OrdinalIgnoreCase) { "N/A", "OTRA", "OTRO", "NA", "-" };

    /// <summary>El demo escribe algunas instituciones con su nombre largo, pero en el portal ya
    /// existen con su sigla. Se mapean para no duplicar el catálogo.</summary>
    public static readonly Dictionary<string, string> AliasInstituciones = new(StringComparer.OrdinalIgnoreCase)
    {
        ["INSTITUTO DE LA PROPIEDAD"]              = "IP",
        ["AGENCIA DE REGULACIÓN SANITARIA (ARSA)"] = "ARSA",
        ["SRECI (CANCILLERÍA)"]                    = "SRECI",
        ["INSTITUTO HONDUREÑO DE TURISMO"]         = "IHT",
    };

    public async Task<ImportarCatalogosResult> ImportarAsync(CancellationToken ct = default)
    {
        var colecciones = await LeerColeccionesAsync(ct);
        var errores = new List<string>();

        var (instCre, instOmi) = await ImportarInstitucionesAsync(Coleccion(colecciones, "instituciones"), errores, ct);
        var (levCre, levOmi)   = await ImportarLevantamientosAsync(Coleccion(colecciones, "levantamientos_estado"), errores, ct);

        var evtCre = 0; var evtOmi = 0;
        foreach (var (clave, tipo) in new[] { ("cal_digitalizacion", "Reunión técnica"), ("cal_proyectos", "Taller") })
        {
            var (c, o) = await ImportarEventosAsync(Coleccion(colecciones, clave), tipo, errores, ct);
            evtCre += c; evtOmi += o;
        }

        await ctx.SaveChangesAsync(ct);

        return new ImportarCatalogosResult(instCre, instOmi, levCre, levOmi, evtCre, evtOmi, errores);
    }

    // ── instituciones → Institucion ────────────────────────────────────────────
    private async Task<(int creadas, int omitidas)> ImportarInstitucionesAsync(
        JsonElement value, List<string> errores, CancellationToken ct)
    {
        if (value.ValueKind != JsonValueKind.Array) return (0, 0);
        int creadas = 0, omitidas = 0;

        foreach (var el in value.EnumerateArray())
        {
            var nombre = el.ValueKind == JsonValueKind.String ? el.GetString()?.Trim() : null;
            if (string.IsNullOrWhiteSpace(nombre) || NoInstituciones.Contains(nombre)) { omitidas++; continue; }

            // Si es un alias de una institución que ya existe con su sigla, no se duplica.
            if (AliasInstituciones.TryGetValue(nombre, out var canonico)
                && await instRepo.GetByNombreAsync(canonico, ct) is not null)
            {
                omitidas++;
                continue;
            }

            // El Id sólo admite A-Z, 0-9, '-' y '_': hay que quitar acentos antes de filtrar
            // (char.IsLetterOrDigit acepta 'Í'/'Ó' y el dominio los rechaza).
            var id = GenerarId(nombre);
            if (string.IsNullOrWhiteSpace(id)) { omitidas++; continue; }

            if (await instRepo.GetByNombreAsync(nombre, ct) is not null
                || await instRepo.GetByIdAsync(id, ct) is not null)
            {
                omitidas++;
                continue;
            }

            try
            {
                await instRepo.AddAsync(Institucion.Crear(id, nombre), ct);
                await ctx.SaveChangesAsync(ct); // evita choques de Id dentro del mismo lote
                creadas++;
            }
            catch (Exception ex) { errores.Add($"[institución {nombre}] {ex.Message}"); }
        }
        return (creadas, omitidas);
    }

    // ── levantamientos_estado → Levantamiento (+ equipo y checklist) ───────────
    private async Task<(int creados, int omitidos)> ImportarLevantamientosAsync(
        JsonElement value, List<string> errores, CancellationToken ct)
    {
        if (value.ValueKind != JsonValueKind.Array) return (0, 0);
        int creados = 0, omitidos = 0;

        // El origen no tiene un id estable reutilizable, así que se identifica por institución + encargado.
        var existentes = await ctx.Levantamientos.AsNoTracking()
            .Select(l => new { l.Institucion, l.Encargado })
            .ToListAsync(ct);
        var claves = existentes
            .Select(x => Clave(x.Institucion, x.Encargado))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var el in value.EnumerateArray())
        {
            var inst = Txt(el, "inst");
            var enc  = Txt(el, "enc");
            if (string.IsNullOrWhiteSpace(inst) || string.IsNullOrWhiteSpace(enc)) { omitidos++; continue; }
            if (!claves.Add(Clave(inst, enc))) { omitidos++; continue; }

            try
            {
                var lev = new Levantamiento
                {
                    Institucion            = inst!,
                    Encargado              = enc!,
                    Correo                 = Txt(el, "cor"),
                    Celular                = Txt(el, "cel"),
                    Estado                 = MapEstado(Txt(el, "estado")),
                    ObsEstado              = Txt(el, "obsEstado"),
                    ObsGenerales           = Txt(el, "obsGen"),
                    MigradaSOL             = EsSi(Txt(el, "migrada")),
                    Personal               = EsSi(Txt(el, "personal")),
                    RequiereAcompanamiento = EsSi(Txt(el, "acomp")),
                };

                if (el.TryGetProperty("equipo", out var equipo) && equipo.ValueKind == JsonValueKind.Array)
                {
                    var orden = 0;
                    foreach (var m in equipo.EnumerateArray())
                    {
                        var nom = Txt(m, "nombre");
                        if (string.IsNullOrWhiteSpace(nom)) continue;
                        lev.Equipo.Add(new MiembroEquipo
                        {
                            Nombre   = nom!.Trim(),
                            Funcion  = Txt(m, "funcion") ?? "—",
                            Contacto = Txt(m, "contacto"),
                            Orden    = orden++
                        });
                    }
                }

                if (el.TryGetProperty("tramites", out var trams) && trams.ValueKind == JsonValueKind.Array)
                {
                    var orden = 0;
                    foreach (var t in trams.EnumerateArray())
                    {
                        var nom = Txt(t, "nombre");
                        if (string.IsNullOrWhiteSpace(nom)) continue;
                        lev.Tramites.Add(new TramiteChecklist
                        {
                            NombreTramite    = nom!.Trim(),
                            Orden            = orden++,
                            ActaFirmada      = Bool(t, "acta"),
                            RequiereMejoras  = Bool(t, "mejoras"),
                            TieneInstructivo = Bool(t, "instructivo"),
                            Socializado      = Bool(t, "socializacion"),
                            Observaciones    = Txt(t, "obs")
                        });
                    }
                }

                ctx.Levantamientos.Add(lev);
                creados++;
            }
            catch (Exception ex) { errores.Add($"[levantamiento {inst}] {ex.Message}"); }
        }
        return (creados, omitidos);
    }

    // ── cal_* → Reunion ────────────────────────────────────────────────────────
    private async Task<(int creados, int omitidos)> ImportarEventosAsync(
        JsonElement value, string tipo, List<string> errores, CancellationToken ct)
    {
        if (value.ValueKind != JsonValueKind.Array) return (0, 0);
        int creados = 0, omitidos = 0;

        var yaImportados = await ctx.Reuniones.AsNoTracking()
            .Where(r => r.OrigenExternoId != null)
            .Select(r => r.OrigenExternoId!)
            .ToListAsync(ct);
        var externos = yaImportados.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var el in value.EnumerateArray())
        {
            var titulo = Txt(el, "titulo");
            if (string.IsNullOrWhiteSpace(titulo)) { omitidos++; continue; }

            // Prefijo "cal:" para no chocar con los ids de la tabla reuniones.
            var extId = $"cal:{Txt(el, "id") ?? titulo}";
            if (!externos.Add(extId)) { omitidos++; continue; }

            try
            {
                var r = Reunion.Crear(titulo!.Trim());
                r.OrigenExternoId = extId;
                r.Tipo = tipo;
                r.Hora = Txt(el, "hora");
                r.ObjetivoAgenda = Txt(el, "desc");
                if (DateOnly.TryParse(Txt(el, "fecha"), out var f)) r.Fecha = f;

                ctx.Reuniones.Add(r);
                creados++;
            }
            catch (Exception ex) { errores.Add($"[evento {titulo}] {ex.Message}"); }
        }
        return (creados, omitidos);
    }

    // ── Lectura del origen ─────────────────────────────────────────────────────
    private async Task<Dictionary<string, JsonElement>> LeerColeccionesAsync(CancellationToken ct)
    {
        var baseUrl = config["Supabase:Url"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("Falta configuración 'Supabase:Url'.");
        var apiKey = config["Supabase:AnonKey"]
            ?? throw new InvalidOperationException("Falta configuración 'Supabase:AnonKey'.");

        http.DefaultRequestHeaders.Remove("apikey");
        http.DefaultRequestHeaders.Add("apikey", apiKey);
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var filas = await http.GetFromJsonAsync<List<DigerTramRaw>>(
            $"{baseUrl}/rest/v1/diger_tram?select=key,value", JsonOpts, ct) ?? [];

        return filas.Where(f => !string.IsNullOrWhiteSpace(f.Key))
                    .ToDictionary(f => f.Key!, f => f.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static JsonElement Coleccion(Dictionary<string, JsonElement> d, string key) =>
        d.TryGetValue(key, out var v) ? v : default;

    // ── Helpers ────────────────────────────────────────────────────────────────
    private static string Clave(string? inst, string? enc) => $"{inst?.Trim()}|{enc?.Trim()}";

    /// <summary>Id de institución válido: mayúsculas sin acentos, sólo A-Z y 0-9.</summary>
    public static string GenerarId(string nombre)
    {
        var desc = nombre.Trim().ToUpperInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(desc.Length);
        foreach (var ch in desc)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (ch is >= 'A' and <= 'Z' or >= '0' and <= '9') sb.Append(ch);
        }
        return sb.ToString();
    }

    private static bool EsSi(string? s) =>
        s is not null && (s.Trim().StartsWith("S", StringComparison.OrdinalIgnoreCase)
                       || s.Trim().Equals("true", StringComparison.OrdinalIgnoreCase));

    /// <summary>El demo usa etiquetas libres; se traducen al enum del portal.</summary>
    private static EstadoLevantamientoExp MapEstado(string? estado)
    {
        var e = (estado ?? "").Trim();
        if (e.Contains("producción", StringComparison.OrdinalIgnoreCase)
         || e.Contains("produccion", StringComparison.OrdinalIgnoreCase)
         || e.Contains("Completo",   StringComparison.OrdinalIgnoreCase)) return EstadoLevantamientoExp.Completo;
        if (e.Contains("valid",    StringComparison.OrdinalIgnoreCase))   return EstadoLevantamientoExp.PendienteDeValidar;
        if (e.Contains("revisita", StringComparison.OrdinalIgnoreCase))   return EstadoLevantamientoExp.RequiereRevisita;
        return EstadoLevantamientoExp.EnProceso;
    }

    private static string? Txt(JsonElement el, string prop) =>
        el.ValueKind == JsonValueKind.Object
        && el.TryGetProperty(prop, out var v)
        && v.ValueKind == JsonValueKind.String
            ? (string.IsNullOrWhiteSpace(v.GetString()) ? null : v.GetString()!.Trim())
            : null;

    private static bool Bool(JsonElement el, string prop) =>
        el.ValueKind == JsonValueKind.Object
        && el.TryGetProperty(prop, out var v)
        && v.ValueKind == JsonValueKind.True;

    private sealed class DigerTramRaw
    {
        [JsonPropertyName("key")]   public string?     Key   { get; set; }
        [JsonPropertyName("value")] public JsonElement Value { get; set; }
    }
}
