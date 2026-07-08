using Diger.TramitesEstado.Application.Tickets.Common;

namespace Diger.TramitesEstado.Application.Tickets.Queries.GetTickets;

public sealed record GetTicketsQuery(
    EstadoTicket? Estado = null,
    PrioridadTicket? Prioridad = null,
    int? InstitucionId = null,
    int? AsignadoAId = null,
    string? Q = null,
    int? Page = null,
    int? Size = null,
    IReadOnlyList<int>? TemaIds = null,   // "Sus temas": limita a estos temas
    bool SoloVencidos = false) : IRequest<PagedResult<TicketListItemDto>>;

public sealed class GetTicketsQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetTicketsQuery, PagedResult<TicketListItemDto>>
{
    public async Task<PagedResult<TicketListItemDto>> Handle(GetTicketsQuery query, CancellationToken ct)
    {
        var (q, page, size) = Paginacion.Normalizar(query.Q, query.Page, query.Size);

        var baseq = ctx.Tickets
            .AsNoTracking()
            .Where(t => query.Estado == null || t.Estado == query.Estado)
            .Where(t => query.Prioridad == null || t.Prioridad == query.Prioridad)
            .Where(t => query.InstitucionId == null || t.InstitucionId == query.InstitucionId)
            .Where(t => query.AsignadoAId == null || t.AsignadoAId == query.AsignadoAId);

        if (q is not null)
            baseq = baseq.Where(t =>
                t.Numero.Contains(q) || t.Titulo.Contains(q) ||
                (t.Institucion != null && t.Institucion.Contains(q)));

        // "Sus temas": solo los temas que atiende el especialista.
        // Lista no nula (aunque vacía) = filtro activo; vacía → sin resultados.
        if (query.TemaIds is not null)
            baseq = baseq.Where(t => t.TemaId != null && query.TemaIds.Contains(t.TemaId.Value));

        // "Solo vencidos": SLA superado en tickets abiertos (se calcula en SQL con DATEADD).
        if (query.SoloVencidos)
        {
            var ahora = DateTime.UtcNow;
            baseq = baseq.Where(t =>
                (t.Estado == EstadoTicket.Abierto || t.Estado == EstadoTicket.EnProgreso) &&
                t.TemaRef != null && t.TemaRef.HorasResolucion > 0 &&
                t.CreatedAt.AddHours(t.TemaRef.HorasResolucion) < ahora);
        }

        var total = await baseq.CountAsync(ct);
        var raw = await baseq
            // Abiertos primero, luego por prioridad (Crítica→Baja) y más recientes
            .OrderBy(t => t.Estado == EstadoTicket.Cerrado || t.Estado == EstadoTicket.Resuelto)
            .ThenByDescending(t => t.Prioridad)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(t => new
            {
                t.Id, t.Numero, t.Titulo, t.Estado, t.Prioridad,
                Tema = t.TemaRef != null ? t.TemaRef.Nombre : null,
                t.TemaOtro,
                Horas = t.TemaRef != null ? (int?)t.TemaRef.HorasResolucion : null,
                t.Institucion, t.AsignadoA, t.CreatedAt, NumComentarios = t.Comentarios.Count
            })
            .ToListAsync(ct);

        // El indicador de vencido para mostrar se calcula en memoria (hora actual).
        var items = raw.Select(t => new TicketListItemDto(
                t.Id, t.Numero, t.Titulo, t.Estado, t.Prioridad, t.Tema, t.TemaOtro, t.Horas,
                TicketSla.Vencido(t.Estado, t.Horas, t.CreatedAt),
                t.Institucion, t.AsignadoA, t.CreatedAt, t.NumComentarios))
            .ToList();

        return new PagedResult<TicketListItemDto>(items, total, page, size);
    }
}
