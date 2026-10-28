using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Diger.TramitesEstado.Application.Reuniones.Memoria;
using Diger.TramitesEstado.Application.Reuniones.Outlook;
using Microsoft.Extensions.Logging;

namespace Diger.TramitesEstado.Infrastructure.Outlook;

/// <summary>
/// Las tres lecturas de Graph que saca la transcripción de una reunión de Teams, sobre /me (el
/// token es del organizador): la reunión en línea por su enlace, la lista de sus transcripciones
/// y el contenido de la más reciente en WebVTT, que se entrega en texto llano.
///
/// <para>Como <see cref="GraphOutlookCalendario"/>: sin SDK de Graph, y un fallo se registra con
/// el estado HTTP, el <c>error.code</c> y la ruta, nunca las cabeceras ni el cuerpo. La ruta del
/// filtro lleva el enlace de Teams, que no es secreto para quien ya ve la reunión.</para>
/// </summary>
public sealed class GraphTranscripcionesTeams(
    HttpClient http, IConexionMicrosoft conexion, ILogger<GraphTranscripcionesTeams> log) : ITranscripcionesTeams
{
    public const string SinReunionEnLinea =
        "Teams no encuentra la reunión en línea de esta cita con la cuenta de quien la organizó.";

    public const string SinTranscripcion =
        "La reunión no tiene transcripción. Debe iniciarse en Teams durante la reunión, y Teams la " +
        "publica unos minutos después de que termina: si acaba de terminar, vuelva a intentarlo en un rato.";

    public async Task<TranscripcionTeams> ObtenerUltimaAsync(Guid organizadorId, string enlaceTeams, CancellationToken ct)
    {
        var token = await conexion.ObtenerTokenTranscripcionesAsync(organizadorId, ct);

        // OData escapa la comilla simple duplicándola; luego todo el filtro va escapado en la URL.
        var filtro = Uri.EscapeDataString($"JoinWebUrl eq '{enlaceTeams.Replace("'", "''")}'");
        string reunionId;
        using (var doc = await JsonAsync(token, $"me/onlineMeetings?$filter={filtro}", ct))
        {
            reunionId = Valores(doc).Select(v => Texto(v, "id")).FirstOrDefault(id => id is not null)
                        ?? throw new TranscripcionNoDisponibleException(SinReunionEnLinea);
        }

        var rutaReunion = $"me/onlineMeetings/{Uri.EscapeDataString(reunionId)}";
        (string Id, DateTime Creada) ultima;
        using (var doc = await JsonAsync(token, $"{rutaReunion}/transcripts", ct))
        {
            ultima = Valores(doc)
                .Select(v => (Id: Texto(v, "id"), Creada: Fecha(Texto(v, "createdDateTime"))))
                .Where(t => t.Id is not null)
                .OrderByDescending(t => t.Creada)
                .Select(t => (t.Id!, t.Creada))
                .FirstOrDefault();
            if (ultima.Id is null) throw new TranscripcionNoDisponibleException(SinTranscripcion);
        }

        using var resp = await EnviarAsync(token,
            $"{rutaReunion}/transcripts/{Uri.EscapeDataString(ultima.Id)}/content?$format=text/vtt", ct);
        var vtt = await resp.Content.ReadAsStringAsync(ct);

        var texto = TranscripcionVtt.ATexto(vtt);
        if (texto.Length == 0) throw new TranscripcionNoDisponibleException(SinTranscripcion);
        return new TranscripcionTeams(texto, ultima.Creada);
    }

    private async Task<JsonDocument> JsonAsync(string token, string ruta, CancellationToken ct)
    {
        using var resp = await EnviarAsync(token, ruta, ct);
        try
        {
            return await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        }
        catch (JsonException)
        {
            throw new OutlookNoDisponibleException("Graph devolvió una respuesta ilegible");
        }
    }

    private static IEnumerable<JsonElement> Valores(JsonDocument doc) =>
        doc.RootElement.ValueKind == JsonValueKind.Object
        && doc.RootElement.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object).ToList()
            : [];

    private static string? Texto(JsonElement e, string propiedad) =>
        e.TryGetProperty(propiedad, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static DateTime Fecha(string? iso) =>
        DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var f)
            ? f : DateTime.MinValue;

    private async Task<HttpResponseMessage> EnviarAsync(string token, string ruta, CancellationToken ct)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Get, ruta);
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
        var codigoGraph = await GraphOutlookCalendario.CodigoDeErrorAsync(resp, ct);
        resp.Dispose();
        log.LogWarning("Graph respondió {Estado} ({Codigo}) en GET {Ruta}", (int)codigo, codigoGraph ?? "sin código", ruta);
        throw codigo switch
        {
            HttpStatusCode.Unauthorized => new OutlookConexionVencidaException("Graph rechazó el token (401)"),
            HttpStatusCode.Forbidden    => new TranscripcionNoDisponibleException(
                "Microsoft negó el acceso a la transcripción. Lo más probable es que TI no haya dado el " +
                "consentimiento de administrador para OnlineMeetingTranscript.Read.All."),
            HttpStatusCode.NotFound     => new TranscripcionNoDisponibleException(SinTranscripcion),
            _                           => new OutlookNoDisponibleException($"Graph respondió {(int)codigo}")
        };
    }
}
