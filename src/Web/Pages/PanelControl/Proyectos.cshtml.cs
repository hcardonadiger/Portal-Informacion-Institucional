using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.PanelControl.Proyectos;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.PanelControl;

/// <summary>
/// Panel de control → Proyectos. Archiva (oculta del portafolio sin borrar nada) cualquier proyecto
/// y devuelve al portafolio los archivados.
///
/// <para>Clave propia y no <c>Proyectos.Eliminar</c>: esta pantalla ve todos los proyectos de
/// todas las instituciones, también los ya archivados, y eso es de administración. Por omisión
/// solo la tiene el administrador.</para>
/// </summary>
[Authorize]
[Permission(Clave, AccionModulo.Ver, "Ver los proyectos activos y archivados del Panel de control")]
public sealed class ProyectosModel(ISender sender) : PageModel
{
    public const string Clave = "PanelControl.Proyectos";

    [BindProperty(SupportsGet = true)] public bool    Archivados { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q          { get; set; }

    [BindProperty] public string? Motivo { get; set; }

    [TempData] public string? Aviso { get; set; }
    [TempData] public string? Error { get; set; }

    public IReadOnlyList<ProyectoAdministracionDto> Proyectos { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) =>
        Proyectos = await sender.Send(new GetProyectosAdministracionQuery(Archivados, Q), ct);

    [Permission(Clave, AccionModulo.Eliminar, "Archivar y restaurar proyectos desde el Panel de control")]
    public async Task<IActionResult> OnPostArchivarAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new ArchivarProyectoCommand(id, Motivo), ct);
            Aviso = "Proyecto archivado. Ya no aparece en el portafolio; puede restaurarlo desde «Archivados».";
        }
        catch (DomainException ex) { Error = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return RedirectToPage(new { Q });
    }

    [Permission(Clave, AccionModulo.Eliminar, "Archivar y restaurar proyectos desde el Panel de control")]
    public async Task<IActionResult> OnPostRestaurarAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new RestaurarProyectoCommand(id), ct);
            Aviso = "Proyecto restaurado al portafolio.";
        }
        catch (DomainException ex) { Error = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return RedirectToPage(new { Archivados = true, Q });
    }
}
