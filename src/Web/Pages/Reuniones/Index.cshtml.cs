namespace Diger.TramitesEstado.Web.Pages.Reuniones;

[Authorize]
public sealed class IndexModel(ISender sender) : PageModel
{
    public PagedResult<ReunionListItemDto> Resultado { get; private set; } = PagedResult<ReunionListItemDto>.Empty(15);
    public string? Q { get; private set; }

    public async Task OnGetAsync(string? q, int? page, CancellationToken ct)
    {
        Q = q;
        Resultado = await sender.Send(new GetReunionesQuery(q, page), ct);
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
