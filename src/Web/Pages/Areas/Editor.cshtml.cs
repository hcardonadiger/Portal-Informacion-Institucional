using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MediatR;
using Diger.TramitesEstado.Application.Areas.Commands;
using Diger.TramitesEstado.Application.Areas.Queries;
using Diger.TramitesEstado.Application.Instituciones.Queries.GetInstituciones;
using Microsoft.AspNetCore.Authorization;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Web.Pages.Areas;

[Authorize(Roles = nameof(RolUsuario.Administrador))]
public class EditorModel(ISender sender) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? IdFromRoute { get; set; }

    private string _id = string.Empty;
    [BindProperty] public string Id { get => _id; set => _id = value?.ToUpperInvariant() ?? string.Empty; }
    [BindProperty] public string InstitucionId { get; set; } = string.Empty;
    [BindProperty] public string Nombre { get; set; } = string.Empty;
    [BindProperty] public string? NombreCorto { get; set; }
    [BindProperty] public string? Descripcion { get; set; }
    [BindProperty] public string? LogoUrl { get; set; }

    public IReadOnlyList<InstitucionListItemDto> Instituciones { get; set; } = [];

    public async Task<IActionResult> OnGetAsync([FromRoute] string? id, CancellationToken ct)
    {
        IdFromRoute = id;
        Instituciones = (await sender.Send(new GetInstitucionesQuery(Size: 1000), ct)).Items;

        if (id is not null)
        {
            try
            {
                var area = await sender.Send(new GetAreaByIdQuery(id), ct);
                Id = area.Id;
                InstitucionId = area.InstitucionId;
                Nombre = area.Nombre;
                NombreCorto = area.NombreCorto;
                Descripcion = area.Descripcion;
                LogoUrl = area.LogoUrl;
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
            Instituciones = (await sender.Send(new GetInstitucionesQuery(Size: 1000), ct)).Items;
            return Page();
        }

        try
        {
            if (string.IsNullOrEmpty(id))
            {
                await sender.Send(new CrearAreaCommand(Id, InstitucionId, Nombre, Descripcion, NombreCorto, LogoUrl), ct);
            }
            else
            {
                await sender.Send(new ActualizarAreaCommand(id, Nombre, Descripcion, NombreCorto, LogoUrl), ct);
            }
            return RedirectToPage("./Index");
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            Instituciones = (await sender.Send(new GetInstitucionesQuery(Size: 1000), ct)).Items;
            return Page();
        }
    }
}
