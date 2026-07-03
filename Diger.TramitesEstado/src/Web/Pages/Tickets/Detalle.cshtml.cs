using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Web.Pages.Tickets;

[Authorize]
public sealed class DetalleModel(ISender sender, ICurrentUserService currentUser, IUsuarioRepository usuarioRepo, IWebHostEnvironment env) : PageModel
{
    public TicketDetailDto Ticket { get; private set; } = default!;
    public string? Error { get; set; }

    // Un técnico (sin rol de gestión superior) solo alcanza tickets de sus temas o asignados a él.
    private bool EsTecnicoRestringido =>
        User.IsInRole(nameof(RolUsuario.Tecnico))
        && !User.IsInRole(nameof(RolUsuario.Administrador))
        && !User.IsInRole(nameof(RolUsuario.Coordinador));

    // El filtro global por institución garantiza que el usuario solo carga tickets de su alcance;
    // por tanto, quien pueda verlo y tenga rol de gestión puede gestionarlo.
    public bool PuedeGestionar =>
        User.IsInRole(nameof(RolUsuario.Administrador)) || User.IsInRole(nameof(RolUsuario.Coordinador))
        || User.IsInRole(nameof(RolUsuario.Tecnico));
    public bool EsAsignado => Ticket.AsignadoAId is int a && a == currentUser.UserId;
    public bool PuedeAtender => PuedeGestionar || EsAsignado;
    private bool EsGestorSuperior =>
        User.IsInRole(nameof(RolUsuario.Administrador)) || User.IsInRole(nameof(RolUsuario.Coordinador));

    // El responsable se define cuando alguien "toma" el ticket para iniciar el seguimiento;
    // no se asigna en la creación.
    public bool SinResponsable => Ticket.AsignadoAId is null;
    public bool PuedeTomar => SinResponsable && PuedeGestionar;
    // Solo el propio responsable o un gestor superior puede liberar (un técnico no libera el de otro).
    public bool PuedeLiberar => !SinResponsable && (EsAsignado || EsGestorSuperior);

    // Carga el ticket aplicando el alcance del técnico: solo sus temas o los asignados a él.
    // Devuelve false (→ 404) si no existe o queda fuera de su alcance.
    private async Task<bool> CargarAsync(int id, CancellationToken ct)
    {
        try { Ticket = await sender.Send(new GetTicketByIdQuery(id), ct); }
        catch (NotFoundException) { return false; }

        if (EsTecnicoRestringido && currentUser.UserId is int uid)
        {
            var temaIds = await usuarioRepo.GetTemaIdsAsync(uid, ct);
            var enSuAlcance = (Ticket.TemaId is int tid && temaIds.Contains(tid)) || EsAsignado;
            if (!enSuAlcance) return false;
        }
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
        => await CargarAsync(id, ct) ? Page() : NotFound();

    public async Task<IActionResult> OnPostCambiarEstadoAsync(int id, EstadoTicket estado, string? nota, CancellationToken ct)
    {
        if (!await CargarAsync(id, ct)) return NotFound();
        if (!PuedeAtender) return Forbid();
        try
        {
            await sender.Send(new CambiarEstadoTicketCommand(id, estado, nota), ct);
            TempData["SuccessMsg"] = "Estado actualizado.";
            return RedirectToPage(new { id });
        }
        catch (DomainException ex) { Error = ex.Message; return Page(); }
    }

    // El responsable del seguimiento se auto-asigna tomando el ticket (no lo asigna quien lo crea).
    public async Task<IActionResult> OnPostTomarAsync(int id, CancellationToken ct)
    {
        if (!await CargarAsync(id, ct)) return NotFound();
        if (!PuedeGestionar) return Forbid();
        if (!SinResponsable)
        {
            TempData["SuccessMsg"] = $"El ticket ya lo tomó {Ticket.AsignadoA}.";
            return RedirectToPage(new { id });
        }
        await sender.Send(new AsignarTicketCommand(id, currentUser.UserId), ct);
        TempData["SuccessMsg"] = "Tomaste el ticket. Ahora eres el responsable del seguimiento.";
        return RedirectToPage(new { id });
    }

    // Liberar deja el ticket sin responsable para que lo tome quien corresponda.
    public async Task<IActionResult> OnPostLiberarAsync(int id, CancellationToken ct)
    {
        if (!await CargarAsync(id, ct)) return NotFound();
        if (!PuedeLiberar) return Forbid();
        await sender.Send(new AsignarTicketCommand(id, null), ct);
        TempData["SuccessMsg"] = "Ticket liberado. Queda disponible para que lo tome un responsable.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostComentarAsync(int id, string texto, List<IFormFile>? archivos, CancellationToken ct)
    {
        if (!await CargarAsync(id, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(texto))
        {
            Error = "El comentario no puede estar vacío.";
            return Page();
        }
        try
        {
            var adjuntos = await AdjuntoStorage.GuardarAsync(archivos, env, ct);
            await sender.Send(new AgregarComentarioTicketCommand(id, texto, adjuntos), ct);
            TempData["SuccessMsg"] = "Comentario agregado.";
            return RedirectToPage(new { id });
        }
        catch (DomainException ex) { Error = ex.Message; return Page(); }
    }
}
