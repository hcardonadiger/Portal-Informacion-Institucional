namespace Diger.TramitesEstado.Application.Expedientes.Queries.GetBitacoraExpediente;

public sealed record BitacoraEntryDto(int Id, TipoEventoBitacora Tipo, string Detalle, string Actor, DateTime Fecha);

/// <summary>Bitácora de auditoría de un expediente (cambios de estado, modificaciones en bloque
/// de hijos, avance metodológico, validaciones), de la más reciente a la más antigua.</summary>
public sealed record GetBitacoraExpedienteQuery(int ExpedienteId) : IRequest<IReadOnlyList<BitacoraEntryDto>>;

public sealed class GetBitacoraExpedienteQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetBitacoraExpedienteQuery, IReadOnlyList<BitacoraEntryDto>>
{
    public async Task<IReadOnlyList<BitacoraEntryDto>> Handle(GetBitacoraExpedienteQuery q, CancellationToken ct)
    {
        // La tabla de bitácora no lleva filtro global propio: el alcance se comprueba
        // contra ctx.Expedientes, que sí está filtrado. Sin esto, cualquiera podría
        // leer la bitácora de un expediente ajeno pasando su Id.
        var visible = await ctx.Expedientes
            .AsNoTracking()
            .AnyAsync(e => e.Id == q.ExpedienteId, ct);
        if (!visible) return [];

        return await ctx.BitacorasExpediente
            .AsNoTracking()
            .Where(b => b.ExpedienteId == q.ExpedienteId)
            .OrderByDescending(b => b.Fecha)
            .Select(b => new BitacoraEntryDto(b.Id, b.Tipo, b.Detalle, b.Actor, b.Fecha))
            .ToListAsync(ct);
    }
}
