namespace Diger.TramitesEstado.Web.Pages;

public sealed class IndexModel(ISender sender) : PageModel
{
    public PagedResult<ExpedienteListItemDto> Resultado { get; private set; } = PagedResult<ExpedienteListItemDto>.Empty(Paginacion.TamanoDefecto);
    public IReadOnlyList<ExpedienteListItemDto> Todos { get; private set; } = [];
    public string? Q { get; private set; }
    public bool? Legado { get; private set; }

    public async Task OnGetAsync(string? q, int? pg, bool? legado, CancellationToken ct)
    {
        Q = q;
        Legado = legado;
        Resultado = await sender.Send(new GetExpedientesQuery(q, pg, Legado: legado), ct);
        Todos = (await sender.Send(new GetExpedientesQuery(q, Page: 1, Size: 100, Legado: legado), ct)).Items;
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        if (!User.IsInRole(nameof(RolUsuario.Administrador)))
            return Forbid();
        await sender.Send(new EliminarExpedienteCommand(id), ct);
        return RedirectToPage();
    }
}
