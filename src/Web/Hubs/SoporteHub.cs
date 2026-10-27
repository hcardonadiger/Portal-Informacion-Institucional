using Diger.TramitesEstado.Application.AI;
using Diger.TramitesEstado.Application.Chat;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Diger.TramitesEstado.Web.Hubs;

[Authorize]
public sealed class SoporteHub(
    IChatService chatSvc,
    ICurrentUserService currentUser,
    IAgenteService agente,
    ILogger<SoporteHub> logger) : Hub
{
    // ── Constantes de grupos SignalR ──────────────────────────────────────
    public static string GrupoSesion(int sesionId) => $"sesion-{sesionId}";
    /// <summary>Todos los que atienden soporte: comparten la cola y su contador en vivo.</summary>
    public const string GrupoBandeja = "bandeja-soporte";
    public static string GrupoTema(int temaId)     => $"tema-{temaId}";

    // ── Conexión / desconexión ────────────────────────────────────────────

    public override async Task OnConnectedAsync()
    {
        var uid = UserId();
        if (uid is null) return;

        // Quien atiende soporte ve la misma cola que los demás: se suscribe a sus cambios.
        if (currentUser.EsTecnicoSoporte)
            await Groups.AddToGroupAsync(Context.ConnectionId, GrupoBandeja);

        logger.LogDebug("Chat: usuario {Uid} conectado ({ConnectionId})", uid, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogDebug("Chat: usuario {Uid} desconectado", UserId());
        await base.OnDisconnectedAsync(exception);
    }

    // ── Métodos del cliente (usuario final) ───────────────────────────────

    /// <summary>El usuario inicia o retoma su sesión de chat.</summary>
    public async Task UnirseASesion(int sesionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GrupoSesion(sesionId));
    }

    /// <summary>El usuario envía un mensaje.</summary>
    public async Task EnviarMensaje(int sesionId, string texto)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Length > 2000) return;

        var nombre = currentUser.Nombre ?? currentUser.Correo ?? "Usuario";
        var msg    = await chatSvc.EnviarMensajeAsync(sesionId, texto.Trim(),
            esDelTecnico: false, esSistema: false, autorNombre: nombre);

        await Clients.Group(GrupoSesion(sesionId)).SendAsync("MensajeRecibido", msg);

        // Si la sesión sigue en cola (sin técnico), el asistente IA responde
        var detalle = await chatSvc.GetDetalleAsync(sesionId);
        if (detalle?.Sesion.Estado == ChatEstado.EnCola)
        {
            var respuesta = await agente.ResponderAsync(detalle);
            if (!string.IsNullOrEmpty(respuesta))
            {
                var msgAgente = await chatSvc.EnviarMensajeAsync(sesionId, respuesta,
                    esDelTecnico: true, esSistema: false, autorNombre: "Asistente DIGER");
                await Clients.Group(GrupoSesion(sesionId)).SendAsync("MensajeRecibido", msgAgente);
            }
        }
    }

    /// <summary>El usuario cierra voluntariamente el chat.</summary>
    public async Task AbandonarChat(int sesionId)
    {
        await chatSvc.CerrarSesionAsync(sesionId, ChatEstado.Abandonado);
        await Clients.Group(GrupoSesion(sesionId)).SendAsync("SesionCerrada", ChatEstado.Abandonado.ToString());
        await AvisarColaAsync(Clients.Group(GrupoBandeja), chatSvc);
    }

    // ── Métodos del técnico ───────────────────────────────────────────────

    /// <summary>El técnico toma una sesión de la cola.</summary>
    public async Task TomarSesion(int sesionId)
    {
        var uid    = UserId();
        var nombre = currentUser.Nombre ?? currentUser.Correo ?? "Técnico";
        if (uid is null) return;

        string? anterior;
        try
        {
            anterior = await chatSvc.TomarSesionAsync(sesionId, uid.Value, nombre, puedeReasignar: currentUser.EsGlobal);
        }
        catch (DomainException ex)
        {
            throw new HubException(ex.Message);
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, GrupoSesion(sesionId));

        var msgSistema = await chatSvc.EnviarMensajeAsync(sesionId, MensajeToma(nombre, anterior),
            esDelTecnico: true, esSistema: true, autorNombre: "Sistema");

        await Clients.Group(GrupoSesion(sesionId)).SendAsync("TecnicoAsignado", nombre, msgSistema, uid.Value);
        await AvisarColaAsync(Clients.Group(GrupoBandeja), chatSvc);
        logger.LogInformation("Chat: técnico {Uid} tomó sesión {SesionId}", uid, sesionId);
    }

    /// <summary>El texto de sistema que deja la toma; si hubo reasignación, dice a quién se la quitó.</summary>
    public static string MensajeToma(string nombre, string? anterior) => anterior is null
        ? $"{nombre} se ha unido al chat."
        : $"{nombre} tomó el chat (lo atendía {anterior}).";

    /// <summary>
    /// Solo quien atiende la sesión responde o la resuelve. Sin esta guarda, cualquier
    /// conectado al hub podía escribir en cualquier chat con solo conocer su número; y el
    /// «solo lectura» del Global en los chats ajenos habría sido cosa de la pantalla.
    /// </summary>
    private async Task ExigirQueLaAtiendeAsync(int sesionId)
    {
        var uid = UserId();
        if (uid is null || !await chatSvc.AtiendeSesionAsync(sesionId, uid.Value))
            throw new HubException("Solo el técnico que atiende esta conversación puede responderla o resolverla.");
    }

    /// <summary>El técnico envía un mensaje.</summary>
    public async Task EnviarRespuesta(int sesionId, string texto)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Length > 2000) return;
        await ExigirQueLaAtiendeAsync(sesionId);

        var nombre = currentUser.Nombre ?? currentUser.Correo ?? "Técnico";
        var msg    = await chatSvc.EnviarMensajeAsync(sesionId, texto.Trim(),
            esDelTecnico: true, esSistema: false, autorNombre: nombre);

        await Clients.Group(GrupoSesion(sesionId)).SendAsync("MensajeRecibido", msg);
    }

    /// <summary>El técnico cierra la sesión como resuelta.</summary>
    public async Task CerrarComoResuelto(int sesionId)
    {
        await ExigirQueLaAtiendeAsync(sesionId);
        await chatSvc.CerrarSesionAsync(sesionId, ChatEstado.Resuelto);

        var msgSistema = await chatSvc.EnviarMensajeAsync(sesionId,
            "El técnico marcó esta consulta como resuelta.",
            esDelTecnico: true, esSistema: true, autorNombre: "Sistema");

        await Clients.Group(GrupoSesion(sesionId))
            .SendAsync("SesionCerrada", ChatEstado.Resuelto.ToString(), msgSistema);
    }

    /// <summary>Marca mensajes del interlocutor como leídos.</summary>
    public async Task MarcarLeidos(int sesionId, bool esElTecnicoQueLee)
    {
        // Que el Global lea un chat ajeno no debe apagarle los no leídos al técnico que lo atiende.
        if (esElTecnicoQueLee && (UserId() is not Guid uid || !await chatSvc.AtiendeSesionAsync(sesionId, uid)))
            return;

        await chatSvc.MarcarLeidosAsync(sesionId, lectoPorTecnico: esElTecnicoQueLee);
        await Clients.OthersInGroup(GrupoSesion(sesionId)).SendAsync("MensajesLeidos", sesionId);
    }

    // ── La cola compartida ────────────────────────────────────────────────

    /// <summary>
    /// Avisa a toda la bandeja que la cola cambió, con el nuevo total: el contador del menú y la
    /// tabla «En cola» se refrescan solos. Ya no se elige a un técnico para avisarle: la cola es
    /// una sola, la ven todos, cada quien toma y un supervisor reparte.
    /// </summary>
    public static async Task AvisarColaAsync(IClientProxy bandeja, IChatService chatSvc, CancellationToken ct = default)
        => await bandeja.SendAsync("ColaActualizada", await chatSvc.ContarColaAsync(ct), cancellationToken: ct);

    // ── Helpers ───────────────────────────────────────────────────────────

    private Guid? UserId()
    {
        var raw = Context.User?.FindFirstValue("diger:uid");
        return Guid.TryParse(raw, out var g) ? g : null;
    }

}
