using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Diger.TramitesEstado.Web.Pages.Expedientes;

namespace Diger.TramitesEstado.Web.Import;

public sealed record ImportarExpedientesResult(
    int Total, int Creados, int Omitidos, IReadOnlyList<string> Errores);

/// <summary>Importa los expedientes de digitalización del portal demo (Supabase) hacia SQL Server.
/// La data vive como un blob JSON bajo <c>diger_tram.key = 'expedientes'</c> (array con la forma
/// <see cref="OriginalExpedienteDto"/>), por lo que se reutiliza el pipeline existente:
/// <c>OriginalShapeMapper.ToInput → CrearExpedienteCommand</c>. Idempotente por
/// <c>Expediente.OrigenExternoId</c> (= <c>inst|_ts</c>).</summary>
public sealed class SupabaseExpedienteImporter(
    HttpClient http,
    IConfiguration config,
    ISender sender,
    IInstitucionRepository institucionRepo,
    IExpedienteRepository expedienteRepo)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<ImportarExpedientesResult> ImportarAsync(CancellationToken ct = default)
    {
        var baseUrl = config["Supabase:Url"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("Falta configuración 'Supabase:Url'.");
        var apiKey = config["Supabase:AnonKey"]
            ?? throw new InvalidOperationException("Falta configuración 'Supabase:AnonKey'.");

        http.DefaultRequestHeaders.Remove("apikey");
        http.DefaultRequestHeaders.Add("apikey", apiKey);
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var wrappers = await http.GetFromJsonAsync<List<DigerTramRow>>(
            $"{baseUrl}/rest/v1/diger_tram?key=eq.expedientes&select=value", JsonOpts, ct) ?? [];
        var expedientes = wrappers.FirstOrDefault()?.Value ?? [];

        var existentes   = await expedienteRepo.GetOrigenExternoIdsAsync(ct);
        var instituciones = await institucionRepo.GetAllActivasAsync(ct);

        var errores = new List<string>();
        int creados = 0, omitidos = 0;

        foreach (var o in expedientes)
        {
            var extId = $"{o.Inst?.Trim()}|{o.Ts}";

            if (existentes.Contains(extId))
            {
                omitidos++;
                continue;
            }

            try
            {
                var inst = instituciones.FirstOrDefault(i =>
                    string.Equals(i.Nombre, o.Inst?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (inst is null)
                {
                    errores.Add($"[{o.Inst}] institución no encontrada en el catálogo; se omite.");
                    continue;
                }

                var input = OriginalShapeMapper.ToInput(o, inst.Id);

                // El formulario original del demo no limpiaba la ficha al cambiar de trámite,
                // por lo que cada trámite quedó con la ficha del primero (datos arrastrados).
                // Se vacían los campos de ficha idénticos al trámite anterior (se conserva
                // nombre, requisitos y flujos, que sí son propios de cada trámite).
                input = input with { Tramites = DesduplicarFichas(input.Tramites) };

                // El origen puede traer el analista vacío (p. ej. expedientes cerrados);
                // el dominio/validador lo exige, así que se completa con un marcador.
                if (string.IsNullOrWhiteSpace(input.Analista))
                    input = input with { Analista = "(Sin asignar)" };

                await sender.Send(new CrearExpedienteCommand(input, extId), ct);

                existentes.Add(extId);
                creados++;
            }
            catch (Exception ex)
            {
                errores.Add($"[{o.Inst}] {ex.Message}");
            }
        }

        return new ImportarExpedientesResult(expedientes.Count, creados, omitidos, errores);
    }

    /// <summary>Vacía los campos de ficha de un trámite cuando son idénticos al trámite
    /// inmediatamente anterior (arrastre del formulario demo). Conserva el nombre.
    /// La comparación es contra el valor ORIGINAL del anterior, por lo que no se encadena.</summary>
    private static List<TramiteInput> DesduplicarFichas(List<TramiteInput> tramites)
    {
        var ord = tramites.OrderBy(t => t.TramiteIndex).ToList();
        var res = new List<TramiteInput>(ord.Count);
        for (var i = 0; i < ord.Count; i++)
        {
            if (i == 0) { res.Add(ord[i]); continue; }
            var t = ord[i];
            var p = ord[i - 1]; // valor original del anterior
            static string? Dedup(string? cur, string? prev) =>
                string.Equals((cur ?? "").Trim(), (prev ?? "").Trim(), StringComparison.Ordinal) ? null : cur;

            res.Add(t with
            {
                NombreCorto  = Dedup(t.NombreCorto,  p.NombreCorto),
                Modalidad    = Dedup(t.Modalidad,    p.Modalidad),
                PlazoLegal   = Dedup(t.PlazoLegal,   p.PlazoLegal),
                Tercero      = Dedup(t.Tercero,      p.Tercero),
                TiempoReal   = Dedup(t.TiempoReal,   p.TiempoReal),
                MetodoPago   = Dedup(t.MetodoPago,   p.MetodoPago),
                PagoBanco    = Dedup(t.PagoBanco,    p.PagoBanco),
                PagoCuenta   = Dedup(t.PagoCuenta,   p.PagoCuenta),
                TgrInst      = Dedup(t.TgrInst,      p.TgrInst),
                TgrRubro     = Dedup(t.TgrRubro,     p.TgrRubro),
                TgrMonto     = Dedup(t.TgrMonto,     p.TgrMonto),
                DocEntregado = Dedup(t.DocEntregado, p.DocEntregado),
                Objetivo     = Dedup(t.Objetivo,     p.Objetivo),
                Alcance      = Dedup(t.Alcance,      p.Alcance),
                AlcanceObs   = Dedup(t.AlcanceObs,   p.AlcanceObs),
                Descripcion  = Dedup(t.Descripcion,  p.Descripcion),
                Dirigido     = Dedup(t.Dirigido,     p.Dirigido),
                Horario      = Dedup(t.Horario,      p.Horario),
                Telefono     = Dedup(t.Telefono,     p.Telefono),
                EmailTramite = Dedup(t.EmailTramite, p.EmailTramite),
                SitioWeb     = Dedup(t.SitioWeb,     p.SitioWeb),
            });
        }
        return res;
    }

    private sealed class DigerTramRow
    {
        [JsonPropertyName("value")] public List<OriginalExpedienteDto>? Value { get; set; }
    }
}
