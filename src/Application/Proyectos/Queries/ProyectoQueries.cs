using Diger.TramitesEstado.Application.Proyectos.Common;

namespace Diger.TramitesEstado.Application.Proyectos.Queries;

// ── Listado ───────────────────────────────────────────────────────────────
/// <summary>Señales del portafolio por las que se puede filtrar. Son las mismas que cuenta el
/// tablero, para que sus indicadores lleven al listado en vez de quedar en un número suelto.</summary>
public enum SenalProyecto
{
    Atrasado,
    SinLineaBase,
    SinReportar,
    SinResponsable,
    Divergente
}

public sealed record GetProyectosQuery(
    EstadoProyecto?    Estado        = null,
    Guid?              ResponsableId = null,
    int?               Anio          = null,
    string?            Q             = null,
    PrioridadProyecto? Prioridad     = null,
    string?            AreaId        = null,
    string?            UnidadId      = null,
    SenalProyecto?     Senal         = null) : IRequest<IReadOnlyList<ProyectoListItemDto>>;

public sealed class GetProyectosQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetProyectosQuery, IReadOnlyList<ProyectoListItemDto>>
{
    public async Task<IReadOnlyList<ProyectoListItemDto>> Handle(GetProyectosQuery query, CancellationToken ct)
    {
        var q = ctx.Proyectos.AsNoTracking();

        if (query.Estado is { } estado)   q = q.Where(p => p.Estado == estado);
        if (query.ResponsableId is { } r) q = q.Where(p => p.ResponsableId == r);
        if (query.Prioridad is { } prio)  q = q.Where(p => p.Prioridad == prio);

        if (!string.IsNullOrWhiteSpace(query.AreaId))   q = q.Where(p => p.AreaId == query.AreaId);
        if (!string.IsNullOrWhiteSpace(query.UnidadId)) q = q.Where(p => p.UnidadId == query.UnidadId);

        // El año del proyecto es el de su arranque planificado; si no hay plan, el de creación.
        if (query.Anio is { } anio)
            q = q.Where(p => p.FechaInicioPlan.HasValue
                ? p.FechaInicioPlan.Value.Year == anio
                : p.CreatedAt.Year == anio);

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var s = query.Q.Trim().ToLower();
            q = q.Where(p => p.Nombre.ToLower().Contains(s)
                          || p.Codigo.ToLower().Contains(s)
                          || (p.Objetivo != null && p.Objetivo.ToLower().Contains(s)));
        }

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        // Los conteos de hitos y la fecha del último avance salen en la misma consulta: el listado
        // pinta el semáforo con ellos y no queremos una consulta por fila.
        var filas = await q
            .OrderBy(p => p.Estado == EstadoProyecto.Cerrado || p.Estado == EstadoProyecto.Cancelado)
            .ThenBy(p => p.Prioridad)
            .ThenBy(p => p.FechaFinPlan ?? DateOnly.MaxValue)
            .Select(p => new ProyectoListItemDto(
                p.Id,
                p.Codigo,
                p.Nombre,
                p.Responsable,
                p.Prioridad,
                p.Estado,
                p.FechaInicioPlan,
                p.FechaFinPlan,
                p.FechaFinReal,
                p.AvancePct,
                p.Hitos.Count,
                p.Hitos.Count(h => h.Estado == EstadoHito.Completado),
                p.Hitos.Count(h => h.FechaPlan.HasValue && h.FechaPlan < hoy
                                   && (h.Estado == EstadoHito.Pendiente || h.Estado == EstadoHito.EnProceso)),
                ctx.ProyectoAvances.Where(a => a.ProyectoId == p.Id)
                                   .Max(a => (DateTime?)a.Fecha)
            ))
            .ToListAsync(ct);

        // El filtro por señal se aplica en memoria porque las señales son propiedades calculadas
        // del DTO —dependen de conteos de hitos y de la fecha del último reporte— y SQL no las
        // conoce. El portafolio es de decenas de proyectos; si algún día son miles, hay que
        // bajarlas a la consulta.
        return query.Senal switch
        {
            SenalProyecto.Atrasado       => filas.Where(p => p.EstaAtrasado).ToList(),
            SenalProyecto.SinLineaBase   => filas.Where(p => p.SinLineaBase).ToList(),
            SenalProyecto.SinReportar    => filas.Where(p => p.SinReportar).ToList(),
            SenalProyecto.SinResponsable => filas.Where(p => string.IsNullOrWhiteSpace(p.Responsable)).ToList(),
            SenalProyecto.Divergente     => filas.Where(p => p.Divergente).ToList(),
            _                            => filas
        };
    }
}

// ── Detalle ───────────────────────────────────────────────────────────────
public sealed record GetProyectoQuery(int Id) : IRequest<ProyectoDetailDto?>;

