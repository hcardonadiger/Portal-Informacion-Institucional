namespace Diger.TramitesEstado.Application.Expedientes.Queries.GetNotasSeguimiento;

public sealed record NotaSeguimientoDto(int Id, string Texto, string CreadoPor, DateTime CreadoEl);

/// <summary>Bitácora completa de un expediente, de la más reciente a la más antigua.</summary>
public sealed record GetNotasSeguimientoQuery(int ExpedienteId) : IRequest<IReadOnlyList<NotaSeguimientoDto>>;

public sealed class GetNotasSeguimientoQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetNotasSeguimientoQuery, IReadOnlyList<NotaSeguimientoDto>>
{
    public async Task<IReadOnlyList<NotaSeguimientoDto>> Handle(GetNotasSeguimientoQuery q, CancellationToken ct)
    {
        // La tabla de notas no lleva filtro global propio: el alcance se comprueba
        // contra ctx.Expedientes, que sí está filtrado. Sin esto, cualquiera podría
        // leer las notas de un expediente ajeno pasando su Id.
        var visible = await ctx.Expedientes
            .AsNoTracking()
            .AnyAsync(e => e.Id == q.ExpedienteId, ct);
        if (!visible) return [];

        return await ctx.NotasSeguimiento
            .AsNoTracking()
            .Where(n => n.ExpedienteId == q.ExpedienteId)
            .OrderByDescending(n => n.CreadoEl)
            .Select(n => new NotaSeguimientoDto(n.Id, n.Texto, n.CreadoPor, n.CreadoEl))
            .ToListAsync(ct);
    }
}
