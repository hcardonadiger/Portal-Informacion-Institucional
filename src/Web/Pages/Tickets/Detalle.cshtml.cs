using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Web.Pages.Tickets;

[Authorize]
public sealed class DetalleModel(ISender sender, ICurrentUserService currentUser) : PageModel
{
    public TicketDetailDto Ticket { get; private set; } = default!;
    public IReadOnlyList<UsuarioAsignableDto> Usuarios { get; private set; } = [];
    public string? Error { get; set; }

    // El filtro global por institución garantiza que el usuario solo carga tickets de su alcance;
    // por tanto, quien pueda verlo y tenga rol de gestión puede gestionarlo.
    public bool PuedeGestionar =>
        User.IsInRole(nameof(RolUsuario.Administrador)) || User.IsInRole(nameof(RolUsuario.Coordinador))
        || User.IsInRole(nameof(RolUsuario.Tecnico));
    public bool EsAsignado => Ticket.AsignadoAId is int a && a == currentUser.UserId;
    public bool PuedeAtender => PuedeGestionar || EsAsignado;

    private async Task<bool> CargarAsync(int id, CancellationToken ct)
    {
        try { Ticket = await sender.Send(new GetTicketByIdQuery(id), ct); }
        catch (NotFoundException) { return false; }
        Usuarios = await sender.Send(new GetUsuariosAsignablesQuery(), ct);
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

    public async Task<IActionResult> OnPostAsignarAsync(int id, int? usuarioId, CancellationToken ct)
    {
        if (!await CargarAsync(id, ct)) return NotFound();
        if (!PuedeGestionar) return Forbid();
        await sender.Send(new AsignarTicketCommand(id, usuarioId), ct);
        TempData["SuccessMsg"] = "Asignación actualizada.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostComentarAsync(int id, string texto, CancellationToken ct)
    {
        if (!await CargarAsync(id, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(texto))
        {
            Error = "El comentario no puede estar vacío.";
            return Page();
        }
        await sender.Send(new AgregarComentarioTicketCommand(id, texto), ct);
        TempData["SuccessMsg"] = "Comentario agregado.";
        return RedirectToPage(new { id });
    }
}
