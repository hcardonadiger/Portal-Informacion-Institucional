using Diger.TramitesEstado.Application.Dashboards.Queries.GetMiTablero;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Diger.TramitesEstado.Web.Pages.Tableros;

[Authorize]
[PermisoNoRequerido("Autoservicio: resume el trabajo asignado al propio usuario, no datos de otros.")]
public sealed class MiTableroModel(ISender sender) : PageModel
{
    public MiTableroDto Data { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken ct)
    {
        Data = await sender.Send(new GetMiTableroQuery(), ct);
    }
}
