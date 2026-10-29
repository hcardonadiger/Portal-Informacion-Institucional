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
/// Outlook/: sin SDK de Graph, y los logs llevan estado y código, nunca cuerpo, cabeceras ni ruta
/// (la de un evento lleva su id y no aporta al diagnóstico).
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
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("value", out var valores) && valores.ValueKind == JsonValueKind.Array)
                foreach (var e in valores.EnumerateArray())
                    if (Leer(e) is { } c) citas.Add(c);

            // El nextLink es absoluto: HttpClient lo usa tal cual aunque tenga BaseAddress.
            ruta = doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty("@odata.nextLink", out var sig) && sig.ValueKind == JsonValueKind.String
                ? sig.GetString() : null;
        }

        // Diagnóstico (Debug): qué vio Graph, sin asunto, lugar ni invitados. La clave es el hash del
        // id, la misma que queda en OrigenExternoId, para cruzarla con la base.
        if (log.IsEnabled(LogLevel.Debug))
            foreach (var c in citas)
                log.LogDebug("Cita {Clave}: inicio {Inicio:yyyy-MM-dd HH:mm}, cancelada {Cancelada}, modificada {Modificada:o}",
                    Diger.TramitesEstado.Domain.Entities.Reunion.ClaveOrigenOutlook(c.Id)[..20], c.Inicio, c.Cancelada, c.ModificadaElUtc);
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
