namespace Diger.TramitesEstado.Web.Pages.Reuniones;

[Authorize]
public sealed class IndexModel(ISender sender) : PageModel
{
    public IReadOnlyList<ReunionListItemDto> Reuniones { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Reuniones = await sender.Send(new GetReunionesQuery(), ct);
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        if (!User.IsInRole(nameof(RolUsuario.Administrador)) && !User.IsInRole(nameof(RolUsuario.Coordinador)))
            return Forbid();
        await sender.Send(new EliminarReunionCommand(id), ct);
        TempData["SuccessMsg"] = "Reunión eliminada.";
        return RedirectToPage();
    }
}
