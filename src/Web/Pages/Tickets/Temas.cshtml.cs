namespace Diger.TramitesEstado.Web.Pages.Tickets;

[Authorize(Roles = nameof(RolUsuario.Administrador))]
public sealed class TemasModel(ISender sender) : PageModel
{
    public IReadOnlyList<TemaAdminDto> Temas { get; private set; } = [];
    public IReadOnlyList<CategoriaAdminDto> Categorias { get; private set; } = [];
    public string? Error { get; set; }

    [BindProperty] public string Nombre { get; set; } = string.Empty;
    [BindProperty] public int    HorasResolucion { get; set; }
    [BindProperty] public int?   CategoriaId { get; set; }
    [BindProperty] public string NombreCategoria { get; set; } = string.Empty;

    private async Task CargarAsync(CancellationToken ct)
    {
        Categorias = await sender.Send(new GetCategoriasQuery(), ct);
        Temas      = await sender.Send(new GetTemasQuery(), ct);
    }

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    // ── Categorías ──────────────────────────────────────────────────────────
    public async Task<IActionResult> OnPostCrearCategoriaAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(NombreCategoria))
        {
            Error = "El nombre de la categoría es obligatorio.";
            await CargarAsync(ct);
            return Page();
        }
        try { await sender.Send(new CrearCategoriaCommand(NombreCategoria), ct); TempData["SuccessMsg"] = "Categoría creada."; return RedirectToPage(); }
        catch (DomainException ex) { Error = ex.Message; await CargarAsync(ct); return Page(); }
    }

    public async Task<IActionResult> OnPostActualizarCategoriaAsync(int id, string nombre, bool activo, CancellationToken ct)
    {
        try { await sender.Send(new ActualizarCategoriaCommand(id, nombre ?? "", activo), ct); TempData["SuccessMsg"] = "Categoría actualizada."; }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEliminarCategoriaAsync(int id, CancellationToken ct)
    {
        try { await sender.Send(new EliminarCategoriaCommand(id), ct); TempData["SuccessMsg"] = "Categoría eliminada."; }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        return RedirectToPage();
    }

    // ── Temas ───────────────────────────────────────────────────────────────
    public async Task<IActionResult> OnPostCrearAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Nombre))
        {
            Error = "El nombre del tema es obligatorio.";
            await CargarAsync(ct);
            return Page();
        }
        try
        {
            await sender.Send(new CrearTemaCommand(Nombre, Math.Max(0, HorasResolucion), CategoriaId), ct);
            TempData["SuccessMsg"] = "Tema creado.";
            return RedirectToPage();
        }
        catch (DomainException ex) { Error = ex.Message; await CargarAsync(ct); return Page(); }
    }

    public async Task<IActionResult> OnPostActualizarAsync(int id, string nombre, int horas, bool activo, int? categoriaId, CancellationToken ct)
    {
        try
        {
            await sender.Send(new ActualizarTemaCommand(id, nombre ?? "", Math.Max(0, horas), activo, categoriaId), ct);
            TempData["SuccessMsg"] = "Tema actualizado.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        try { await sender.Send(new EliminarTemaCommand(id), ct); TempData["SuccessMsg"] = "Tema eliminado."; }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        return RedirectToPage();
    }
}