public sealed class GetProyectoQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetProyectoQuery, ProyectoDetailDto?>
{
    public async Task<ProyectoDetailDto?> Handle(GetProyectoQuery query, CancellationToken ct)
    {
        var p = await ctx.Proyectos.AsNoTracking()
            .Include(x => x.Hitos)
            .FirstOrDefaultAsync(x => x.Id == query.Id, ct);

        if (p is null) return null;

        // Los estados a los que puede pasar los decide el dominio, no la vista: así el <select>
        // del editor nunca ofrece una transición que el comando va a rechazar.
        var posibles = Enum.GetValues<EstadoProyecto>().Where(p.PuedePasarA).ToList();

        return new ProyectoDetailDto(
            p.Id, p.Codigo, p.Nombre, p.Objetivo, p.InstitucionId, p.AreaId, p.UnidadId,
            p.ResponsableId, p.Responsable, p.Prioridad, p.Estado,
            p.FechaInicioPlan, p.FechaFinPlan, p.FechaInicioReal, p.FechaFinReal,
            p.AvancePct, p.CreatedAt, p.CreatedBy,
            p.Hitos.OrderBy(h => h.Orden)
                   .Select(h => new HitoProyectoDto(
                       h.Id, h.Orden, h.Nombre, h.Descripcion,
                       h.FechaPlan, h.FechaReal, h.Estado, h.ResponsableId, h.Responsable))
                   .ToList(),
            posibles);
    }
}

// ── Bitácora de ejecución ─────────────────────────────────────────────────
public sealed record GetAvancesProyectoQuery(int ProyectoId) : IRequest<IReadOnlyList<AvanceProyectoDto>>;

public sealed class GetAvancesProyectoQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetAvancesProyectoQuery, IReadOnlyList<AvanceProyectoDto>>
{
    public async Task<IReadOnlyList<AvanceProyectoDto>> Handle(GetAvancesProyectoQuery query, CancellationToken ct) =>
        await ctx.ProyectoAvances.AsNoTracking()
            .Where(a => a.ProyectoId == query.ProyectoId)
            .Join(ctx.Proyectos, a => a.ProyectoId, p => p.Id, (a, _) => a)   // aplica el alcance
            .OrderByDescending(a => a.Fecha)
            .Select(a => new AvanceProyectoDto(
                a.Id,
                a.HitoId,
                ctx.ProyectoHitos.Where(h => h.Id == a.HitoId).Select(h => h.Nombre).FirstOrDefault(),
                a.Fecha,
                a.Autor,
                a.Descripcion,
                a.PorcentajeReportado,
                a.Bloqueo,
                a.ArchivoNombre,
                a.ArchivoTamano,
                a.EditadoEn,
                a.EditadoPor))
            .ToListAsync(ct);
}

// ── Riesgos ───────────────────────────────────────────────────────────────
public sealed record GetRiesgosProyectoQuery(int ProyectoId) : IRequest<IReadOnlyList<RiesgoProyectoDto>>;

public sealed class GetRiesgosProyectoQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetRiesgosProyectoQuery, IReadOnlyList<RiesgoProyectoDto>>
{
    public async Task<IReadOnlyList<RiesgoProyectoDto>> Handle(GetRiesgosProyectoQuery query, CancellationToken ct)
    {
        var riesgos = await ctx.ProyectoRiesgos.AsNoTracking()
            .Where(r => r.ProyectoId == query.ProyectoId)
            .Join(ctx.Proyectos, r => r.ProyectoId, p => p.Id, (r, _) => r)   // aplica el alcance
            .Select(r => new RiesgoProyectoDto(
                r.Id, r.Descripcion, r.Categoria, r.Probabilidad, r.Impacto, r.Estrategia, r.Estado,
                r.Mitigacion, r.ResponsableId, r.Responsable,
                r.FechaDeteccion, r.FechaRevision, r.FechaCierre, r.RegistradoPor))
            .ToListAsync(ct);

        // El orden se arma en memoria: Severidad es calculada y SQL no la conoce. Con los volúmenes
        // de un registro de riesgos por proyecto —decenas, no miles— no compensa persistirla.
        return riesgos
            .OrderBy(r => r.Estado == EstadoRiesgo.Cerrado)
            .ThenByDescending(r => r.Severidad)
            .ThenBy(r => r.FechaRevision ?? DateOnly.MaxValue)
            .ToList();
    }
}

// ── Interesados ───────────────────────────────────────────────────────────
public sealed record GetInteresadosProyectoQuery(int ProyectoId) : IRequest<IReadOnlyList<InteresadoProyectoDto>>;

