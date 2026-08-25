using Diger.TramitesEstado.Application.Siger.Importacion;
using Diger.TramitesEstado.Application.Expedientes.Duplicados;
using Diger.TramitesEstado.Application.Expedientes.Seguimiento;

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
    DateOnly?          FechaApertura,
    bool               EsLegado,
    bool               EsPosibleDuplicado,
    IReadOnlyList<string> DuplicadoDeCodigos,
    IReadOnlyList<string> TramiteNombres);

/// <param name="Legado">Filtro por corte de legado: null = todos, true = solo legados, false = solo nuevos.</param>
public sealed record GetExpedientesQuery(
    string? Q = null, int? Page = null, int? Size = null, bool Todos = false, bool? Legado = null)
    : IRequest<PagedResult<ExpedienteListItemDto>>;

public sealed class GetExpedientesQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetExpedientesQuery, PagedResult<ExpedienteListItemDto>>
{
    public async Task<PagedResult<ExpedienteListItemDto>> Handle(
        GetExpedientesQuery query, CancellationToken ct)
    {
        var (q, page, size) = Paginacion.Normalizar(query.Q, query.Page, query.Size);

        var baseq = ctx.Expedientes.AsNoTracking().SinBuckets();
        if (q is not null)
            baseq = baseq.Where(e =>
                e.Codigo.Contains(q) || e.Institucion.Contains(q) || e.Analista.Contains(q) ||
                e.Tramites.Any(t => t.NombreTramite.Contains(q)));
        if (query.Legado is { } legado)
            baseq = legado
                ? baseq.Where(e => e.FechaApertura == null || e.FechaApertura < CorteLegado.Fecha)
                : baseq.Where(e => e.FechaApertura != null && e.FechaApertura >= CorteLegado.Fecha);

        var total = await baseq.CountAsync(ct);

        var ordenada = baseq
            .OrderByDescending(e => e.FechaApertura)
            .ThenByDescending(e => e.CreatedAt);
        // Todos: lista completa (p.ej. para selectores dependientes), sin paginar.
        var paginada = query.Todos
            ? (IQueryable<Expediente>)ordenada
            : ordenada.Skip((page - 1) * size).Take(size);

        var filas = await paginada
            .Select(e => new
            {
                e.Id, e.Codigo, e.InstitucionId, e.Institucion, e.Analista,
                NumTramites = e.Tramites.Count,
                e.EstadoExpediente, e.CreatedAt, e.FechaApertura,
                TramiteNombres = e.Tramites.OrderBy(t => t.TramiteIndex).Select(t => t.NombreTramite).ToList()
            })
            .ToListAsync(ct);

        // Posibles duplicados: se comparan los expedientes de esta página contra TODOS los de
        // la misma institución (no solo los de la página), para no perder coincidencias fuera de vista.
        var institucionIds = filas.Select(f => f.InstitucionId).Distinct().ToList();
        var candidatos = await ctx.Expedientes.AsNoTracking().SinBuckets()
            .Where(e => institucionIds.Contains(e.InstitucionId))
            .Select(e => new ExpedienteDuplicadoCandidato(
                e.Id, e.InstitucionId, e.Codigo,
                e.Tramites.Select(t => t.NombreTramite).ToList()))
            .ToListAsync(ct);
        var duplicados = DuplicadosDetector.Detectar(candidatos);

        var items = filas.Select(f => new ExpedienteListItemDto(
                f.Id,
                f.Codigo,
                f.InstitucionId,
                f.Institucion,
                f.Analista,
                f.NumTramites,
                f.EstadoExpediente,
                f.CreatedAt,
                f.FechaApertura,
                f.FechaApertura == null || f.FechaApertura < CorteLegado.Fecha,
                duplicados.ContainsKey(f.Id),
                duplicados.TryGetValue(f.Id, out var codigos) ? codigos : [],
                f.TramiteNombres))
            .ToList();

        return new PagedResult<ExpedienteListItemDto>(items, total, page, size);
    }
}
