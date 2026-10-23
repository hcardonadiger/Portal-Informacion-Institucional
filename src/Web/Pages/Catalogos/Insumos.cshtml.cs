using Diger.TramitesEstado.Application.Proyectos.Insumos;

namespace Diger.TramitesEstado.Web.Pages.Catalogos;

/// <summary>
/// El catálogo de insumos: lo que una actividad puede necesitar —materiales, equipo,
/// servicios—, elegido después en la ficha del proyecto. Sin cantidad, costo ni inventario.
///
/// <para>Misma clave que <see cref="CategoriasInsumoModel"/>: es un solo catálogo, solo para el
/// Administrador. Tampoco se elimina nada: un insumo se desactiva y las actividades que ya lo
/// tienen lo conservan.</para>
///
/// <para>Puede crecer a cientos de filas, así que filtra por texto y por categoría del lado del
/// servidor, y guardar una fila vuelve al mismo filtro.</para>
/// </summary>
[Permission(CategoriasInsumoModel.Modulo, AccionModulo.Editar, "Administrar el catálogo de insumos")]
[Authorize(Policy = CategoriasInsumoModel.Clave)]
public sealed class InsumosModel(ISender sender) : PageModel
{
    public IReadOnlyList<InsumoDto>          Insumos    { get; private set; } = [];
    public IReadOnlyList<CategoriaInsumoDto> Categorias { get; private set; } = [];
    public string? Error { get; set; }

    /// <summary>Si el catálogo tiene algún insumo, sin filtrar: distingue «aún no hay» de «nada
    /// coincide con la búsqueda».</summary>
    public bool HayAlguno { get; private set; }

    [BindProperty(SupportsGet = true)] public string? Q   { get; set; }
    [BindProperty(SupportsGet = true)] public int?    Cat { get; set; }

    [BindProperty] public int     NuevoCategoriaId  { get; set; }
    [BindProperty] public string  NuevoNombre       { get; set; } = string.Empty;
    [BindProperty] public string? NuevoDescripcion  { get; set; }

    public bool Filtrado => !string.IsNullOrWhiteSpace(Q) || Cat is not null;

    private async Task CargarAsync(CancellationToken ct)
    {
        Categorias = await sender.Send(new GetCategoriasInsumoQuery(), ct);
        Insumos    = await sender.Send(new GetInsumosQuery(Q, Cat), ct);
        HayAlguno  = Insumos.Count > 0 || Categorias.Any(c => c.Insumos > 0);
    }

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    public async Task<IActionResult> OnPostCrearAsync(CancellationToken ct)
    {
        try
        {
            await sender.Send(new CrearInsumoCommand(NuevoCategoriaId, NuevoNombre ?? "", NuevoDescripcion), ct);
            TempData["SuccessMsg"] = "Insumo creado.";
            return RedirectToPage(new { Q, Cat });
        }
        catch (Exception ex) when (ex is DomainException or FluentValidation.ValidationException)
        {
            Error = CategoriasInsumoModel.Mensaje(ex);
            await CargarAsync(ct);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostActualizarAsync(
        int id, int categoriaId, string nombre, string? descripcion, bool activo, CancellationToken ct)
    {
        try
        {
            await sender.Send(new ActualizarInsumoCommand(id, categoriaId, nombre ?? "", descripcion, activo), ct);
            TempData["SuccessMsg"] = "Insumo actualizado.";
        }
        catch (Exception ex) when (ex is DomainException or FluentValidation.ValidationException)
        {
            TempData["ErrorMsg"] = CategoriasInsumoModel.Mensaje(ex);
        }
        // Al mismo filtro: con cientos de filas, perderlo en cada guardado obliga a buscar de nuevo.
        return RedirectToPage(new { Q, Cat });
    }
}
