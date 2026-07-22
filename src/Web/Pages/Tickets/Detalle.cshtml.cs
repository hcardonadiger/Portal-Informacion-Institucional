using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.Tickets;

[Authorize]
public sealed class DetalleModel(ISender sender, ICurrentUserService currentUser, IWebHostEnvironment env) : PageModel
{
    public TicketDetailDto Ticket { get; private set; } = default!;
    public string? Error { get; set; }

    public bool EsAdmin => User.IsInRole(nameof(RolUsuario.Administrador));

    public bool PuedeGestionar => EsAdmin;
    public bool EsAsignado => Ticket.AsignadoAId is Guid a && a == currentUser.UserId;
    public bool PuedeAtender => EsAdmin;

    public bool SinResponsable => Ticket.AsignadoAId is null;
    public bool PuedeTomar => SinResponsable && EsAdmin;
    public bool PuedeLiberar => !SinResponsable && EsAdmin;

    private async Task<bool> CargarAsync(int id, CancellationToken ct)
    {
        try { Ticket = await sender.Send(new GetTicketByIdQuery(id), ct); }
        catch (NotFoundException) { return false; }
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

    public async Task<IActionResult> OnPostTomarAsync(int id, CancellationToken ct)
    {
        if (!await CargarAsync(id, ct)) return NotFound();
        if (!PuedeTomar) return Forbid();
        if (!SinResponsable)
        {
            TempData["SuccessMsg"] = $"El ticket ya lo tomó {Ticket.AsignadoA}.";
            return RedirectToPage(new { id });
        }
        await sender.Send(new AsignarTicketCommand(id, currentUser.UserId), ct);
        TempData["SuccessMsg"] = "Tomaste el ticket. Ahora eres el responsable del seguimiento.";
        return RedirectToPage(new { id });
    }

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
        if (!User.CanMutate()) return Forbid();
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
