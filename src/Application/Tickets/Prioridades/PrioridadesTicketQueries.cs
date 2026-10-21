namespace Diger.TramitesEstado.Application.Tickets.Prioridades;

/// <summary>
/// Fila del catálogo de prioridades de ticket. <paramref name="Tickets"/> cuenta también los
/// borrados lógicamente: siguen apuntando a esta fila, así que pesan a la hora de decidir si se
/// puede eliminar.
/// </summary>
public sealed record PrioridadTicketDto(
    int    Id,
    string Nombre,
    int    Orden,
    ColorEtiqueta Color,
    bool   EsCritica,
    bool   Activo,
    bool   EsPredeterminada,
    int    Tickets);

// ── Catálogo completo, para la pantalla de administración ──────────────────
public sealed record GetPrioridadesTicketQuery(bool SoloActivas = false)
    : IRequest<IReadOnlyList<PrioridadTicketDto>>;

public sealed class GetPrioridadesTicketQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetPrioridadesTicketQuery, IReadOnlyList<PrioridadTicketDto>>
{
    public async Task<IReadOnlyList<PrioridadTicketDto>> Handle(
        GetPrioridadesTicketQuery q, CancellationToken ct)
    {
        return await ctx.PrioridadesTicket
            .AsNoTracking()
            .Where(p => !q.SoloActivas || p.Activo)
            .OrderBy(p => p.Orden).ThenBy(p => p.Nombre)
            .Select(p => new PrioridadTicketDto(
                p.Id, p.Nombre, p.Orden, p.Color, p.EsCritica, p.Activo, p.EsPredeterminada,
                ctx.Tickets.IgnoreQueryFilters().Count(t => t.PrioridadId == p.Id)))
            .ToListAsync(ct);
    }
}

// ── Opciones para desplegables ─────────────────────────────────────────────
/// <summary>Lo que necesita un &lt;select&gt; o una insignia, sin el conteo de uso.</summary>
public sealed record OpcionPrioridadTicketDto(int Id, string Nombre, ColorEtiqueta Color, bool EsPredeterminada);

/// <param name="IncluirId">
/// Prioridad que debe aparecer aunque esté inactiva: la que ya tiene el ticket que se está
/// editando. Sin esto, abrir un ticket viejo cuya prioridad se retiró le cambiaría el valor con
/// solo guardar.
/// </param>
public sealed record GetOpcionesPrioridadTicketQuery(int? IncluirId = null)
    : IRequest<IReadOnlyList<OpcionPrioridadTicketDto>>;

public sealed class GetOpcionesPrioridadTicketQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetOpcionesPrioridadTicketQuery, IReadOnlyList<OpcionPrioridadTicketDto>>
{
    public async Task<IReadOnlyList<OpcionPrioridadTicketDto>> Handle(
        GetOpcionesPrioridadTicketQuery q, CancellationToken ct)
    {
        return await ctx.PrioridadesTicket
            .AsNoTracking()
            .Where(p => p.Activo || (q.IncluirId != null && p.Id == q.IncluirId))
            .OrderBy(p => p.Orden).ThenBy(p => p.Nombre)
            .Select(p => new OpcionPrioridadTicketDto(p.Id, p.Nombre, p.Color, p.EsPredeterminada))
            .ToListAsync(ct);
    }
}
