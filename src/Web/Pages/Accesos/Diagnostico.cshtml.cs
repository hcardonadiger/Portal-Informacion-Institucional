namespace Diger.TramitesEstado.Web.Pages.Accesos;

/// <summary>
/// Diagnóstico de acceso: dado un usuario, muestra de dónde sale lo que puede y lo que no.
/// Con 51 claves y roles que ahora edita gente no técnica, "¿por qué Fulano no puede entrar
/// a X?" se contestaba consultando cuatro tablas a mano.
///
/// Es de solo lectura y pide la misma clave que consultar la matriz.
/// </summary>
[Permission(PermisosConstants.ModuloPermisos, AccionModulo.Ver, "Ver la matriz de permisos")]
public sealed class DiagnosticoModel(ISender sender) : PageModel
{
    public PagedResult<UsuarioListItemDto> Usuarios { get; private set; } =
        PagedResult<UsuarioListItemDto>.Empty(Paginacion.TamanoDefecto);

    public DiagnosticoAccesoDto? Diagnostico { get; private set; }
    public IReadOnlyList<PermisoDto> Catalogo { get; private set; } = [];
    public string? Q { get; private set; }

    /// <summary>Claves del catálogo agrupadas por área, para contrastar contra lo concedido.</summary>
    public IReadOnlyList<(string Area, IReadOnlyList<PermisoDto> Permisos)> PorArea { get; private set; } = [];

    public async Task OnGetAsync(string? q, Guid? usuarioId, int? pg, CancellationToken ct)
    {
        Q = q;
        Usuarios = await sender.Send(new GetUsuariosQuery(q, pg), ct);

        if (usuarioId is not Guid id) return;

        Diagnostico = await sender.Send(new GetDiagnosticoAccesoQuery(id), ct);
        Catalogo    = (await sender.Send(new GetCatalogoPermisosQuery(), ct)).Catalogo;

        PorArea = Catalogo
            .GroupBy(p => CatalogoModulos.Obtener(p.Modulo).Area)
            .OrderBy(g => CatalogoModulos.Areas.ToList().IndexOf(g.Key))
            .Select(g => (Area: g.Key, Permisos: (IReadOnlyList<PermisoDto>)g
                .OrderBy(p => CatalogoModulos.Obtener(p.Modulo).Orden)
                .ThenBy(p => p.Accion)
                .ToList()))
            .ToList();
    }

    /// <summary>Claves efectivas de todos los roles del usuario juntas. Un usuario con varias
    /// asignaciones puede tener roles distintos; en sesión manda el del contexto activo, pero
    /// para diagnosticar interesa ver el conjunto.</summary>
    public HashSet<string> ClavesDeTodosLosRoles =>
        Diagnostico is null
            ? []
            : Diagnostico.Roles.SelectMany(r => r.Claves).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public bool AlgunRolEsAdministrador =>
        Diagnostico?.Roles.Any(r => r.EsAdministrador) == true;
}
