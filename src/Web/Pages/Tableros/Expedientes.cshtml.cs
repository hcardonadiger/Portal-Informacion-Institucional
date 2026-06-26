namespace Diger.TramitesEstado.Web.Pages.Tableros;

[Authorize]
public sealed class ExpedientesModel(ISender sender) : PageModel
{
    public ExpedientesDashboardDto Data { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken ct)
        => Data = await sender.Send(new GetExpedientesDashboardQuery(), ct);
}
