using Diger.TramitesEstado.Application.Tickets.Common;

namespace Diger.TramitesEstado.Application.Tickets.Queries.GetTickets;

public sealed record GetTicketsQuery(
    EstadoTicket? Estado = null,
    PrioridadTicket? Prioridad = null,
    int? InstitucionId = null,
    int? AsignadoAId = null) : IRequest<IReadOnlyList<TicketListItemDto>>;

public sealed class GetTicketsQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetTicketsQuery, IReadOnlyList<TicketListItemDto>>
{
    public async Task<IReadOnlyList<TicketListItemDto>> Handle(GetTicketsQuery q, CancellationToken ct) =>
        await ctx.Tickets
            .AsNoTracking()
            .Where(t => q.Estado == null || t.Estado == q.Estado)
            .Where(t => q.Prioridad == null || t.Prioridad == q.Prioridad)
            .Where(t => q.InstitucionId == null || t.InstitucionId == q.InstitucionId)
            .Where(t => q.AsignadoAId == null || t.AsignadoAId == q.AsignadoAId)
            // Abiertos primero, luego por prioridad (Crítica→Baja) y más recientes
            .OrderBy(t => t.Estado == EstadoTicket.Cerrado || t.Estado == EstadoTicket.Resuelto)
            .ThenByDescending(t => t.Prioridad)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new TicketListItemDto(
                t.Id, t.Numero, t.Titulo, t.Estado, t.Prioridad, t.Categoria,
                t.Institucion, t.AsignadoA, t.CreatedAt, t.Comentarios.Count))
            .ToListAsync(ct);
}
