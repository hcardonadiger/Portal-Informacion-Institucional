namespace Diger.TramitesEstado.Web.Pages.Tableros;

[Authorize]
public sealed class ReunionesModel(ISender sender) : PageModel
{
    public ReunionesDashboardDto Data { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken ct)
        => Data = await sender.Send(new GetReunionesDashboardQuery(), ct);
}
