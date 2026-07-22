using Diger.TramitesEstado.Application.PlanTrabajo.Commands;
using Diger.TramitesEstado.Application.PlanTrabajo.Common;
using Diger.TramitesEstado.Application.PlanTrabajo.Queries;

namespace Diger.TramitesEstado.Web.Pages.PlanTrabajo;

[Authorize]
public sealed class IndexModel(ISender sender) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? InstitucionId { get; set; }
    [BindProperty(SupportsGet = true)] public int?    Anio          { get; set; }

    // Crear nuevo plan
    [BindProperty] public string? NuevaInstitucionId { get; set; }
    [BindProperty] public string? NuevaInstitucion   { get; set; }
    [BindProperty] public int     NuevoAnio          { get; set; } = DateTime.UtcNow.Year;
    [BindProperty] public string? NuevasObs          { get; set; }

    public IReadOnlyList<PlanTrabajoListItemDto> Planes { get; private set; } = [];
    public IReadOnlyList<(string Id, string Nombre)> Instituciones { get; private set; } = [];
    public bool EsAdmin => User.IsInRole("Administrador");

    public async Task OnGetAsync(CancellationToken ct)
    {
        Anio ??= DateTime.UtcNow.Year;
        await CargarAsync(ct);
    }

    public async Task<IActionResult> OnPostCrearAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(NuevaInstitucionId) || string.IsNullOrWhiteSpace(NuevaInstitucion))
            return RedirectToPage(new { Anio });

        try
        {
            var id = await sender.Send(
                new CrearPlanTrabajoCommand(NuevaInstitucionId, NuevaInstitucion, NuevoAnio, NuevasObs), ct);
            return RedirectToPage("/PlanTrabajo/Editor", new { id });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await CargarAsync(ct);
            return Page();
        }
    }

    private async Task CargarAsync(CancellationToken ct)
    {
        Planes        = await sender.Send(new GetPlanesQuery(InstitucionId, Anio), ct);
        var paged     = await sender.Send(new GetInstitucionesQuery(Size: 500), ct);
        Instituciones = paged.Items.Select(i => (i.Id, i.Nombre)).ToList();
    }
}
