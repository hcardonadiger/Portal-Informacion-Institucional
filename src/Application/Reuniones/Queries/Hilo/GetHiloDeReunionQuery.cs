using Diger.TramitesEstado.Application.Reuniones.Common;

namespace Diger.TramitesEstado.Application.Reuniones.Queries.Hilo;

/// <summary>Devuelve el hilo de una reunión: su HiloId y las demás reuniones enlazadas
/// (ordenadas cronológicamente), para mostrarlas en el detalle de la reunión.</summary>
public sealed record GetHiloDeReunionQuery(int ReunionId) : IRequest<HiloDeReunionDto>;

public sealed class GetHiloDeReunionQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetHiloDeReunionQuery, HiloDeReunionDto>
{
    public async Task<HiloDeReunionDto> Handle(GetHiloDeReunionQuery q, CancellationToken ct)
    {
        var r = await ctx.Reuniones.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == q.ReunionId, ct)
            ?? throw new NotFoundException(nameof(Reunion), q.ReunionId);

        if (r.HiloId is null)
            return new HiloDeReunionDto(null, []);

        var otras = await ctx.Reuniones.AsNoTracking()
            .Where(x => x.HiloId == r.HiloId && x.Id != r.Id)
            .OrderBy(x => x.Fecha).ThenBy(x => x.Id)
            .Select(x => new HiloMiembroRefDto(x.Id, x.Titulo, x.Fecha, x.Institucion))
            .ToListAsync(ct);

        return new HiloDeReunionDto(r.HiloId, otras);
    }
}
