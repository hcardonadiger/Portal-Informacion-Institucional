using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Expedientes.Plantillas;

public sealed record GetPlantillasQuery : IRequest<IReadOnlyList<PlantillaListItemDto>>;

public sealed class GetPlantillasQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetPlantillasQuery, IReadOnlyList<PlantillaListItemDto>>
{
    public async Task<IReadOnlyList<PlantillaListItemDto>> Handle(GetPlantillasQuery q, CancellationToken ct)
        => await ctx.PlantillasTramite
            .OrderBy(p => p.Nombre)
            .Select(p => new PlantillaListItemDto(p.Id, p.Nombre, p.Activa, p.Legal.Count, p.Requisitos.Count))
            .ToListAsync(ct);
}

public sealed record GetPlantillaByIdQuery(int Id) : IRequest<PlantillaDetalleDto>;

public sealed class GetPlantillaByIdQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetPlantillaByIdQuery, PlantillaDetalleDto>
{
    public async Task<PlantillaDetalleDto> Handle(GetPlantillaByIdQuery q, CancellationToken ct)
    {
        var p = await ctx.PlantillasTramite
            .Include(x => x.Legal).Include(x => x.Requisitos)
            .FirstOrDefaultAsync(x => x.Id == q.Id, ct)
            ?? throw new NotFoundException(nameof(PlantillaTramite), q.Id);

        return ToDetalle(p);
    }

    internal static PlantillaDetalleDto ToDetalle(PlantillaTramite p) => new(
        p.Id, p.Nombre, p.Activa,
        p.Legal.OrderBy(l => l.Orden).Select(l => new PlantillaLegalItemDto(l.Id, l.Instrumento, l.Articulos, l.Obs)).ToList(),
        p.Requisitos.OrderBy(r => r.Orden).Select(r => new PlantillaRequisitoItemDto(r.Id, r.Requisito, r.Obs)).ToList());
}

/// <summary>Busca una plantilla activa por nombre exacto (usado por el wizard de Expedientes para el copiado automático).</summary>
public sealed record GetPlantillaPorNombreQuery(string Nombre) : IRequest<PlantillaDetalleDto?>;

public sealed class GetPlantillaPorNombreQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetPlantillaPorNombreQuery, PlantillaDetalleDto?>
{
    public async Task<PlantillaDetalleDto?> Handle(GetPlantillaPorNombreQuery q, CancellationToken ct)
    {
        var nombre = q.Nombre.Trim();
        var p = await ctx.PlantillasTramite
            .Include(x => x.Legal).Include(x => x.Requisitos)
            .FirstOrDefaultAsync(x => x.Activa && x.Nombre == nombre, ct);

        return p is null ? null : GetPlantillaByIdQueryHandler.ToDetalle(p);
    }
}
