namespace Diger.TramitesEstado.Application.Unidades.Queries;

public sealed record UnidadDetailDto(string Id, string AreaId, string Nombre, string? Descripcion, string? NombreCorto, string? LogoUrl, bool Activo);

public sealed record GetUnidadByIdQuery(string Id) : IRequest<UnidadDetailDto>;

public sealed class GetUnidadByIdQueryHandler(IUnidadRepository repo)
    : IRequestHandler<GetUnidadByIdQuery, UnidadDetailDto>
{
    public async Task<UnidadDetailDto> Handle(GetUnidadByIdQuery query, CancellationToken ct)
    {
        var unidad = await repo.GetByIdAsync(query.Id, ct)
            ?? throw new NotFoundException(nameof(Unidad), query.Id);
            
        return new UnidadDetailDto(unidad.Id, unidad.AreaId, unidad.Nombre, unidad.Descripcion, unidad.NombreCorto, unidad.LogoUrl, unidad.Activo);
    }
}
