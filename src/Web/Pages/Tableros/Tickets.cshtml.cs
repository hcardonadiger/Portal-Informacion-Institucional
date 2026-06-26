namespace Diger.TramitesEstado.Web.Pages.Tableros;

[Authorize]
public sealed class TicketsModel(ISender sender) : PageModel
{
    public TicketsDashboardDto Data { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken ct)
        => Data = await sender.Send(new GetTicketsDashboardQuery(), ct);
}
