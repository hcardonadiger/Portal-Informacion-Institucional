using Diger.TramitesEstado.Application.Dashboards.Queries.GetMiTablero;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Diger.TramitesEstado.Web.Pages.Tableros;

[Authorize]
public sealed class MiTableroModel(ISender sender) : PageModel
{
    public MiTableroDto Data { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken ct)
    {
        Data = await sender.Send(new GetMiTableroQuery(), ct);
    }
}
