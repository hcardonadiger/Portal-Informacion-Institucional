using Diger.TramitesEstado.Application.Chat;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Web.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Diger.TramitesEstado.Web.Pages.Chat;

[Authorize]
public sealed class IndexModel(
    IChatService chatSvc,
    ICurrentUserService currentUser,
    IHubContext<SoporteHub> hub,
    INotificacionService notifSvc) : PageModel
{
    public IReadOnlyList<ChatSesionDto> SesionesActivas { get; private set; } = [];
    public IReadOnlyList<ChatSesionDto> Cola            { get; private set; } = [];
    public ChatSesionDetalleDto?        SesionAbierta   { get; private set; }
    public int                          SesionAbiertoId { get; private set; }

    public async Task OnGetAsync(int? sesion, CancellationToken ct)
    {
        var uid = currentUser.UserId;
        if (uid is null) return;

        SesionesActivas = await chatSvc.GetSesionesTecnicoAsync(uid.Value, ct);
        Cola            = await chatSvc.GetColaAsync(temaId: null, ct);

        if (sesion.HasValue)
        {
            SesionAbierta   = await chatSvc.GetDetalleAsync(sesion.Value, ct);
            SesionAbiertoId = sesion.Value;
        }
    }

    // Tomar sesión de la cola (POST desde el panel)
    public async Task<IActionResult> OnPostTomarAsync(int sesionId, CancellationToken ct)
    {
        var uid    = currentUser.UserId;
        var nombre = currentUser.Nombre ?? currentUser.Correo ?? "Técnico";
        if (uid is null) return Forbid();

        await chatSvc.AsignarTecnicoAsync(sesionId, uid.Value, nombre, ct);

        var msgSistema = await chatSvc.EnviarMensajeAsync(sesionId,
            $"{nombre} se ha unido al chat.",
            esDelTecnico: true, esSistema: true, autorNombre: "Sistema", ct);

        await hub.Clients.Group(SoporteHub.GrupoSesion(sesionId))
            .SendAsync("TecnicoAsignado", nombre, msgSistema, cancellationToken: ct);

        return RedirectToPage(new { sesion = sesionId });
    }

    // Cerrar sesión como resuelta
    public async Task<IActionResult> OnPostCerrarAsync(int sesionId, CancellationToken ct)
    {
        await chatSvc.CerrarSesionAsync(sesionId, ChatEstado.Resuelto, ct);

        var msgSistema = await chatSvc.EnviarMensajeAsync(sesionId,
            "El técnico marcó esta consulta como resuelta.",
            esDelTecnico: true, esSistema: true, autorNombre: "Sistema", ct);

        await hub.Clients.Group(SoporteHub.GrupoSesion(sesionId))
            .SendAsync("SesionCerrada", ChatEstado.Resuelto.ToString(), msgSistema, cancellationToken: ct);

        return RedirectToPage();
    }

    // Datos de una sesión para el panel AJAX
    public async Task<IActionResult> OnGetSesionAsync(int sesionId, CancellationToken ct)
    {
        var detalle = await chatSvc.GetDetalleAsync(sesionId, ct);
        return new JsonResult(detalle);
    }

    // Iniciar sesión desde el widget flotante (usuario final)
    public async Task<IActionResult> OnPostIniciarSesionAsync(
        [FromBody] IniciarSesionRequest req, CancellationToken ct)
    {
        var uid    = currentUser.UserId;
        var nombre = currentUser.Nombre ?? currentUser.Correo ?? "Usuario";
        if (uid is null) return Forbid();

        var sesion = await chatSvc.IniciarSesionAsync(uid.Value, nombre, req.TemaId, ct);

        if (!string.IsNullOrWhiteSpace(req.Mensaje))
            await chatSvc.EnviarMensajeAsync(sesion.Id, req.Mensaje.Trim(),
                esDelTecnico: false, esSistema: false, autorNombre: nombre, ct);

        // Buscar técnico disponible y notificarlo
        if (sesion.TemaId.HasValue)
        {
            var tecnicos = await chatSvc.GetTecnicosDisponiblesAsync(sesion.TemaId.Value, ct);
            var tecnico  = tecnicos.FirstOrDefault();
            if (tecnico != null)
            {
                await SoporteHub.NotificarTecnicoAsync(hub, notifSvc,
                    tecnico.Id, sesion.Id, nombre, sesion.TemaNombre, ct);
            }
        }

        return new JsonResult(new { sesionId = sesion.Id });
    }

    public sealed record IniciarSesionRequest(int? TemaId, string? Mensaje);
}
