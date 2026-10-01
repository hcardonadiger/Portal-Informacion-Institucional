using Diger.TramitesEstado.Application.Permisos;
using Diger.TramitesEstado.Web.Common;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Pages.Accesos;

public sealed record OpcionOrg(string Id, string Nombre, string? PadreId);

public sealed record ModuloConAmbito(
    ModuloInfo Info,
    IReadOnlyList<ModuloAmbito> Concesiones)
{
    /// <summary>Sin concesiones el módulo está abierto: la tabla solo declara excepciones.</summary>
    public bool Limitado => Concesiones.Count > 0;
}

/// <summary>
/// Limita módulos a áreas o unidades concretas. Es la otra mitad de la pregunta que responde
/// <see cref="Permisos"/>: allá se define qué puede hacer un rol, acá dónde está disponible el
/// módulo. Las dos se componen en <c>PermissionCache</c>, que es lo que impide que el menú
/// muestre algo que después da «Acceso denegado».
///
/// <para>Abierto por defecto: un módulo sin filas lo ven todos. Se restringe lo que se quiere
/// restringir, no hay que configurar los veinticuatro.</para>
/// </summary>
[Permission("Accesos.Modulos", AccionModulo.Ver, "Ver el alcance de los módulos")]
public sealed class ModulosModel(
    IApplicationDbContext ctx,
    IPermissionCache cache,
    ICurrentUserService currentUser) : PageModel
{
    public IReadOnlyList<IGrouping<string, ModuloConAmbito>> Grupos { get; private set; } = [];
    public IReadOnlyList<OpcionOrg> Areas    { get; private set; } = [];
    public IReadOnlyList<OpcionOrg> Unidades { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    /// <summary>
    /// Cómo se lee una concesión en pantalla. Se muestra el nombre y no el Id: quien configura
    /// esto reconoce «Digitalización de Trámites», no «DITRA».
    /// </summary>
    public string Etiqueta(ModuloAmbito a)
    {
        if (a.UnidadId is not null)
            return Unidades.FirstOrDefault(u => u.Id == a.UnidadId)?.Nombre ?? a.UnidadId;

        var area = Areas.FirstOrDefault(x => x.Id == a.AreaId)?.Nombre ?? a.AreaId;
        return area is null ? a.InstitucionId : $"{area} (toda el área)";
    }

    private async Task CargarAsync(CancellationToken ct)
    {
        // Las claves del catálogo dan los módulos; el ámbito se aplica al módulo, no a la acción.
        var modulos = await ctx.Permisos.AsNoTracking()
            .Where(p => p.Activo)
            .Select(p => p.Modulo)
            .Distinct()
            .ToListAsync(ct);

        var ambitos = await ctx.ModuloAmbitos.AsNoTracking().ToListAsync(ct);

        Grupos = modulos
            .Select(m => new ModuloConAmbito(
                CatalogoModulos.Obtener(m),
                ambitos.Where(a => string.Equals(a.Modulo, m, StringComparison.OrdinalIgnoreCase))
                       .OrderBy(a => a.AreaId).ThenBy(a => a.UnidadId).ToList()))
            .OrderBy(x => CatalogoModulos.Areas.ToList().IndexOf(x.Info.Area))
            .ThenBy(x => x.Info.Orden)
            .GroupBy(x => x.Info.Area)
            .ToList();

        // Solo el alcance del administrador: no se configura lo que no se administra.
        var areas = ctx.Areas.AsNoTracking().AsQueryable();
        if (!currentUser.EsGlobal)
            areas = areas.Where(a => a.InstitucionId == currentUser.ActiveInstitucionId);

        Areas = await areas.OrderBy(a => a.Nombre)
            .Select(a => new OpcionOrg(a.Id, a.Nombre, a.InstitucionId)).ToListAsync(ct);

        var idsArea = Areas.Select(a => a.Id).ToList();
        Unidades = await ctx.Unidades.AsNoTracking()
            .Where(u => idsArea.Contains(u.AreaId))
            .OrderBy(u => u.Nombre)
            .Select(u => new OpcionOrg(u.Id, u.Nombre, u.AreaId)).ToListAsync(ct);
    }

    [Permission("Accesos.Modulos", AccionModulo.Editar, "Configurar el alcance de los módulos")]
    public async Task<IActionResult> OnPostAgregarAsync(
        string modulo, string areaId, string? unidadId, CancellationToken ct)
    {
        var area = await ctx.Areas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == areaId, ct);
        if (area is null)
        {
            TempData["ErrorMsg"] = "El área indicada no existe.";
            return RedirectToPage();
        }

        // ModuloAmbitos NO lleva filtro global de alcance —el caché de permisos la lee desde un
        // scope sin usuario, y filtrarla haría que viera cero filas y dejara todo abierto—, así
        // que el ancla institucional se comprueba acá a mano. Sin esto, quien tenga delegada la
        // clave Accesos.Modulos.Editar podría mandar el Id de un área de otra institución: la
        // pantalla solo filtra lo que muestra, no lo que acepta.
        if (!currentUser.EsGlobal && area.InstitucionId != currentUser.ActiveInstitucionId)
            return Forbid();

        // La institución sale del área, no del usuario activo: así un administrador global
        // —que no tiene institución activa— también puede configurarlo.
        var unidad = string.IsNullOrWhiteSpace(unidadId) ? null : unidadId;

        // El filtrado de unidades por área que hace el JavaScript es comodidad de pantalla, no
        // un control: una unidad de otra área daría una concesión que no alcanza a nadie.
        if (unidad is not null &&
            !await ctx.Unidades.AnyAsync(u => u.Id == unidad && u.AreaId == areaId, ct))
        {
            TempData["ErrorMsg"] = "Esa unidad no pertenece al área seleccionada.";
            return RedirectToPage();
        }

        var repetida = await ctx.ModuloAmbitos.AnyAsync(
            a => a.Modulo == modulo && a.InstitucionId == area.InstitucionId
              && a.AreaId == areaId && a.UnidadId == unidad, ct);
        if (repetida)
        {
            TempData["ErrorMsg"] = "Ese alcance ya estaba configurado.";
            return RedirectToPage();
        }

        try
        {
            ctx.ModuloAmbitos.Add(ModuloAmbito.Crear(modulo, area.InstitucionId, areaId, unidad));
            await ctx.SaveChangesAsync(ct);
        }
        catch (DomainException ex)
        {
            TempData["ErrorMsg"] = ex.Message;
            return RedirectToPage();
        }

        // El cambio afecta a todos los roles, no a uno: la clave del cache lleva el ámbito.
        cache.InvalidarTodo();
        TempData["SuccessMsg"] = $"«{CatalogoModulos.Obtener(modulo).Etiqueta}» quedó limitado.";
        return RedirectToPage();
    }

    [Permission("Accesos.Modulos", AccionModulo.Editar, "Configurar el alcance de los módulos")]
    public async Task<IActionResult> OnPostQuitarAsync(int id, CancellationToken ct)
    {
        // Mismo motivo que en Agregar: la tabla no lleva filtro de alcance, así que borrar por
        // Id suelto dejaría quitarle una restricción a otra institución.
        var fila = await ctx.ModuloAmbitos.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (fila is not null && !currentUser.EsGlobal && fila.InstitucionId != currentUser.ActiveInstitucionId)
            return Forbid();

        if (fila is not null)
        {
            var modulo = fila.Modulo;
            ctx.ModuloAmbitos.Remove(fila);
            await ctx.SaveChangesAsync(ct);
            cache.InvalidarTodo();

            var quedan = await ctx.ModuloAmbitos.AnyAsync(a => a.Modulo == modulo, ct);
            TempData["SuccessMsg"] = quedan
                ? "Alcance quitado."
                : $"«{CatalogoModulos.Obtener(modulo).Etiqueta}» vuelve a estar disponible para todos.";
        }
        return RedirectToPage();
    }
}
