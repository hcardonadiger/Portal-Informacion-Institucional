namespace Diger.TramitesEstado.Application.Permisos;

public sealed record AuditoriaPermisoDto(
    string RolId, string PermisoClave, string PermisoNombre,
    AccionPermiso Accion, string Actor, DateTime Fecha);

/// <summary>
/// Bitácora de cambios en la matriz de permisos: quién otorgó o revocó qué clave, a qué rol
/// y cuándo. Lee PermisoAuditoria, que se escribe en cada guardado de la matriz.
///
/// Se consulta sobre el snapshot que guardó la bitácora, no contra el catálogo vigente: un
/// permiso renombrado o desactivado, o un rol eliminado, tienen que seguir siendo legibles —
/// para eso PermisoAuditoria guarda PermisoNombre y no tiene FK a Roles.
/// </summary>
public sealed record GetAuditoriaPermisosQuery(
    string? RolId = null,
    string? Q = null,
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    int? Pagina = null) : IRequest<PagedResult<AuditoriaPermisoDto>>;

public sealed class GetAuditoriaPermisosQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetAuditoriaPermisosQuery, PagedResult<AuditoriaPermisoDto>>
{
    public async Task<PagedResult<AuditoriaPermisoDto>> Handle(GetAuditoriaPermisosQuery q, CancellationToken ct)
    {
        var (texto, pagina, tamano) = Paginacion.Normalizar(q.Q, q.Pagina, null);

        var consulta = ctx.PermisosAuditoria.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.RolId))
            consulta = consulta.Where(a => a.RolId == q.RolId);

        if (texto is not null)
            consulta = consulta.Where(a =>
                a.PermisoClave.Contains(texto) ||
                a.PermisoNombre.Contains(texto) ||
                a.Actor.Contains(texto));

        if (q.Desde is DateOnly d)
            consulta = consulta.Where(a => a.Fecha >= d.ToDateTime(TimeOnly.MinValue));

        // El filtro "hasta" incluye el día completo: la bitácora guarda hora, así que comparar
        // contra la medianoche de ese día dejaría fuera todo lo ocurrido durante la jornada.
        if (q.Hasta is DateOnly h)
            consulta = consulta.Where(a => a.Fecha < h.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var total = await consulta.CountAsync(ct);

        var items = await consulta
            .OrderByDescending(a => a.Fecha).ThenByDescending(a => a.Id)
            .Skip((pagina - 1) * tamano).Take(tamano)
            .Select(a => new AuditoriaPermisoDto(
                a.RolId, a.PermisoClave, a.PermisoNombre, a.Accion, a.Actor, a.Fecha))
            .ToListAsync(ct);

        return new PagedResult<AuditoriaPermisoDto>(items, total, pagina, tamano);
    }
}
