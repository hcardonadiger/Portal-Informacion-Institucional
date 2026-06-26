namespace Diger.TramitesEstado.Web.Pages.Instituciones;

[Authorize(Policy = "PuedeAdministrarCatalogo")]
public sealed class IndexModel(ISender sender) : PageModel
{
    public IReadOnlyList<InstitucionListItemDto> Instituciones { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Instituciones = await sender.Send(new GetInstitucionesQuery(), ct);
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new EliminarInstitucionCommand(id), ct);
            TempData["SuccessMsg"] = "Institución eliminada.";
        }
        catch (DomainException ex)
        {
            TempData["ErrorMsg"] = ex.Message;
        }
        return RedirectToPage();
    }
}
