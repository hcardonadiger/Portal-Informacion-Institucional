using Diger.TramitesEstado.Web.Common;

namespace Diger.TramitesEstado.Web.Pages.Instituciones;

[Authorize(Policy = "PuedeAdministrarCatalogo")]
public sealed class EditorModel(ISender sender, IInstitucionBrandingService branding) : PageModel
{
    public string? InstId { get; private set; }
    public InstitucionDetailDto? Detalle { get; private set; }

    private string _id = string.Empty;
    [BindProperty] public string  Id           { get => _id; set => _id = value?.ToUpperInvariant() ?? string.Empty; }
    [BindProperty] public string  Nombre       { get; set; } = string.Empty;
    [BindProperty] public bool    Activo       { get; set; } = true;
    [BindProperty] public string? TramitesText { get; set; }
    [BindProperty] public string? NombreCorto  { get; set; }
    [BindProperty] public string? Descripcion  { get; set; }
    [BindProperty] public string? LogoUrl      { get; set; }
    [BindProperty] public string? Color        { get; set; }

    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync([FromRoute] string? id, CancellationToken ct)
    {
        if (id is null) return Page();

        try
        {
            var d = await sender.Send(new GetInstitucionByIdQuery(id), ct);
            Detalle      = d;
            InstId       = d.Id;
            Id           = d.Id;
            Nombre       = d.Nombre;
            Activo       = d.Activo;
            TramitesText = string.Join("\n", d.Tramites);
            NombreCorto  = d.NombreCorto;
            Descripcion  = d.Descripcion;
            LogoUrl      = d.LogoUrl;
            Color        = d.Color;
            return Page();
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnPostAsync([FromRoute] string? id, CancellationToken ct)
    {
        InstId = id;
        if (!ModelState.IsValid) return Page();

        var tramites = (TramitesText ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        try
        {
            if (id is null)
                await sender.Send(new CrearInstitucionCommand(Id, Nombre, tramites), ct);
            else
            {
                await sender.Send(new ActualizarInstitucionCommand(
                    id, Nombre, Activo, tramites, LogoUrl, NombreCorto, Color, Descripcion), ct);
                branding.InvalidarCache(id);
            }

            TempData["SuccessMsg"] = id is null ? "Institución creada." : "Institución actualizada.";
            return RedirectToPage("/Instituciones/Index");
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
            return Page();
        }
    }
}
