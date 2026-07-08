using Microsoft.AspNetCore.Mvc.RazorPages;
using MediatR;
using Diger.TramitesEstado.Application.Areas.Queries;
using Microsoft.AspNetCore.Authorization;
using Diger.TramitesEstado.Domain.Enums;

namespace Diger.TramitesEstado.Web.Pages.Areas;

[Authorize(Roles = nameof(RolUsuario.Administrador))]
public class IndexModel(ISender sender) : PageModel
{
    public IReadOnlyList<AreaListItemDto> Items { get; set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Items = await sender.Send(new GetAreasQuery(), ct);
    }
}
