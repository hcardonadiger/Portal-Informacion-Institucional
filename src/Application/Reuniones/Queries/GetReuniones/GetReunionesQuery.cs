namespace Diger.TramitesEstado.Application.Reuniones.Queries.GetReuniones;

public sealed record ReunionListItemDto(
    int Id, string Titulo, DateOnly? Fecha, string? Institucion, string? Tipo, int NumAsistentes,
    VisibilidadReunion Visibilidad, Guid? HiloId = null, DateTime CreadaEl = default);

/// <summary>Columnas por las que se puede ordenar el listado.</summary>
public enum OrdenReuniones { Creacion, Fecha, Titulo, Institucion, Tipo, Asistentes }

public sealed record GetReunionesQuery(
    string? Q = null, int? Page = null, int? Size = null,
    string? Titulo = null, string? Institucion = null, string? Tipo = null,
    DateOnly? FechaDesde = null, DateOnly? FechaHasta = null,
    DateOnly? CreadaDesde = null, DateOnly? CreadaHasta = null,
    OrdenReuniones Ordenar = OrdenReuniones.Creacion, bool Desc = true)
    : IRequest<PagedResult<ReunionListItemDto>>;

/// <summary>Valores disponibles para los desplegables de filtro, sobre todas las reuniones visibles.</summary>
public sealed record OpcionesFiltroReuniones(IReadOnlyList<string> Instituciones, IReadOnlyList<string> Tipos);

public sealed record GetOpcionesFiltroReunionesQuery : IRequest<OpcionesFiltroReuniones>;

public sealed class GetOpcionesFiltroReunionesQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetOpcionesFiltroReunionesQuery, OpcionesFiltroReuniones>
{
    public async Task<OpcionesFiltroReuniones> Handle(GetOpcionesFiltroReunionesQuery query, CancellationToken ct)
    {
        // Los valores salen de todas las reuniones que el usuario puede ver —el filtro global
        // de alcance sigue aplicando—, no de la página en pantalla: antes los armaba JavaScript
        // con las 20 filas visibles y el desplegable sólo ofrecía lo que ya se estaba viendo.
        var instituciones = await ctx.Reuniones.AsNoTracking()
            .Where(r => r.Institucion != null && r.Institucion != "")
            .Select(r => r.Institucion!).Distinct().OrderBy(x => x).ToListAsync(ct);

        var tipos = await ctx.Reuniones.AsNoTracking()
            .Where(r => r.Tipo != null && r.Tipo != "")
            .Select(r => r.Tipo!).Distinct().OrderBy(x => x).ToListAsync(ct);

        return new OpcionesFiltroReuniones(instituciones, tipos);
    }
}

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

        // ── Filtros por columna ───────────────────────────────────────────────
        // Corren en el servidor, sobre todas las reuniones visibles: si el dato está en
        // la página 3, el filtro igual lo encuentra.
        if (Limpiar(query.Titulo) is string titulo)
            baseq = baseq.Where(r => r.Titulo.Contains(titulo));
        if (Limpiar(query.Institucion) is string institucion)
            baseq = baseq.Where(r => r.Institucion == institucion);
        if (Limpiar(query.Tipo) is string tipo)
            baseq = baseq.Where(r => r.Tipo == tipo);
        // Rango con ambos extremos incluidos. Una reunión sin fecha no puede afirmarse
        // dentro del rango, así que queda fuera en cuanto se pide un extremo.
        if (query.FechaDesde is DateOnly desde)
            baseq = baseq.Where(r => r.Fecha != null && r.Fecha >= desde);
        if (query.FechaHasta is DateOnly hasta)
            baseq = baseq.Where(r => r.Fecha != null && r.Fecha <= hasta);
        // CreatedAt lleva hora, así que el rango se compara contra instantes: el día final
        // cuenta completo (hasta el instante anterior a la medianoche siguiente), si no
        // quedaría afuera todo lo registrado después de las 00:00 de ese día.
        if (query.CreadaDesde is DateOnly creadaDesde)
        {
            var inicio = creadaDesde.ToDateTime(TimeOnly.MinValue);
            baseq = baseq.Where(r => r.CreatedAt >= inicio);
        }
        if (query.CreadaHasta is DateOnly creadaHasta)
        {
            var finExclusivo = creadaHasta.AddDays(1).ToDateTime(TimeOnly.MinValue);
            baseq = baseq.Where(r => r.CreatedAt < finExclusivo);
        }

        var total = await baseq.CountAsync(ct);
        var items = await Ordenar(baseq, query.Ordenar, query.Desc)
            .Skip((page - 1) * size).Take(size)
            .Select(r => new ReunionListItemDto(
                r.Id, r.Titulo, r.Fecha, r.Institucion, r.Tipo, r.Asistentes.Count, r.Visibilidad, r.HiloId, r.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<ReunionListItemDto>(items, total, page, size);
    }

    private static string? Limpiar(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    /// <summary>
    /// Por defecto se desglosa por última creación: lo último registrado encabeza la lista,
    /// aunque la reunión sea de una fecha anterior (actas que se cargan días o meses después).
    /// Cualquier criterio cierra con creación, fecha e Id como desempate: sin un orden total
    /// la paginación repite o pierde filas al cambiar de página cuando hay empates. Ese mismo
    /// desempate hace que dentro de una importación —todas las filas con igual CreatedAt—
    /// las reuniones sigan saliendo cronológicas.
    /// </summary>
    private static IOrderedQueryable<Reunion> Ordenar(IQueryable<Reunion> q, OrdenReuniones por, bool desc)
    {
        var orden = por switch
        {
            OrdenReuniones.Fecha       => desc ? q.OrderByDescending(r => r.Fecha)            : q.OrderBy(r => r.Fecha),
            OrdenReuniones.Titulo      => desc ? q.OrderByDescending(r => r.Titulo)           : q.OrderBy(r => r.Titulo),
            OrdenReuniones.Institucion => desc ? q.OrderByDescending(r => r.Institucion)      : q.OrderBy(r => r.Institucion),
            OrdenReuniones.Tipo        => desc ? q.OrderByDescending(r => r.Tipo)             : q.OrderBy(r => r.Tipo),
            OrdenReuniones.Asistentes  => desc ? q.OrderByDescending(r => r.Asistentes.Count) : q.OrderBy(r => r.Asistentes.Count),
            _                          => desc ? q.OrderByDescending(r => r.CreatedAt)        : q.OrderBy(r => r.CreatedAt),
        };
        return orden.ThenByDescending(r => r.CreatedAt).ThenByDescending(r => r.Fecha).ThenByDescending(r => r.Id);
    }
}
