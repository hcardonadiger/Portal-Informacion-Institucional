using Diger.TramitesEstado.Application.Proyectos.Insumos;
using FluentValidation;

namespace Diger.TramitesEstado.Web.Pages.Catalogos;

/// <summary>
/// Categorías del catálogo de insumos: cómo se agrupan al elegirlos en una actividad.
///
/// <para>Solo para el Administrador: la clave no se le otorga a ningún rol, y el administrador
/// la aprueba por código. La comparte con la página de insumos —es un solo catálogo—.</para>
///
/// <para>A diferencia de Categorías de proyecto, <b>no se elimina</b>: se desactiva. Una categoría
/// desactivada deja de ofrecer sus insumos, y las actividades que ya los tienen los conservan.</para>
/// </summary>
[Permission(CategoriasInsumoModel.Modulo, AccionModulo.Editar, "Administrar el catálogo de insumos")]
[Authorize(Policy = CategoriasInsumoModel.Clave)]
public sealed class CategoriasInsumoModel(ISender sender) : PageModel
{
    public const string Modulo = "Insumos.Catalogo";
    public const string Clave  = "Insumos.Catalogo.Editar";

    public IReadOnlyList<CategoriaInsumoDto> Categorias { get; private set; } = [];
    public string? Error { get; set; }

    [BindProperty] public string Nombre { get; set; } = string.Empty;
    [BindProperty] public int    Orden  { get; set; }

    /// <summary>El texto que ve el usuario: el de la regla, nunca el detalle técnico.</summary>
    internal static string Mensaje(Exception ex) =>
        ex is ValidationException v ? string.Join(" ", v.Errors.Select(e => e.ErrorMessage).Distinct()) : ex.Message;

    private async Task CargarAsync(CancellationToken ct) =>
        Categorias = await sender.Send(new GetCategoriasInsumoQuery(), ct);

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    public async Task<IActionResult> OnPostCrearAsync(CancellationToken ct)
    {
        try
        {
            await CargarAsync(ct);
            // Orden 0 significa «no me importa»: se va al final en vez de empatar con la primera.
            var orden = Orden > 0 ? Orden : Categorias.Count + 1;
            await sender.Send(new CrearCategoriaInsumoCommand(Nombre ?? "", orden), ct);
            TempData["SuccessMsg"] = "Categoría creada.";
            return RedirectToPage();
        }
        catch (Exception ex) when (ex is DomainException or ValidationException)
        {
            Error = Mensaje(ex);
            await CargarAsync(ct);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostActualizarAsync(int id, string nombre, int orden, bool activo, CancellationToken ct)
    {
        try
        {
            await sender.Send(new ActualizarCategoriaInsumoCommand(id, nombre ?? "", orden, activo), ct);
            TempData["SuccessMsg"] = "Categoría actualizada.";
        }
        catch (Exception ex) when (ex is DomainException or ValidationException)
        {
            TempData["ErrorMsg"] = Mensaje(ex);
        }
        return RedirectToPage();
    }
}
