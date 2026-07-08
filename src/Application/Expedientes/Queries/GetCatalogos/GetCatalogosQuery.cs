using Diger.TramitesEstado.Application.Common.Catalogs;

namespace Diger.TramitesEstado.Application.Expedientes.Queries.GetCatalogos;

public sealed record InstitucionOpcion(string Id, string Nombre);
public sealed record InfraGrupoDto(string Grupo, IReadOnlyList<string> Items);

public sealed record CatalogosDto(
    IReadOnlyList<InstitucionOpcion> Instituciones,
    IReadOnlyList<string>            TgrInstituciones,
    IReadOnlyList<string>            Perfiles,
    IReadOnlyList<string>            DatacenterCond,
    IReadOnlyList<InfraGrupoDto>     InfraReqs,
    IReadOnlyList<string>            InfraStatus);

public sealed record GetCatalogosQuery : IRequest<CatalogosDto>;

public sealed class GetCatalogosQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetCatalogosQuery, CatalogosDto>
{
    public async Task<CatalogosDto> Handle(GetCatalogosQuery _, CancellationToken ct)
    {
        var instituciones = await ctx.Instituciones
            .Where(i => i.Activo)
            .OrderBy(i => i.Nombre)
            .Select(i => new InstitucionOpcion(i.Id, i.Nombre))
            .ToListAsync(ct);

        return new CatalogosDto(
            instituciones,
            TgrCatalog.Instituciones,
            InfraCatalog.Perfiles,
            InfraCatalog.DatacenterCond,
            InfraCatalog.Reqs.Select(g => new InfraGrupoDto(g.Grupo, g.Items)).ToList(),
            Enum.GetNames<InfraStatus>());
    }
}
