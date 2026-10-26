using Diger.TramitesEstado.Web.Common;

namespace Diger.TramitesEstado.Web.Pages.Instituciones;

[Permission("Instituciones", AccionModulo.Editar, "Crear y editar instituciones")]
[Authorize(Policy = "Instituciones.Editar")]
public sealed class EditorModel(ISender sender, IInstitucionBrandingService branding) : PageModel
{
    public string? InstId { get; private set; }
    public InstitucionDetailDto? Detalle { get; private set; }

    private string _id = string.Empty;
    [BindProperty] public string  Id           { get => _id; set => _id = value?.ToUpperInvariant() ?? string.Empty; }
    [BindProperty] public string  Nombre       { get; set; } = string.Empty;
    [BindProperty] public bool    Activo       { get; set; } = true;
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

        try
        {
            if (id is null)
                await sender.Send(new CrearInstitucionCommand(Id, Nombre), ct);
            else
            {
                await sender.Send(new ActualizarInstitucionCommand(
                    id, Nombre, Activo, LogoUrl, NombreCorto, Color, Descripcion), ct);
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
