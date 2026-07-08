namespace Diger.TramitesEstado.Web.Pages.Reuniones;

[Authorize]
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

    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        if (User.IsInRole(nameof(RolUsuario.Empleado)) || User.IsInRole(nameof(RolUsuario.Consultor)))
            return Forbid();
        await sender.Send(new EliminarReunionCommand(id), ct);
        TempData["SuccessMsg"] = "Reunión eliminada.";
        return RedirectToPage();
    }
}