public sealed class GetInteresadosProyectoQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetInteresadosProyectoQuery, IReadOnlyList<InteresadoProyectoDto>>
{
    public async Task<IReadOnlyList<InteresadoProyectoDto>> Handle(GetInteresadosProyectoQuery query, CancellationToken ct) =>
        await ctx.ProyectoInteresados.AsNoTracking()
            .Where(i => i.ProyectoId == query.ProyectoId)
            .Join(ctx.Proyectos, i => i.ProyectoId, p => p.Id, (i, _) => i)   // aplica el alcance
            .OrderByDescending(i => i.Influencia)
            .ThenBy(i => i.Rol)
            .ThenBy(i => i.Nombre)
            .Select(i => new InteresadoProyectoDto(
                i.Id, i.UsuarioId, i.Nombre, i.Institucion, i.Cargo, i.Correo, i.Rol, i.Influencia, i.Notas))
            .ToListAsync(ct);
}

// ── Auditoría del proyecto ────────────────────────────────────────────────
public sealed record GetBitacoraProyectoQuery(int ProyectoId) : IRequest<IReadOnlyList<BitacoraProyectoDto>>;

public sealed class GetBitacoraProyectoQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetBitacoraProyectoQuery, IReadOnlyList<BitacoraProyectoDto>>
{
    public async Task<IReadOnlyList<BitacoraProyectoDto>> Handle(GetBitacoraProyectoQuery query, CancellationToken ct) =>
        await ctx.BitacorasProyecto.AsNoTracking()
            .Where(b => b.ProyectoId == query.ProyectoId)
            .Join(ctx.Proyectos, b => b.ProyectoId, p => p.Id, (b, _) => b)   // aplica el alcance
            .OrderByDescending(b => b.Fecha).ThenByDescending(b => b.Id)
            .Select(b => new BitacoraProyectoDto(b.Tipo, b.Detalle, b.Actor, b.Fecha))
            .ToListAsync(ct);
}

// ── Catálogo de alcance para el editor ────────────────────────────────────
/// <summary>Áreas y unidades de la institución activa, para los selectores de la ficha.
/// La unidad viaja con su <c>AreaId</c> para que la vista pueda encadenar los dos combos.</summary>
public sealed record GetAlcanceOpcionesQuery : IRequest<AlcanceOpcionesDto>;

public sealed class GetAlcanceOpcionesQueryHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<GetAlcanceOpcionesQuery, AlcanceOpcionesDto>
{
    public async Task<AlcanceOpcionesDto> Handle(GetAlcanceOpcionesQuery query, CancellationToken ct)
    {
        var inst = currentUser.ActiveInstitucionId;

        var areas = await ctx.Areas.AsNoTracking()
            .Where(a => a.Activo && a.InstitucionId == inst)
            .OrderBy(a => a.Nombre)
            .Select(a => new OpcionAlcanceDto(a.Id, a.Nombre, null))
            .ToListAsync(ct);

        var ids = areas.Select(a => a.Id).ToList();
        var unidades = await ctx.Unidades.AsNoTracking()
            .Where(u => u.Activo && ids.Contains(u.AreaId))
            .OrderBy(u => u.Nombre)
            .Select(u => new OpcionAlcanceDto(u.Id, u.Nombre, u.AreaId))
            .ToListAsync(ct);

        return new AlcanceOpcionesDto(areas, unidades);
    }
}

// ── Evidencia (solo para el handler de descarga autenticado) ───────────────
public sealed record GetEvidenciaAvanceQuery(int AvanceId) : IRequest<EvidenciaAvanceDto?>;

public sealed class GetEvidenciaAvanceQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetEvidenciaAvanceQuery, EvidenciaAvanceDto?>
{
    /// <summary>
    /// El <c>join</c> contra <c>Proyectos</c> no es decorativo: es lo que aplica el filtro de
    /// alcance. Este handler se invoca con un <c>avanceId</c> que llega por la URL, y
    /// <c>ProyectoAvances</c> no lleva filtro propio, así que sin el join alguien podría bajar la
    /// evidencia de un proyecto que no puede ver probando números. Con él, un proyecto fuera de
    /// alcance no devuelve fila y el handler responde NotFound.
    /// </summary>
    public async Task<EvidenciaAvanceDto?> Handle(GetEvidenciaAvanceQuery query, CancellationToken ct) =>
        await ctx.ProyectoAvances.AsNoTracking()
            .Where(a => a.Id == query.AvanceId
                        && a.ArchivoNombre != null && a.ArchivoUrl != null)
            .Join(ctx.Proyectos, a => a.ProyectoId, p => p.Id, (a, _) => a)
            .Select(a => new EvidenciaAvanceDto(a.ProyectoId, a.ArchivoNombre!, a.ArchivoUrl!))
            .FirstOrDefaultAsync(ct);
}
