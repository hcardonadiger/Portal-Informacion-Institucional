namespace Diger.TramitesEstado.Application.Areas.Queries;

public sealed record AreaDetailDto(string Id, string InstitucionId, string Nombre, string? Descripcion, string? NombreCorto, string? LogoUrl, bool Activo);

public sealed record GetAreaByIdQuery(string Id) : IRequest<AreaDetailDto>;

public sealed class GetAreaByIdQueryHandler(IAreaRepository repo)
    : IRequestHandler<GetAreaByIdQuery, AreaDetailDto>
{
    public async Task<AreaDetailDto> Handle(GetAreaByIdQuery query, CancellationToken ct)
    {
        var area = await repo.GetByIdAsync(query.Id, ct)
            ?? throw new NotFoundException(nameof(Area), query.Id);
            
        return new AreaDetailDto(area.Id, area.InstitucionId, area.Nombre, area.Descripcion, area.NombreCorto, area.LogoUrl, area.Activo);
    }
}
