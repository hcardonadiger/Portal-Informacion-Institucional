using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diger.TramitesEstado.Application.Reuniones.Outlook;

namespace Diger.TramitesEstado.Infrastructure.Outlook;

/// <summary>
/// Las tres llamadas a Graph que necesita el portal, sobre /me (el token es del organizador).
/// Sin SDK de Graph a propósito: son tres peticiones y así se prueban con un HttpMessageHandler.
/// </summary>
public sealed class GraphOutlookCalendario(HttpClient http, IConexionMicrosoft conexion) : IOutlookCalendario
{
    public async Task<ResultadoOutlook> CrearAsync(Guid organizadorId, EventoOutlook evento, Guid transaccionId, CancellationToken ct)
    {
        var cuerpo = Cuerpo(evento);
        cuerpo["transactionId"] = transaccionId.ToString();
        using var resp = await EnviarAsync(organizadorId, HttpMethod.Post, "me/events", cuerpo, ct);
        return await LeerAsync(resp, ct);
    }

    public async Task<ResultadoOutlook> ActualizarAsync(Guid organizadorId, string eventId, EventoOutlook evento, CancellationToken ct)
    {
        using var resp = await EnviarAsync(organizadorId, HttpMethod.Patch, $"me/events/{Uri.EscapeDataString(eventId)}", Cuerpo(evento), ct);
        return await LeerAsync(resp, ct);
    }

    public async Task CancelarAsync(Guid organizadorId, string eventId, string comentario, CancellationToken ct)
    {
        using var _ = await EnviarAsync(organizadorId, HttpMethod.Post, $"me/events/{Uri.EscapeDataString(eventId)}/cancel",
            new JsonObject { ["comment"] = comentario }, ct);
    }

    private static JsonObject Cuerpo(EventoOutlook e)
    {
        var o = new JsonObject
        {
            ["subject"]  = e.Asunto,
            ["body"]     = new JsonObject { ["contentType"] = "HTML", ["content"] = e.CuerpoHtml },
            ["start"]    = new JsonObject { ["dateTime"] = e.Inicio.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture), ["timeZone"] = EventoOutlook.ZonaHoraria },
            ["end"]      = new JsonObject { ["dateTime"] = e.Fin.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),    ["timeZone"] = EventoOutlook.ZonaHoraria },
            ["location"] = new JsonObject { ["displayName"] = e.Ubicacion ?? string.Empty },
            ["attendees"] = new JsonArray(e.Invitados.Select(i => (JsonNode)new JsonObject
            {
                ["emailAddress"] = new JsonObject { ["address"] = i.Correo, ["name"] = i.Nombre ?? i.Correo },
                ["type"] = "required"
            }).ToArray())
        };
        if (e.EsTeams)
        {
            o["isOnlineMeeting"] = true;
            o["onlineMeetingProvider"] = "teamsForBusiness";
        }
        return o;
    }

    private async Task<HttpResponseMessage> EnviarAsync(Guid organizadorId, HttpMethod metodo, string ruta, JsonObject cuerpo, CancellationToken ct)
    {
        var token = await conexion.ObtenerTokenAsync(organizadorId, ct);
        using var pedido = new HttpRequestMessage(metodo, ruta) { Content = JsonContent.Create(cuerpo) };
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
        resp.Dispose();
        throw codigo switch
        {
            HttpStatusCode.Unauthorized => new OutlookConexionVencidaException("Graph rechazó el token (401)"),
            HttpStatusCode.NotFound     => new OutlookNoDisponibleException("la cita ya no existe en Outlook (¿se borró desde el calendario?)"),
            _                           => new OutlookNoDisponibleException($"Graph respondió {(int)codigo}")
        };
    }

    /// <summary>
    /// Un cuerpo que no se puede leer (no es JSON, no es un objeto, le falta "id") es un fallo de
    /// Graph como cualquier otro: no se deja escapar JsonException ni KeyNotFoundException por el puerto.
    /// </summary>
    private static async Task<ResultadoOutlook> LeerAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var raiz = doc.RootElement;
            var id = raiz.GetProperty("id").GetString()
                     ?? throw new OutlookNoDisponibleException("Graph devolvió una respuesta ilegible");
            string? enlace = raiz.TryGetProperty("onlineMeeting", out var om) && om.ValueKind == JsonValueKind.Object
                             && om.TryGetProperty("joinUrl", out var ju) ? ju.GetString() : null;
            return new ResultadoOutlook(id, enlace);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new OutlookNoDisponibleException("Graph devolvió una respuesta ilegible");
        }
    }
}
