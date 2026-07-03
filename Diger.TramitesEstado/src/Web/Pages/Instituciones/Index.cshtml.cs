namespace Diger.TramitesEstado.Web.Pages.Instituciones;

[Authorize(Policy = "PuedeAdministrarCatalogo")]
public sealed class IndexModel(ISender sender) : PageModel
{
    public PagedResult<InstitucionListItemDto> Resultado { get; private set; } = PagedResult<InstitucionListItemDto>.Empty(15);
    public string? Q { get; private set; }

    public async Task OnGetAsync(string? q, int? page, CancellationToken ct)
    {
        Q = q;
        Resultado = await sender.Send(new GetInstitucionesQuery(q, page), ct);
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
