namespace Diger.TramitesEstado.Application.Instituciones.Queries.GetInstituciones;

public sealed record InstitucionListItemDto(
    int Id, string Nombre, bool Activo, int NumTramites,
    int Expedientes, int Tickets, int Reuniones, int Contactos);

public sealed record GetInstitucionesQuery(string? Q = null, int? Page = null, int? Size = null)
    : IRequest<PagedResult<InstitucionListItemDto>>;

public sealed class GetInstitucionesQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetInstitucionesQuery, PagedResult<InstitucionListItemDto>>
{
    // El catálogo es de Administrador (acceso global), por lo que las subconsultas de conteo no se ven
    // afectadas por el filtro por institución.
    public async Task<PagedResult<InstitucionListItemDto>> Handle(
        GetInstitucionesQuery query, CancellationToken ct)
    {
        var (q, page, size) = Paginacion.Normalizar(query.Q, query.Page, query.Size);

        var baseq = ctx.Instituciones.AsNoTracking();
        if (q is not null)
            baseq = baseq.Where(i => i.Nombre.Contains(q));

        var total = await baseq.CountAsync(ct);
        var items = await baseq
            .OrderBy(i => i.Nombre)
            .Skip((page - 1) * size).Take(size)
            .Select(i => new InstitucionListItemDto(
                i.Id, i.Nombre, i.Activo, i.Tramites.Count,
                ctx.Expedientes.Count(e => e.InstitucionId == i.Id),
                ctx.Tickets.Count(t => t.InstitucionId == i.Id),
                ctx.Reuniones.Count(r => r.InstitucionId == i.Id),
                ctx.Contactos.Count(c => c.InstitucionId == i.Id)))
            .ToListAsync(ct);

        return new PagedResult<InstitucionListItemDto>(items, total, page, size);
    }
}
