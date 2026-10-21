namespace Diger.TramitesEstado.Web.Pages.Reuniones;

[Authorize]
[Permission("Reuniones", AccionModulo.Ver, "Ver reuniones y compromisos")]
public sealed class IndexModel(ISender sender) : PageModel
{
    public PagedResult<ReunionListItemDto> Resultado { get; private set; } = PagedResult<ReunionListItemDto>.Empty(Paginacion.TamanoDefecto);
    public IReadOnlyList<ReunionListItemDto> Todas { get; private set; } = [];
    public string? Q { get; private set; }

    public async Task OnGetAsync(string? q, int? pg, CancellationToken ct)
    {
        Q = q;
        Resultado = await sender.Send(new GetReunionesQuery(q, pg), ct);
        Todas = (await sender.Send(new GetReunionesQuery(q, Page: 1, Size: 100), ct)).Items;
    }

    [Permission("Reuniones", AccionModulo.Eliminar, "Eliminar reuniones")]
    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        // El chequeo de rol por nombre que había acá lo sustituye el [Permission] de arriba.
        await sender.Send(new EliminarReunionCommand(id), ct);
        TempData["SuccessMsg"] = "Reunión eliminada.";
        return RedirectToPage();
    }
}
