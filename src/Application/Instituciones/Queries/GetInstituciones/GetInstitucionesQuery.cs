namespace Diger.TramitesEstado.Application.Instituciones.Queries.GetInstituciones;

public sealed record InstitucionListItemDto(int Id, string Nombre, bool Activo, int NumTramites);

public sealed record GetInstitucionesQuery : IRequest<IReadOnlyList<InstitucionListItemDto>>;

public sealed class GetInstitucionesQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetInstitucionesQuery, IReadOnlyList<InstitucionListItemDto>>
{
    public async Task<IReadOnlyList<InstitucionListItemDto>> Handle(
        GetInstitucionesQuery _, CancellationToken ct) =>
        await ctx.Instituciones
            .AsNoTracking()
            .OrderBy(i => i.Nombre)
            .Select(i => new InstitucionListItemDto(
                i.Id, i.Nombre, i.Activo, i.Tramites.Count))
            .ToListAsync(ct);
}
