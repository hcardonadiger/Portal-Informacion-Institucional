namespace Diger.TramitesEstado.Application.Areas.Queries;

public sealed record AreaListItemDto(string Id, string InstitucionId, string InstitucionNombre, string Nombre, string? NombreCorto, int Unidades, bool Activo);

public sealed record GetAreasQuery(string? InstitucionId = null) : IRequest<IReadOnlyList<AreaListItemDto>>;

public sealed class GetAreasQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetAreasQuery, IReadOnlyList<AreaListItemDto>>
{
    public async Task<IReadOnlyList<AreaListItemDto>> Handle(GetAreasQuery query, CancellationToken ct)
    {
        var q = ctx.Areas.AsNoTracking();
        if (!string.IsNullOrEmpty(query.InstitucionId))
            q = q.Where(a => a.InstitucionId == query.InstitucionId);

        // El nombre de la institución se trae aparte y se combina con InstitucionId (el
        // fallback si no se encuentra) en memoria, no en el propio SELECT: Institucion.Nombre
        // tiene colación insensible a tildes (Modern_Spanish_CI_AI) y Area.InstitucionId usa la
        // colación por defecto de la base — un COALESCE entre las dos en SQL Server revienta con
        // "Cannot resolve collation conflict" en el CASE que genera el ??.
        var filas = await q
            .OrderBy(a => a.InstitucionId).ThenBy(a => a.Nombre)
            .Select(a => new
            {
                a.Id,
                a.InstitucionId,
                InstitucionNombre = ctx.Instituciones.Where(i => i.Id == a.InstitucionId).Select(i => i.Nombre).FirstOrDefault(),
                a.Nombre,
                a.NombreCorto,
                Unidades = ctx.Unidades.Count(u => u.AreaId == a.Id),
                a.Activo
            })
            .ToListAsync(ct);

        return filas
            .Select(f => new AreaListItemDto(
                f.Id, f.InstitucionId, f.InstitucionNombre ?? f.InstitucionId, f.Nombre, f.NombreCorto, f.Unidades, f.Activo))
            .ToList();
    }
}
