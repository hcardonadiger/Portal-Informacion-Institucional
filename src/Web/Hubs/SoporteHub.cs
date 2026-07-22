using Diger.TramitesEstado.Application.Chat;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Diger.TramitesEstado.Web.Hubs;

[Authorize]
public sealed class SoporteHub(
    IChatService chatSvc,
    ICurrentUserService currentUser,
    ILogger<SoporteHub> logger) : Hub
{
    // ── Constantes de grupos SignalR ──────────────────────────────────────
    public static string GrupoSesion(int sesionId) => $"sesion-{sesionId}";
    public static string GrupoTecnico(Guid uid)    => $"tecnico-{uid}";
    public static string GrupoTema(int temaId)     => $"tema-{temaId}";

    // ── Conexión / desconexión ────────────────────────────────────────────

    public override async Task OnConnectedAsync()
    {
        var uid = UserId();
        if (uid is null) return;

        // El técnico se suscribe a su grupo personal para recibir alertas de nuevos chats
        var rol = Context.User?.FindFirstValue("diger:rol");
        if (EsTecnico(rol))
            await Groups.AddToGroupAsync(Context.ConnectionId, GrupoTecnico(uid.Value));

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

        var uid    = UserId();
        var nombre = currentUser.Nombre ?? currentUser.Correo ?? "Usuario";
        var msg    = await chatSvc.EnviarMensajeAsync(sesionId, texto.Trim(),
            esDelTecnico: false, esSistema: false, autorNombre: nombre);

        await Clients.Group(GrupoSesion(sesionId))
            .SendAsync("MensajeRecibido", msg);
    }

    /// <summary>El usuario cierra voluntariamente el chat.</summary>
    public async Task AbandonarChat(int sesionId)
    {
        await chatSvc.CerrarSesionAsync(sesionId, ChatEstado.Abandonado);
        await Clients.Group(GrupoSesion(sesionId)).SendAsync("SesionCerrada", ChatEstado.Abandonado.ToString());
    }

    // ── Métodos del técnico ───────────────────────────────────────────────

    /// <summary>El técnico toma una sesión de la cola.</summary>
    public async Task TomarSesion(int sesionId)
    {
        var uid    = UserId();
        var nombre = currentUser.Nombre ?? currentUser.Correo ?? "Técnico";
        if (uid is null) return;

        await chatSvc.AsignarTecnicoAsync(sesionId, uid.Value, nombre);
        await Groups.AddToGroupAsync(Context.ConnectionId, GrupoSesion(sesionId));

        var msgSistema = await chatSvc.EnviarMensajeAsync(sesionId,
            $"{nombre} se ha unido al chat.",
            esDelTecnico: true, esSistema: true, autorNombre: "Sistema");

        await Clients.Group(GrupoSesion(sesionId)).SendAsync("TecnicoAsignado", nombre, msgSistema);
        logger.LogInformation("Chat: técnico {Uid} tomó sesión {SesionId}", uid, sesionId);
    }

    /// <summary>El técnico envía un mensaje.</summary>
    public async Task EnviarRespuesta(int sesionId, string texto)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Length > 2000) return;

        var nombre = currentUser.Nombre ?? currentUser.Correo ?? "Técnico";
        var msg    = await chatSvc.EnviarMensajeAsync(sesionId, texto.Trim(),
            esDelTecnico: true, esSistema: false, autorNombre: nombre);

        await Clients.Group(GrupoSesion(sesionId)).SendAsync("MensajeRecibido", msg);
    }

    /// <summary>El técnico cierra la sesión como resuelta.</summary>
    public async Task CerrarComoResuelto(int sesionId)
    {
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
        await chatSvc.MarcarLeidosAsync(sesionId, lectoPorTecnico: esElTecnicoQueLee);
        await Clients.OthersInGroup(GrupoSesion(sesionId)).SendAsync("MensajesLeidos", sesionId);
    }

    // ── Método usado por la página de técnico para notificar nuevos chats ─

    /// <summary>Llama al técnico asignado enviando una notificación SignalR.</summary>
    public static async Task NotificarTecnicoAsync(
        IHubContext<SoporteHub> hub,
        INotificacionService notifSvc,
        Guid tecnicoId,
        int sesionId,
        string usuarioNombre,
        string? temaNombre,
        CancellationToken ct = default)
    {
        await hub.Clients.Group(GrupoTecnico(tecnicoId))
            .SendAsync("NuevoChat", sesionId, usuarioNombre, temaNombre ?? "General", cancellationToken: ct);

        await notifSvc.CrearYGuardarAsync(
            tecnicoId,
            TipoNotificacion.ChatRecibido,
            $"Nuevo chat de {usuarioNombre}" + (temaNombre != null ? $" · {temaNombre}" : ""),
            $"/Chat#{sesionId}",
            ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private Guid? UserId()
    {
        var raw = Context.User?.FindFirstValue("diger:uid");
        return Guid.TryParse(raw, out var g) ? g : null;
    }

    private static bool EsTecnico(string? rol) => rol is
        nameof(RolUsuario.Empleado) or nameof(RolUsuario.JefeUnidad) or
        nameof(RolUsuario.JefeArea) or nameof(RolUsuario.JefeInstitucion) or
        nameof(RolUsuario.Administrador);
}
