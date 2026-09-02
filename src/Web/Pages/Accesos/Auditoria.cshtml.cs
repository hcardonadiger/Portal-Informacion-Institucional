namespace Diger.TramitesEstado.Web.Pages.Accesos;

/// <summary>
/// Lector de la bitácora de permisos. La tabla PermisoAuditoria se escribía desde el primer
/// día en cada otorgar/revocar y no tenía ninguna pantalla: un control de auditoría que nadie
/// puede consultar no es un control.
///
/// Pide Accesos.Permisos.Ver, la misma clave que la vista de consulta de la matriz — quien
/// puede ver quién tiene qué, puede ver cómo llegó a tenerlo.
/// </summary>
[Permission(PermisosConstants.ModuloPermisos, AccionModulo.Ver, "Ver la matriz de permisos")]
public sealed class AuditoriaModel(ISender sender) : PageModel
{
    public PagedResult<AuditoriaPermisoDto> Resultado { get; private set; } =
        PagedResult<AuditoriaPermisoDto>.Empty(Paginacion.TamanoDefecto);

    public IReadOnlyList<RolColumnaDto> Roles { get; private set; } = [];

    public string? RolId { get; private set; }
    public string? Q { get; private set; }
    public DateOnly? Desde { get; private set; }
    public DateOnly? Hasta { get; private set; }

    public async Task OnGetAsync(string? rolId, string? q, DateOnly? desde, DateOnly? hasta, int? pg, CancellationToken ct)
    {
        RolId = rolId; Q = q; Desde = desde; Hasta = hasta;

        // Los roles alimentan el filtro. Se listan los configurables: los administradores no
        // aparecen en la matriz, así que tampoco generan movimientos en la bitácora.
        Roles = (await sender.Send(new GetCatalogoPermisosQuery(), ct)).Roles;

        Resultado = await sender.Send(new GetAuditoriaPermisosQuery(rolId, q, desde, hasta, pg), ct);
    }

    /// <summary>Nombre del rol para mostrar; cae al código si el rol ya no existe — la
    /// bitácora sobrevive al borrado del rol a propósito.</summary>
    public string NombreRol(string rolId) =>
        Roles.FirstOrDefault(r => string.Equals(r.RolId, rolId, StringComparison.OrdinalIgnoreCase))?.Nombre ?? rolId;
}
