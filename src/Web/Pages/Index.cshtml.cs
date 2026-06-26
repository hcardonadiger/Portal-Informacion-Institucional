namespace Diger.TramitesEstado.Web.Pages;

public sealed class IndexModel(ISender sender) : PageModel
{
    public IReadOnlyList<ExpedienteListItemDto> Expedientes { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Expedientes = await sender.Send(new GetExpedientesQuery(), ct);
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        if (!User.IsInRole(nameof(RolUsuario.Administrador)) && !User.IsInRole(nameof(RolUsuario.Coordinador)))
            return Forbid();
        await sender.Send(new EliminarExpedienteCommand(id), ct);
        return RedirectToPage();
    }
}
