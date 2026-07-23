using Diger.TramitesEstado.Application.Reuniones.Common;

namespace Diger.TramitesEstado.Application.Reuniones.Queries.Hilo;

/// <summary>Vista completa de un hilo: todas sus reuniones ordenadas cronológicamente,
/// cada una con sus acuerdos/compromisos (acciones y tareas) y su estado de seguimiento.</summary>
public sealed record GetHiloQuery(Guid HiloId) : IRequest<HiloDetalleDto>;

public sealed class GetHiloQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetHiloQuery, HiloDetalleDto>
{
    public async Task<HiloDetalleDto> Handle(GetHiloQuery q, CancellationToken ct)
    {
        var reuniones = await ctx.Reuniones.AsNoTracking()
            .Where(x => x.HiloId == q.HiloId)
            .OrderBy(x => x.Fecha).ThenBy(x => x.Id)
            .Select(x => new HiloReunionDto(
                x.Id, x.Titulo, x.Fecha, x.Institucion, x.Tipo,
                x.Acuerdos
                    .OrderBy(a => a.Orden)
                    .Select(a => new HiloAcuerdoDto(
                        a.Id, a.Compromiso, a.Responsable, a.Plazo,
                        a.Estado, a.FechaCumplimiento, a.NotaSeguimiento))
                    .ToList()))
            .ToListAsync(ct);

        if (reuniones.Count == 0)
            throw new NotFoundException("Hilo", q.HiloId);

        return new HiloDetalleDto(q.HiloId, reuniones);
    }
}
