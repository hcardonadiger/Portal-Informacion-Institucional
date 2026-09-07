using Diger.TramitesEstado.Application.Usuarios.Commands.EliminarUsuario;

namespace Diger.TramitesEstado.Web.Pages.Usuarios;

[Permission("Usuarios", AccionModulo.Ver, "Ver usuarios")]
[Authorize(Policy = "Usuarios.Ver")]
public sealed class IndexModel(ISender sender, AccesoModulosService acceso) : PageModel
{
    public PagedResult<UsuarioListItemDto> Resultado { get; private set; } = PagedResult<UsuarioListItemDto>.Empty(Paginacion.TamanoDefecto);
    public string? Q { get; private set; }

    /// <summary>Casilla «Mostrar eliminados». Solo la honra un administrador: es quien puede
    /// eliminar y restaurar, y para el resto los eliminados sencillamente no existen.</summary>
    public bool VerEliminados { get; private set; }

    /// <summary>Eliminar y restaurar no tienen clave otorgable: los decide la capacidad
    /// <c>EsAdministrador</c> del rol. Acá solo decide qué se pinta; quien realmente lo exige es
    /// <c>EliminarUsuarioCommandHandler</c>.</summary>
    public bool PuedeEliminar => acceso.EsAdministrador;

    public async Task OnGetAsync(string? q, int? pg, bool eliminados, CancellationToken ct)
    {
        Q = q;
        VerEliminados = eliminados && acceso.EsAdministrador;
        Resultado = await sender.Send(new GetUsuariosQuery(q, pg, IncluirEliminados: VerEliminados), ct);
    }

    // Las dos guardas consultan `acceso` directamente y no una propiedad de la página: en un POST,
    // ASP.NET construye un PageModel nuevo y ejecuta solo el handler, así que cualquier bandera
    // que se calcule en OnGetAsync llega en false. Ya pasó antes en cinco páginas del portal.
    public async Task<IActionResult> OnPostEliminarAsync(Guid id, string? q, bool eliminados, CancellationToken ct)
    {
        if (!acceso.EsAdministrador) return Forbid();

        await sender.Send(new EliminarUsuarioCommand(id), ct);
        TempData["SuccessMsg"] = "Usuario eliminado. Puede recuperarlo con «Mostrar eliminados».";
        return RedirectToPage(new { q, eliminados });
    }

    public async Task<IActionResult> OnPostRestaurarAsync(Guid id, string? q, CancellationToken ct)
    {
        if (!acceso.EsAdministrador) return Forbid();

        await sender.Send(new RestaurarUsuarioCommand(id), ct);
        TempData["SuccessMsg"] = "Usuario restaurado.";
        return RedirectToPage(new { q, eliminados = true });
    }
}
