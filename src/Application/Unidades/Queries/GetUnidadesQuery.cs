namespace Diger.TramitesEstado.Application.Unidades.Queries;

public sealed record UnidadListItemDto(string Id, string AreaId, string AreaNombre, string Nombre, string? NombreCorto);

public sealed record GetUnidadesQuery(string? AreaId = null) : IRequest<IReadOnlyList<UnidadListItemDto>>;

public sealed class GetUnidadesQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetUnidadesQuery, IReadOnlyList<UnidadListItemDto>>
{
    public async Task<IReadOnlyList<UnidadListItemDto>> Handle(GetUnidadesQuery query, CancellationToken ct)
    {
        var q = ctx.Unidades.AsNoTracking();
        if (!string.IsNullOrEmpty(query.AreaId))
            q = q.Where(u => u.AreaId == query.AreaId);

        return await q
            .OrderBy(u => u.AreaId).ThenBy(u => u.Nombre)
            .Select(u => new UnidadListItemDto(
                u.Id, 
                u.AreaId, 
                ctx.Areas.Where(a => a.Id == u.AreaId).Select(a => a.Nombre).FirstOrDefault() ?? u.AreaId,
                u.Nombre, 
                u.NombreCorto))
            .ToListAsync(ct);
    }
}
