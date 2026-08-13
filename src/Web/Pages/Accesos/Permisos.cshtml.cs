namespace Diger.TramitesEstado.Web.Pages.Accesos;

/// <summary>Una fila de la grilla: un módulo con sus cuatro acciones, o hueco donde la
/// acción no existe en el catálogo.</summary>
public sealed record FilaModulo(ModuloInfo Modulo, IReadOnlyDictionary<AccionModulo, string?> Claves);

/// <summary>
/// Administración de la matriz rol×permiso.
///
/// Se edita UN rol a la vez, no los cinco en paralelo. El diseño anterior era una matriz de
/// 48 permisos × 5 roles: 240 casillas sin buscador, donde la estructura Módulo×Acción —que
/// es la idea entera del modelo— quedaba escondida detrás de nombres en prosa. Acá cada
/// módulo es una fila con cuatro columnas fijas (Ver/Crear/Editar/Eliminar) y un guion donde
/// la acción no existe, así se lee de un vistazo qué se puede hacer sobre cada cosa.
///
/// La matriz completa sigue disponible en ?vista=comparar, de solo lectura: comparar roles
/// era lo único que el diseño viejo hacía bien.
///
/// El gateo es por handler: consultar pide Accesos.Permisos.Ver y guardar
/// Accesos.Permisos.Editar (la clave que protege la guardia anti-bloqueo).
/// </summary>
[Permission(PermisosConstants.ModuloPermisos, AccionModulo.Ver, "Ver la matriz de permisos")]
public sealed class PermisosModel(ISender sender) : PageModel
{
    public IReadOnlyList<RolColumnaDto> Roles { get; private set; } = [];
    public RolColumnaDto? Rol { get; private set; }
    public string? Q { get; private set; }
    public bool Comparar { get; private set; }
    public string? Error { get; set; }

    /// <summary>Filas agrupadas por área, en el orden de CatalogoModulos.Areas.</summary>
    public IReadOnlyList<(string Area, IReadOnlyList<FilaModulo> Filas)> Areas { get; private set; } = [];

    /// <summary>Claves otorgadas, por rol. En el editor solo se usa la del rol activo.</summary>
    public Dictionary<string, HashSet<string>> Matriz { get; private set; } = [];

    public static readonly AccionModulo[] Acciones =
        [AccionModulo.Ver, AccionModulo.Crear, AccionModulo.Editar, AccionModulo.Eliminar];

    public int Otorgados(string rolId) => Matriz.TryGetValue(rolId, out var s) ? s.Count : 0;

    public bool Tiene(string? clave) =>
        clave is not null && Rol is not null
        && Matriz.TryGetValue(Rol.RolId, out var s) && s.Contains(clave);

    /// <summary>Una casilla de mutación en un rol de solo lectura no se puede marcar: la
    /// capacidad del rol manda sobre la matriz (y el handler lo rechaza igual).</summary>
    public bool Bloqueada(AccionModulo accion) =>
        Rol?.EsSoloLectura == true && accion != AccionModulo.Ver;

    public async Task OnGetAsync(string? rol, string? q, string? vista, CancellationToken ct)
    {
        Q = q;
        Comparar = string.Equals(vista, "comparar", StringComparison.OrdinalIgnoreCase);
        await CargarAsync(rol, ct);
    }

    private async Task CargarAsync(string? rolSeleccionado, CancellationToken ct)
    {
        var data = await sender.Send(new GetCatalogoPermisosQuery(), ct);

        Roles  = data.Roles;
        Matriz = data.Matriz.ToDictionary(m => m.RolId, m => m.Permisos.ToHashSet(), StringComparer.OrdinalIgnoreCase);

        Rol = Roles.FirstOrDefault(r => string.Equals(r.RolId, rolSeleccionado, StringComparison.OrdinalIgnoreCase))
              ?? Roles.FirstOrDefault();

        // Se agrupa siempre sobre el catálogo completo (el filtro de búsqueda es de la vista,
        // no del guardado: el POST manda las claves visibles y las ocultas se conservan por
        // el hidden "presentes" — ver OnPostAsync).
        var filtro = (Q ?? "").Trim();

        Areas = data.Catalogo
            .GroupBy(p => p.Modulo, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var info = CatalogoModulos.Obtener(g.Key);
                var claves = Acciones.ToDictionary(
                    a => a,
                    a => g.FirstOrDefault(p => p.Accion == a)?.Clave);
                return new FilaModulo(info, claves);
            })
            .Where(f => filtro.Length == 0
                     || f.Modulo.Etiqueta.Contains(filtro, StringComparison.OrdinalIgnoreCase)
                     || f.Modulo.Clave.Contains(filtro, StringComparison.OrdinalIgnoreCase))
            .GroupBy(f => f.Modulo.Area)
            .OrderBy(g => CatalogoModulos.Areas.ToList().IndexOf(g.Key))
            .Select(g => (Area: g.Key,
                          Filas: (IReadOnlyList<FilaModulo>)g
                              .OrderBy(f => f.Modulo.Orden)
                              .ThenBy(f => f.Modulo.Etiqueta)
                              .ToList()))
            .ToList();
    }

    /// <summary>
    /// Guarda un solo rol. El formulario manda las claves marcadas en "otorgadas" y TODAS las
    /// que estaban en pantalla en "presentes": el comando reemplaza la lista completa del rol,
    /// así que sin ese segundo campo un filtro de búsqueda activo borraría en silencio todo lo
    /// que no estuviera visible al guardar.
    /// </summary>
    [Permission(PermisosConstants.ModuloPermisos, AccionModulo.Editar, "Administrar matriz de permisos")]
    public async Task<IActionResult> OnPostAsync(
        string rol, List<string>? otorgadas, List<string>? presentes, string? q, CancellationToken ct)
    {
        var marcadas  = (otorgadas ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var enPantalla = (presentes ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var data = await sender.Send(new GetCatalogoPermisosQuery(), ct);
        var yaOtorgadas = data.Matriz
            .FirstOrDefault(m => string.Equals(m.RolId, rol, StringComparison.OrdinalIgnoreCase))
            ?.Permisos ?? [];

        // Estado final = lo marcado en pantalla + lo que ya tenía y no se mostró.
        var final = marcadas
            .Concat(yaOtorgadas.Where(c => !enPantalla.Contains(c)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        try
        {
            await sender.Send(new GuardarMatrizPermisosCommand(
                new Dictionary<string, IReadOnlyList<string>> { [rol] = final }), ct);

            TempData["SuccessMsg"] = "Permisos actualizados.";
            return RedirectToPage(new { rol, q });
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
            Q = q;
            await CargarAsync(rol, ct);
            return Page();
        }
    }
}
