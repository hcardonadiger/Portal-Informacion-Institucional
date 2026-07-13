namespace Diger.TramitesEstado.Web.Pages;

public sealed class IndexModel(ISender sender) : PageModel
{
    public PagedResult<ExpedienteListItemDto> Resultado { get; private set; } = PagedResult<ExpedienteListItemDto>.Empty(Paginacion.TamanoDefecto);
    public IReadOnlyList<ExpedienteListItemDto> Todos { get; private set; } = [];
    public string? Q { get; private set; }

    public async Task OnGetAsync(string? q, int? pg, CancellationToken ct)
    {
        Q = q;
        Resultado = await sender.Send(new GetExpedientesQuery(q, pg), ct);
        Todos = (await sender.Send(new GetExpedientesQuery(q, Page: 1, Size: 100), ct)).Items;
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        if (User.IsInRole(nameof(RolUsuario.Empleado)) || User.IsInRole(nameof(RolUsuario.Consultor)))
            return Forbid();
        await sender.Send(new EliminarExpedienteCommand(id), ct);
        return RedirectToPage();
    }
}
