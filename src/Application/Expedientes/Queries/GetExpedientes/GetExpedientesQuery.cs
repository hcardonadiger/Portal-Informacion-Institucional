namespace Diger.TramitesEstado.Application.Expedientes.Queries.GetExpedientes;

public sealed record ExpedienteListItemDto(
    int                Id,
    string             Codigo,
    string             InstitucionId,
    string             Institucion,
    string             Analista,
    int                NumTramites,
    EstadoExpediente   Estado,
    DateTime           FechaCreacion,
    IReadOnlyList<string> TramiteNombres);

public sealed record GetExpedientesQuery(string? Q = null, int? Page = null, int? Size = null, bool Todos = false)
    : IRequest<PagedResult<ExpedienteListItemDto>>;

public sealed class GetExpedientesQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetExpedientesQuery, PagedResult<ExpedienteListItemDto>>
{
    public async Task<PagedResult<ExpedienteListItemDto>> Handle(
        GetExpedientesQuery query, CancellationToken ct)
    {
        var (q, page, size) = Paginacion.Normalizar(query.Q, query.Page, query.Size);

        var baseq = ctx.Expedientes.AsNoTracking();
        if (q is not null)
            baseq = baseq.Where(e =>
                e.Codigo.Contains(q) || e.Institucion.Contains(q) || e.Analista.Contains(q) ||
                e.Tramites.Any(t => t.NombreTramite.Contains(q)));

        var total = await baseq.CountAsync(ct);

        var ordenada = baseq.OrderByDescending(e => e.CreatedAt);
        // Todos: lista completa (p.ej. para selectores dependientes), sin paginar.
        var paginada = query.Todos
            ? (IQueryable<Expediente>)ordenada
            : ordenada.Skip((page - 1) * size).Take(size);

        var items = await paginada
            .Select(e => new ExpedienteListItemDto(
                e.Id,
                e.Codigo,
                e.InstitucionId,
                e.Institucion,
                e.Analista,
                e.Tramites.Count,
                e.EstadoExpediente,
                e.CreatedAt,
                e.Tramites.OrderBy(t => t.TramiteIndex).Select(t => t.NombreTramite).ToList()))
            .ToListAsync(ct);

        return new PagedResult<ExpedienteListItemDto>(items, total, page, size);
    }
}
