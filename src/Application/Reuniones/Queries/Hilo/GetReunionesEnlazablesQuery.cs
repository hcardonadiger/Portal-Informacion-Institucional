using Diger.TramitesEstado.Application.Reuniones.Common;

namespace Diger.TramitesEstado.Application.Reuniones.Queries.Hilo;

/// <summary>Lista de reuniones candidatas a enlazar con una reunión dada: excluye la propia
/// y las que ya están en su mismo hilo. Filtra por texto (título/institución) para el selector.</summary>
public sealed record GetReunionesEnlazablesQuery(int ReunionId, string? Q = null)
    : IRequest<IReadOnlyList<HiloMiembroRefDto>>;

public sealed class GetReunionesEnlazablesQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetReunionesEnlazablesQuery, IReadOnlyList<HiloMiembroRefDto>>
{
    public async Task<IReadOnlyList<HiloMiembroRefDto>> Handle(GetReunionesEnlazablesQuery q, CancellationToken ct)
    {
        var r = await ctx.Reuniones.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == q.ReunionId, ct)
            ?? throw new NotFoundException(nameof(Reunion), q.ReunionId);

        var baseq = ctx.Reuniones.AsNoTracking().Where(x => x.Id != r.Id);

        // Excluir las que ya pertenecen al mismo hilo.
        if (r.HiloId is not null)
            baseq = baseq.Where(x => x.HiloId != r.HiloId || x.HiloId == null);

        var texto = q.Q?.Trim();
        if (!string.IsNullOrWhiteSpace(texto))
            baseq = baseq.Where(x =>
                x.Titulo.Contains(texto) ||
                (x.Institucion != null && x.Institucion.Contains(texto)));

        return await baseq
            .OrderByDescending(x => x.Fecha).ThenByDescending(x => x.Id)
            .Take(25)
            .Select(x => new HiloMiembroRefDto(x.Id, x.Titulo, x.Fecha, x.Institucion))
            .ToListAsync(ct);
    }
}
