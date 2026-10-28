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
// Bandeja del chat de soporte. Quién puede atenderla se sigue decidiendo además por la
// capacidad EsTecnicoSoporte del rol (ver SoporteHub); este permiso controla el acceso a la
// bandeja como módulo.
[Permission("Chat", AccionModulo.Ver, "Ver la bandeja del chat de soporte")]
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

    // Supervisión: el Global ve todas las conversaciones, no solo la cola y las suyas.
    public bool                         EsSupervision   { get; private set; }
    public IReadOnlyList<ChatSesionDto> ActivasDeOtros  { get; private set; } = [];
    public IReadOnlyList<ChatSesionDto> Cerradas        { get; private set; } = [];

    /// <summary>La sesión abierta la atiende quien mira: puede responder y resolverla.</summary>
    public bool LaAtiendo => SesionAbierta is { } d && d.Sesion.TecnicoId == currentUser.UserId &&
                             d.Sesion.Estado == ChatEstado.Activo;

    /// <summary>
    /// Un chat en cola lo toma cualquiera desde su vista previa, igual que desde la cola. Solo el
    /// Global puede quitarle a otro técnico un chat activo que no es suyo.
    /// </summary>
    public bool PuedeTomarLaAbierta => SesionAbierta is { } d && d.Sesion.TecnicoId != currentUser.UserId &&
                                       (d.Sesion.Estado == ChatEstado.EnCola ||
                                        EsSupervision && d.Sesion.Estado == ChatEstado.Activo);

    /// <summary>
    /// Jefaturas y Global reparten la cola; el resto solo toma. La PMO queda fuera aunque su rol
    /// sea de supervisión: su función es aparte del soporte, y repartir chats no le corresponde.
    /// </summary>
    public bool PuedeAsignar => currentUser.EsGlobal || (currentUser.EsSupervisor && !currentUser.EsPmo);

    /// <summary>Candidatos para el «Asignar a…» de cada chat en cola, por su tema (null = sin tema).</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<TecnicoAsignableDto>> Asignables { get; private set; }
        = new Dictionary<int, IReadOnlyList<TecnicoAsignableDto>>();

    public IReadOnlyList<TecnicoAsignableDto> AsignablesDe(ChatSesionDto s) =>
        Asignables.TryGetValue(s.TemaId ?? 0, out var l) ? l : [];

    private async Task CargarColaAsync(CancellationToken ct)
    {
        Cola = await chatSvc.GetColaAsync(temaId: null, ct);
        if (!PuedeAsignar) return;

        var porTema = new Dictionary<int, IReadOnlyList<TecnicoAsignableDto>>();
        foreach (var temaId in Cola.Select(c => c.TemaId).Distinct())
            porTema[temaId ?? 0] = await chatSvc.GetTecnicosAsignablesAsync(temaId, ct);
        Asignables = porTema;
    }

    public async Task OnGetAsync(int? sesion, CancellationToken ct)
    {
        var uid = currentUser.UserId;
        if (uid is null) return;

        SesionesActivas = await chatSvc.GetSesionesTecnicoAsync(uid.Value, ct);
        await CargarColaAsync(ct);

        EsSupervision = currentUser.EsGlobal;
        if (EsSupervision)
        {
            ActivasDeOtros = await chatSvc.GetActivasDeOtrosAsync(uid.Value, ct);
            Cerradas       = await chatSvc.GetCerradasRecientesAsync(ct: ct);
        }

        if (sesion.HasValue)
        {
            SesionAbierta   = await chatSvc.GetDetalleAsync(sesion.Value, ct);
            SesionAbiertoId = sesion.Value;
        }
    }

    // Tomar sesión de la cola (POST desde el panel)
    [Permission("Chat", AccionModulo.Editar, "Atender el chat de soporte")]
    public async Task<IActionResult> OnPostTomarAsync(int sesionId, CancellationToken ct)
    {
        var uid    = currentUser.UserId;
        var nombre = currentUser.Nombre ?? currentUser.Correo ?? "Técnico";
        if (uid is null) return Forbid();

        string? anterior;
        try
        {
            anterior = await chatSvc.TomarSesionAsync(sesionId, uid.Value, nombre,
                puedeReasignar: currentUser.EsGlobal, ct);
        }
        catch (DomainException ex)
        {
            TempData["ErrorMsg"] = ex.Message;
            return RedirectToPage(new { sesion = sesionId });
        }

        var msgSistema = await chatSvc.EnviarMensajeAsync(sesionId,
            SoporteHub.MensajeToma(nombre, anterior),
            esDelTecnico: true, esSistema: true, autorNombre: "Sistema", ct);

        await hub.Clients.Group(SoporteHub.GrupoSesion(sesionId))
            .SendAsync("TecnicoAsignado", nombre, msgSistema, uid.Value, cancellationToken: ct);
        await SoporteHub.AvisarColaAsync(hub.Clients.Group(SoporteHub.GrupoBandeja), chatSvc, ct);

        return RedirectToPage(new { sesion = sesionId });
    }

    /// <summary>La tabla «En cola», sola: la bandeja la vuelve a pedir cuando la cola cambia.</summary>
    public async Task<IActionResult> OnGetColaAsync(int? sesion, CancellationToken ct)
    {
        SesionAbiertoId = sesion ?? 0;
        await CargarColaAsync(ct);
        return Partial("_ColaChat", this);
    }

    // Un supervisor reparte un chat de la cola. La capacidad del rol se exige acá, no solo en la
    // vista: el permiso Chat.Editar lo tiene cualquier técnico.
    [Permission("Chat", AccionModulo.Editar, "Atender el chat de soporte")]
    public async Task<IActionResult> OnPostAsignarAsync(int sesionId, Guid tecnicoId, CancellationToken ct)
    {
        if (!PuedeAsignar) return Forbid();

        string tecnicoNombre;
        try
        {
            tecnicoNombre = await chatSvc.AsignarDesdeColaAsync(sesionId, tecnicoId, ct);
        }
        catch (DomainException ex)
        {
            TempData["ErrorMsg"] = ex.Message;
            return RedirectToPage();
        }

        var quien = currentUser.Nombre ?? currentUser.Correo ?? "Supervisor";
        var msgSistema = await chatSvc.EnviarMensajeAsync(sesionId,
            $"{quien} asignó el chat a {tecnicoNombre}.",
            esDelTecnico: true, esSistema: true, autorNombre: "Sistema", ct);

        await hub.Clients.Group(SoporteHub.GrupoSesion(sesionId))
            .SendAsync("TecnicoAsignado", tecnicoNombre, msgSistema, tecnicoId, cancellationToken: ct);
        await SoporteHub.AvisarColaAsync(hub.Clients.Group(SoporteHub.GrupoBandeja), chatSvc, ct);

        // Este sí es un aviso con destinatario: el chat ya es suyo.
        var detalle = await chatSvc.GetDetalleAsync(sesionId, ct);
        await notifSvc.CrearYGuardarAsync(tecnicoId, TipoNotificacion.ChatRecibido,
            $"Te asignaron un chat de {detalle?.Sesion.UsuarioNombre}" +
                (detalle?.Sesion.TemaNombre is { } tema ? $" · {tema}" : ""),
            $"/Chat?sesion={sesionId}", ct: ct);

        TempData["SuccessMsg"] = $"Chat asignado a {tecnicoNombre}.";
        return RedirectToPage();
    }

    // Cerrar sesión como resuelta
    [Permission("Chat", AccionModulo.Editar, "Atender el chat de soporte")]
    public async Task<IActionResult> OnPostCerrarAsync(int sesionId, CancellationToken ct)
    {
        // Resolver es del técnico que la atiende; el Global la lee, y si quiere cerrarla la toma antes.
        if (currentUser.UserId is not Guid uid || !await chatSvc.AtiendeSesionAsync(sesionId, uid, ct))
        {
            TempData["ErrorMsg"] = "Solo el técnico que atiende esta conversación puede resolverla.";
            return RedirectToPage(new { sesion = sesionId });
        }

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
    [Permission("Chat", AccionModulo.Editar, "Atender el chat de soporte")]
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

        // Ya no se elige a un técnico para avisarle: el chat entra a la cola que ve toda la
        // bandeja, y cada quien lo toma o un supervisor lo reparte.
        await SoporteHub.AvisarColaAsync(hub.Clients.Group(SoporteHub.GrupoBandeja), chatSvc, ct);

        return new JsonResult(new { sesionId = sesion.Id });
    }

    public sealed record IniciarSesionRequest(int? TemaId, string? Mensaje);

    // El usuario cierra su chat abierto para empezar otra consulta desde la burbuja. Solo el
    // dueño: un chat ajeno responde NotFound, igual que uno que no existe.
    [Permission("Chat", AccionModulo.Editar, "Atender el chat de soporte")]
    public async Task<IActionResult> OnPostNuevaConsultaAsync(int sesionId, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid uid) return Forbid();

        var detalle = await chatSvc.GetDetalleAsync(sesionId, ct);
        if (detalle is null || detalle.Sesion.UsuarioId != uid) return NotFound();
        if (detalle.Sesion.Estado is not (ChatEstado.EnCola or ChatEstado.Activo))
            return new JsonResult(new { cerrada = false });

        await chatSvc.CerrarSesionAsync(sesionId, ChatEstado.Abandonado, ct);
        var msgSistema = await chatSvc.EnviarMensajeAsync(sesionId,
            "El usuario cerró la conversación para iniciar otra consulta.",
            esDelTecnico: false, esSistema: true, autorNombre: "Sistema", ct);

        await hub.Clients.Group(SoporteHub.GrupoSesion(sesionId))
            .SendAsync("SesionCerrada", ChatEstado.Abandonado.ToString(), msgSistema, cancellationToken: ct);
        await SoporteHub.AvisarColaAsync(hub.Clients.Group(SoporteHub.GrupoBandeja), chatSvc, ct);

        return new JsonResult(new { cerrada = true });
    }

    // Generar ticket de soporte a partir del historial de chat
    // Crea un ticket a partir de la conversación: la mutación es sobre Tickets, no sobre Chat.
    [Permission("Tickets", AccionModulo.Editar, "Crear y editar tickets")]
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
            // Sin prioridad elegida: el comando resuelve la predeterminada del catálogo.
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
