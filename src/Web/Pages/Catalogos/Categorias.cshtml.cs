using Diger.TramitesEstado.Application.Proyectos.Categorias;

namespace Diger.TramitesEstado.Web.Pages.Catalogos;

/// <summary>
/// CRUD del catálogo de categorías de proyecto: de qué trata el proyecto, que es distinto de la
/// acción —qué hace DIGER en él— y de la prioridad —cuánto pesa—.
///
/// <para>Módulo propio y solo para administradores, como se pidió. La categoría es opcional en el
/// proyecto, así que este catálogo no tiene predeterminada ni exige que quede alguna activa.</para>
/// </summary>
[Permission("Categorias.Proyectos", AccionModulo.Editar, "Administrar categorías de proyectos")]
[Authorize(Policy = "Categorias.Proyectos.Editar")]
public sealed class CategoriasModel(ISender sender) : PageModel
{
    public IReadOnlyList<CategoriaProyectoDto> Categorias { get; private set; } = [];
    public string? Error { get; set; }

    [BindProperty] public string        Nombre { get; set; } = string.Empty;
    [BindProperty] public int           Orden  { get; set; }
    [BindProperty] public ColorEtiqueta Color  { get; set; } = ColorEtiqueta.Gris;

    private async Task CargarAsync(CancellationToken ct) =>
        Categorias = await sender.Send(new GetCategoriasProyectoQuery(), ct);

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    public async Task<IActionResult> OnPostCrearAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Nombre))
        {
            Error = "El nombre de la categoría es obligatorio.";
            await CargarAsync(ct);
            return Page();
        }

        try
        {
            // Orden 0 significa «no me importa»: se va al final en vez de empatar con la primera.
            var orden = Orden > 0 ? Orden : Categorias.Count + 1;
            await sender.Send(new CrearCategoriaProyectoCommand(Nombre, orden, Color), ct);
            TempData["SuccessMsg"] = "Categoría creada.";
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
            await sender.Send(new ActualizarCategoriaProyectoCommand(id, nombre ?? "", orden, color, activo), ct);
            TempData["SuccessMsg"] = "Categoría actualizada.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new EliminarCategoriaProyectoCommand(id), ct);
            TempData["SuccessMsg"] = "Categoría eliminada.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        return RedirectToPage();
    }
}
