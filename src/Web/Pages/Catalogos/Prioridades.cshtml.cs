using Diger.TramitesEstado.Application.Proyectos.Prioridades;

namespace Diger.TramitesEstado.Web.Pages.Catalogos;

/// <summary>
/// CRUD del catálogo de prioridades de proyecto. Existe para que agregar una prioridad —«Q3» fue
/// la primera que pidieron— deje de ser un cambio de código y un despliegue.
///
/// <para>Módulo propio y no «Proyectos»: administrar el catálogo no es lo mismo que editar un
/// proyecto, y quien puede lo segundo no necesariamente debe poder lo primero. Un administrador
/// entra sin más porque su rol aprueba por código; a cualquier otro rol hay que otorgárselo en
/// la matriz de permisos.</para>
/// </summary>
[Permission("Prioridades.Proyectos", AccionModulo.Editar, "Administrar prioridades de proyectos")]
[Authorize(Policy = "Prioridades.Proyectos.Editar")]
public sealed class PrioridadesModel(ISender sender) : PageModel
{
    public IReadOnlyList<PrioridadProyectoDto> Prioridades { get; private set; } = [];
    public string? Error { get; set; }

    [BindProperty] public string        Nombre { get; set; } = string.Empty;
    [BindProperty] public int           Orden  { get; set; }
    [BindProperty] public ColorEtiqueta Color  { get; set; } = ColorEtiqueta.Gris;

    private async Task CargarAsync(CancellationToken ct) =>
        Prioridades = await sender.Send(new GetPrioridadesProyectoQuery(), ct);

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    public async Task<IActionResult> OnPostCrearAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Nombre))
        {
            Error = "El nombre de la prioridad es obligatorio.";
            await CargarAsync(ct);
            return Page();
        }

        try
        {
            // Orden 0 significa «no me importa»: se va al final en vez de empatar con la primera.
            var orden = Orden > 0 ? Orden : Prioridades.Count + 1;
            await sender.Send(new CrearPrioridadProyectoCommand(Nombre, orden, Color), ct);
            TempData["SuccessMsg"] = "Prioridad creada.";
            return RedirectToPage();
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
            await CargarAsync(ct);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostActualizarAsync(
        int id, string nombre, int orden, ColorEtiqueta color, bool activo, CancellationToken ct)
    {
        try
        {
            await sender.Send(new ActualizarPrioridadProyectoCommand(id, nombre ?? "", orden, color, activo), ct);
            TempData["SuccessMsg"] = "Prioridad actualizada.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPredeterminadaAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new MarcarPrioridadProyectoPredeterminadaCommand(id), ct);
            TempData["SuccessMsg"] = "Prioridad predeterminada actualizada.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new EliminarPrioridadProyectoCommand(id), ct);
            TempData["SuccessMsg"] = "Prioridad eliminada.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        return RedirectToPage();
    }
}
