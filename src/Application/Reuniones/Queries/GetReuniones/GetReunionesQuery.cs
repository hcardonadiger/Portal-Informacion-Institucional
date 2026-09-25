namespace Diger.TramitesEstado.Application.Reuniones.Queries.GetReuniones;

public sealed record ReunionListItemDto(
    int Id, string Titulo, DateOnly? Fecha, string? Institucion, string? Tipo, int NumAsistentes,
    VisibilidadReunion Visibilidad, Guid? HiloId = null);

public sealed record GetReunionesQuery(string? Q = null, int? Page = null, int? Size = null)
    : IRequest<PagedResult<ReunionListItemDto>>;

public sealed class GetReunionesQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetReunionesQuery, PagedResult<ReunionListItemDto>>
{
    public async Task<PagedResult<ReunionListItemDto>> Handle(GetReunionesQuery query, CancellationToken ct)
    {
        var (q, page, size) = Paginacion.Normalizar(query.Q, query.Page, query.Size);

        var baseq = ctx.Reuniones.AsNoTracking();
        if (q is not null)
            baseq = baseq.Where(r =>
                r.Titulo.Contains(q) ||
                (r.Institucion != null && r.Institucion.Contains(q)) ||
                (r.Tipo != null && r.Tipo.Contains(q)));

        var total = await baseq.CountAsync(ct);
        // Se desglosa por última creación: lo último registrado encabeza la lista, aunque la
        // reunión sea de una fecha anterior (actas que se cargan días o meses después).
        // Fecha e Id sólo desempatan dentro de un mismo lote —típicamente una importación,
        // donde todas las filas comparten CreatedAt— para que ahí sí salgan cronológicas.
        var items = await baseq
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Fecha).ThenByDescending(r => r.Id)
            .Skip((page - 1) * size).Take(size)
            .Select(r => new ReunionListItemDto(
                r.Id, r.Titulo, r.Fecha, r.Institucion, r.Tipo, r.Asistentes.Count, r.Visibilidad, r.HiloId))
            .ToListAsync(ct);

        return new PagedResult<ReunionListItemDto>(items, total, page, size);
    }
}
