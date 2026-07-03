using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Diger.TramitesEstado.Application.Reuniones.Import;

namespace Diger.TramitesEstado.Infrastructure.Import;

/// <summary>Lee reuniones + asistencias del portal demo (Supabase, REST/PostgREST)
/// usando la URL y la anon key configuradas en <c>Supabase:Url</c> / <c>Supabase:AnonKey</c>.</summary>
public sealed class SupabaseReunionImportSource(HttpClient http, IConfiguration config)
    : IReunionImportSource
{
    public async Task<IReadOnlyList<ReunionImportRow>> ObtenerReunionesAsync(CancellationToken ct = default)
    {
        var baseUrl = config["Supabase:Url"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("Falta configuración 'Supabase:Url'.");
        var apiKey = config["Supabase:AnonKey"]
            ?? throw new InvalidOperationException("Falta configuración 'Supabase:AnonKey'.");

        http.DefaultRequestHeaders.Remove("apikey");
        http.DefaultRequestHeaders.Add("apikey", apiKey);
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var reuniones = await http.GetFromJsonAsync<List<SbReunion>>(
            $"{baseUrl}/rest/v1/reuniones?select=id,titulo,fecha,hora,lugar,tema,creada_por&order=fecha",
            ct) ?? [];

        var asistencias = await http.GetFromJsonAsync<List<SbAsistencia>>(
            $"{baseUrl}/rest/v1/asistencias?select=reunion_id,nombre,cargo,correo,institucion,telefono&limit=5000",
            ct) ?? [];

        var porReunion = asistencias
            .Where(a => !string.IsNullOrWhiteSpace(a.ReunionId) && !string.IsNullOrWhiteSpace(a.Nombre))
            .GroupBy(a => a.ReunionId!)
            .ToDictionary(g => g.Key, g => g.ToList());

        return reuniones.Select(r => new ReunionImportRow
        {
            Id        = r.Id ?? "",
            Titulo    = r.Titulo,
            Fecha     = r.Fecha,
            Hora      = r.Hora,
            Lugar     = r.Lugar,
            Tema      = r.Tema,
            CreadaPor = r.CreadaPor,
            Asistencias = (porReunion.TryGetValue(r.Id ?? "", out var lst) ? lst : [])
                .Select(a => new AsistenteImportRow
                {
                    Nombre      = a.Nombre ?? "",
                    Cargo       = a.Cargo,
                    Correo      = a.Correo,
                    Institucion = a.Institucion,
                    Telefono    = a.Telefono,
                }).ToList(),
        }).ToList();
    }

    // ── DTOs de transporte (forma PostgREST) ──────────────────────────────
    private sealed class SbReunion
    {
        [JsonPropertyName("id")]         public string? Id        { get; set; }
        [JsonPropertyName("titulo")]     public string? Titulo    { get; set; }
        [JsonPropertyName("fecha")]      public string? Fecha     { get; set; }
        [JsonPropertyName("hora")]       public string? Hora      { get; set; }
        [JsonPropertyName("lugar")]      public string? Lugar     { get; set; }
        [JsonPropertyName("tema")]       public string? Tema      { get; set; }
        [JsonPropertyName("creada_por")] public string? CreadaPor { get; set; }
    }

    private sealed class SbAsistencia
    {
        [JsonPropertyName("reunion_id")]  public string? ReunionId   { get; set; }
        [JsonPropertyName("nombre")]      public string? Nombre      { get; set; }
        [JsonPropertyName("cargo")]       public string? Cargo       { get; set; }
        [JsonPropertyName("correo")]      public string? Correo      { get; set; }
        [JsonPropertyName("institucion")] public string? Institucion { get; set; }
        [JsonPropertyName("telefono")]    public string? Telefono    { get; set; }
    }
}
