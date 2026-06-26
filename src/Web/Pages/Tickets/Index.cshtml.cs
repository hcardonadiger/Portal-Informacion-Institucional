namespace Diger.TramitesEstado.Web.Pages.Tickets;

[Authorize]
public sealed class IndexModel(ISender sender, IInstitucionRepository institucionRepo, ICurrentUserService currentUser) : PageModel
{
    public IReadOnlyList<TicketListItemDto> Tickets { get; private set; } = [];
    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];

    public EstadoTicket?    Estado    { get; private set; }
    public PrioridadTicket? Prioridad { get; private set; }
    public int?             InstitucionId { get; private set; }
    public bool             Mias      { get; private set; }

    public async Task OnGetAsync(EstadoTicket? estado, PrioridadTicket? prioridad, int? institucionId, bool mias, CancellationToken ct)
    {
        Estado = estado; Prioridad = prioridad; InstitucionId = institucionId; Mias = mias;
        Instituciones = await institucionRepo.GetAllActivasAsync(ct);

        int? asignado = mias ? currentUser.UserId : null;
        Tickets = await sender.Send(new GetTicketsQuery(estado, prioridad, institucionId, asignado), ct);
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        if (!User.IsInRole(nameof(RolUsuario.Administrador)) && !User.IsInRole(nameof(RolUsuario.Coordinador)))
            return Forbid();
        await sender.Send(new EliminarTicketCommand(id), ct);
        TempData["SuccessMsg"] = "Ticket eliminado.";
        return RedirectToPage();
    }
}
