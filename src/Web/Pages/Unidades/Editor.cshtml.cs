using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MediatR;
using Diger.TramitesEstado.Application.Unidades.Commands;
using Diger.TramitesEstado.Application.Unidades.Queries;
using Diger.TramitesEstado.Application.Areas.Queries;
using Microsoft.AspNetCore.Authorization;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Web.Pages.Unidades;

[Authorize(Roles = nameof(RolUsuario.Administrador))]
public class EditorModel(ISender sender) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? IdFromRoute { get; set; }

    private string _id = string.Empty;
    [BindProperty] public string Id { get => _id; set => _id = value?.ToUpperInvariant() ?? string.Empty; }
    [BindProperty] public string AreaId { get; set; } = string.Empty;
    [BindProperty] public string Nombre { get; set; } = string.Empty;
    [BindProperty] public string? NombreCorto { get; set; }
    [BindProperty] public string? Descripcion { get; set; }
    [BindProperty] public string? LogoUrl { get; set; }

    public IReadOnlyList<AreaListItemDto> Areas { get; set; } = [];

    public async Task<IActionResult> OnGetAsync([FromRoute] string? id, CancellationToken ct)
    {
        IdFromRoute = id;
        Areas = await sender.Send(new GetAreasQuery(), ct);

        if (id is not null)
        {
            try
            {
                var unidad = await sender.Send(new GetUnidadByIdQuery(id), ct);
                Id = unidad.Id;
                AreaId = unidad.AreaId;
                Nombre = unidad.Nombre;
                NombreCorto = unidad.NombreCorto;
                Descripcion = unidad.Descripcion;
                LogoUrl = unidad.LogoUrl;
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync([FromRoute] string? id, CancellationToken ct)
    {
        IdFromRoute = id;

        if (!ModelState.IsValid)
        {
            Areas = await sender.Send(new GetAreasQuery(), ct);
            return Page();
        }

        try
        {
            if (string.IsNullOrEmpty(id))
            {
                await sender.Send(new CrearUnidadCommand(Id, AreaId, Nombre, Descripcion, NombreCorto, LogoUrl), ct);
            }
            else
            {
                await sender.Send(new ActualizarUnidadCommand(id, Nombre, Descripcion, NombreCorto, LogoUrl), ct);
            }
            return RedirectToPage("./Index");
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            Areas = await sender.Send(new GetAreasQuery(), ct);
            return Page();
        }
    }
}
