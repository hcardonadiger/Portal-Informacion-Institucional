using Microsoft.AspNetCore.Mvc.RazorPages;
using MediatR;
using Diger.TramitesEstado.Application.Unidades.Queries;
using Microsoft.AspNetCore.Authorization;
using Diger.TramitesEstado.Domain.Enums;

namespace Diger.TramitesEstado.Web.Pages.Unidades;

[Authorize(Roles = $"{nameof(RolUsuario.Administrador)},{nameof(RolUsuario.JefeInstitucion)}")]
public class IndexModel(ISender sender) : PageModel
{
    public IReadOnlyList<UnidadListItemDto> Items { get; set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Items = await sender.Send(new GetUnidadesQuery(), ct);
    }
}
