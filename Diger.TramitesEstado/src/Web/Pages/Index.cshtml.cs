namespace Diger.TramitesEstado.Web.Pages;

public sealed class IndexModel(ISender sender) : PageModel
{
    public PagedResult<ExpedienteListItemDto> Resultado { get; private set; } = PagedResult<ExpedienteListItemDto>.Empty(15);
    public string? Q { get; private set; }

    public async Task OnGetAsync(string? q, int? page, CancellationToken ct)
    {
        Q = q;
        Resultado = await sender.Send(new GetExpedientesQuery(q, page), ct);
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        if (!User.IsInRole(nameof(RolUsuario.Administrador)) && !User.IsInRole(nameof(RolUsuario.Coordinador)))
            return Forbid();
        await sender.Send(new EliminarExpedienteCommand(id), ct);
        return RedirectToPage();
    }
}
