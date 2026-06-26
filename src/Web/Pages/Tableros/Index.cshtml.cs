namespace Diger.TramitesEstado.Web.Pages.Tableros;

[Authorize]
public sealed class IndexModel(ISender sender) : PageModel
{
    public ResumenDto Data { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken ct)
        => Data = await sender.Send(new GetResumenQuery(), ct);
}
