namespace Diger.TramitesEstado.Application.Areas.Queries;

public sealed record AreaListItemDto(string Id, string InstitucionId, string InstitucionNombre, string Nombre, string? NombreCorto, int Unidades);

public sealed record GetAreasQuery(string? InstitucionId = null) : IRequest<IReadOnlyList<AreaListItemDto>>;

public sealed class GetAreasQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetAreasQuery, IReadOnlyList<AreaListItemDto>>
{
    public async Task<IReadOnlyList<AreaListItemDto>> Handle(GetAreasQuery query, CancellationToken ct)
    {
        var q = ctx.Areas.AsNoTracking();
        if (!string.IsNullOrEmpty(query.InstitucionId))
            q = q.Where(a => a.InstitucionId == query.InstitucionId);

        return await q
            .OrderBy(a => a.InstitucionId).ThenBy(a => a.Nombre)
            .Select(a => new AreaListItemDto(
                a.Id, 
                a.InstitucionId, 
                ctx.Instituciones.Where(i => i.Id == a.InstitucionId).Select(i => i.Nombre).FirstOrDefault() ?? a.InstitucionId,
                a.Nombre, 
                a.NombreCorto,
                ctx.Unidades.Count(u => u.AreaId == a.Id)))
            .ToListAsync(ct);
    }
}
