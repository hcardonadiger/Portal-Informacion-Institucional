using Diger.TramitesEstado.Application.Chat;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Web.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

namespace Diger.TramitesEstado.Web.Pages.Chat;

[Authorize]
public sealed class IndexModel(
    IChatService chatSvc,
    ICurrentUserService currentUser,
    IHubContext<SoporteHub> hub,
    INotificacionService notifSvc,
    ISender mediator,
    IApplicationDbContext ctx) : PageModel
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

    // Generar ticket de soporte a partir del historial de chat
    public async Task<IActionResult> OnPostCrearTicketDesdeChatAsync(
        [FromBody] CrearTicketDesdeChatRequest req, CancellationToken ct)
    {
        var uid = currentUser.UserId;
        if (uid is null) return Forbid();

        var detalle = await chatSvc.GetDetalleAsync(req.SesionId, ct);
        if (detalle is null) return NotFound();

        // Idempotencia: si ya hay ticket vinculado, devolver el existente
        if (detalle.Sesion.TicketId.HasValue)
        {
            var numeroExistente = await ctx.Tickets.AsNoTracking()
                .Where(t => t.Id == detalle.Sesion.TicketId.Value)
                .Select(t => t.Numero)
                .FirstOrDefaultAsync(ct);
            return new JsonResult(new { ticketId = detalle.Sesion.TicketId.Value, numero = numeroExistente, yaExistia = true });
        }

        // Construir transcripción como descripción del ticket
        var sb = new StringBuilder();
        sb.AppendLine($"Ticket generado desde chat de soporte #{req.SesionId}.");
        sb.AppendLine($"Inicio: {detalle.Sesion.Inicio.ToLocalTime():dd/MM/yyyy HH:mm}");
        if (detalle.Sesion.TemaNombre is { } tema) sb.AppendLine($"Tema: {tema}");
        if (detalle.Sesion.TecnicoNombre is { } tec) sb.AppendLine($"Técnico: {tec}");
        sb.AppendLine();
        sb.AppendLine("--- Transcripción del chat ---");
        foreach (var m in detalle.Mensajes.Where(m => !m.EsSistema))
        {
            var hora  = m.Enviado.ToLocalTime().ToString("HH:mm");
            var quien = m.EsDelTecnico ? m.AutorNombre : detalle.Sesion.UsuarioNombre;
            sb.AppendLine($"[{hora}] {quien}: {m.Texto}");
        }
        var desc = sb.ToString();
        if (desc.Length > 3900) desc = desc[..3900] + "\n... (transcripción truncada)";

        var tituloRaw = string.IsNullOrWhiteSpace(req.Titulo)
            ? (detalle.Sesion.TemaNombre is { } t ? $"Consulta: {t}" : "Consulta de soporte")
            : req.Titulo.Trim();

        var form = new TicketFormDto
        {
            Titulo        = tituloRaw[..Math.Min(200, tituloRaw.Length)],
            Descripcion   = desc,
            TemaId        = detalle.Sesion.TemaId,
            Prioridad     = PrioridadTicket.Media,
            InstitucionId = currentUser.ActiveInstitucionId,
        };

        try
        {
            var ticketId = await mediator.Send(new CrearTicketCommand(form), ct);
            await chatSvc.VincularTicketAsync(req.SesionId, ticketId, ct);

            var numero = await ctx.Tickets.AsNoTracking()
                .Where(t => t.Id == ticketId)
                .Select(t => t.Numero)
                .FirstOrDefaultAsync(ct);

            // Notificar al técnico (si está en el grupo) que se creó un ticket
            await hub.Clients.Group(SoporteHub.GrupoSesion(req.SesionId))
                .SendAsync("TicketCreado", ticketId, numero, cancellationToken: ct);

            return new JsonResult(new { ticketId, numero });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    public sealed record CrearTicketDesdeChatRequest(int SesionId, string? Titulo);
}
