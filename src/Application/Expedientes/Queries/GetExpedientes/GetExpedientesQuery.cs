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
/// <param name="Institucion">Sigla de la institución (InstitucionId), no el nombre largo.</param>
/// <param name="Analista">Encargado del expediente. Coincidencia exacta: viene de un selector,
/// no de un campo libre, así que un Contains solo traería falsos positivos entre homónimos.</param>
/// <param name="Estado">Fase del expediente.</param>
/// <param name="Accion">Acción de racionalización. Vive en los TRÁMITES, no en el expediente, así
/// que filtra los expedientes que tengan al menos un trámite con esa acción. Un expediente puede
/// aparecer bajo dos acciones distintas, y es correcto: tiene trámites de las dos.</param>
public sealed record GetExpedientesQuery(
    string? Q = null, int? Page = null, int? Size = null, bool Todos = false, bool? Legado = null,
    string? Institucion = null, string? Analista = null,
    EstadoExpediente? Estado = null, string? Accion = null)
    : IRequest<PagedResult<ExpedienteListItemDto>>;

/// <summary>Las opciones de los selectores de la lista de expedientes.</summary>
/// <remarks>
/// Va aparte de <see cref="GetExpedientesQuery"/> a propósito: las opciones son el universo
/// completo y no pueden salir de la página que se está viendo. Si se derivaran de las filas
/// cargadas, al filtrar por una institución desaparecerían del selector todas las demás y no
/// habría forma de volver.
///
/// Las tres listas respetan el ámbito institucional por sí solas: salen de la misma tabla, a la
/// que el DbContext ya le aplica el filtro global de alcance.
/// </remarks>
public sealed record OpcionesExpedientesDto(
    IReadOnlyList<string> Instituciones,
    IReadOnlyList<string> Encargados,
    IReadOnlyList<string> Acciones);

public sealed record GetOpcionesExpedientesQuery : IRequest<OpcionesExpedientesDto>;

public sealed class GetOpcionesExpedientesQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetOpcionesExpedientesQuery, OpcionesExpedientesDto>
{
    public async Task<OpcionesExpedientesDto> Handle(
        GetOpcionesExpedientesQuery _, CancellationToken ct)
    {
        var instituciones = await ctx.Expedientes.AsNoTracking()
            .Select(e => e.InstitucionId)
            .Where(s => s != null && s != "")
            .Distinct().OrderBy(s => s)
            .ToListAsync(ct);

        var encargados = await ctx.Expedientes.AsNoTracking()
            .Select(e => e.Analista)
            .Where(s => s != null && s != "")
            .Distinct().OrderBy(s => s)
            .ToListAsync(ct);

        var acciones = await ctx.Expedientes.AsNoTracking()
            .SelectMany(e => e.Tramites)
            .Select(t => t.Accion!)
            .Where(s => s != null && s != "")
            .Distinct().OrderBy(s => s)
            .ToListAsync(ct);

        return new OpcionesExpedientesDto(instituciones, encargados, acciones);
    }
}

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

        // Los cuatro de los selectores. Coincidencia exacta en los tres de texto:
        // los valores salen de un desplegable armado con los que existen, no de
        // un campo libre, así que un Contains solo traería falsos positivos
        // —"INA" dentro de "INPREMA", por ejemplo—. Para buscar por partes está Q.
        if (!string.IsNullOrWhiteSpace(query.Institucion))
            baseq = baseq.Where(e => e.InstitucionId == query.Institucion);

        if (!string.IsNullOrWhiteSpace(query.Analista))
            baseq = baseq.Where(e => e.Analista == query.Analista);

        if (query.Estado is { } estado)
            baseq = baseq.Where(e => e.EstadoExpediente == estado);

        // La acción vive en los trámites: se piden los expedientes que tengan al
        // menos uno con esa acción.
        if (!string.IsNullOrWhiteSpace(query.Accion))
            baseq = baseq.Where(e => e.Tramites.Any(t => t.Accion == query.Accion));

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
