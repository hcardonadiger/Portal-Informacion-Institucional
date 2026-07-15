namespace Diger.TramitesEstado.Web.Pages.Admin;

[Authorize(Roles = nameof(RolUsuario.Administrador))]
public sealed class PlantillasTramiteModel(ISender sender) : PageModel
{
    public IReadOnlyList<PlantillaListItemDto> Plantillas { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
        => Plantillas = await sender.Send(new GetPlantillasQuery(), ct);

    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        try { await sender.Send(new EliminarPlantillaCommand(id), ct); TempData["SuccessMsg"] = "Plantilla eliminada."; }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        return RedirectToPage();
    }
}
