using Diger.TramitesEstado.Application.Dashboards.Common;

namespace Diger.TramitesEstado.Application.Dashboards.Queries;

/// <summary>
/// Tablero gerencial del portafolio de proyectos internos.
///
/// <para>La pregunta que responde no es «cuánto llevamos» sino <b>a qué hay que ponerle
/// atención</b>: qué proyectos vencieron su fecha, cuáles llevan semanas sin que nadie
/// reporte, qué entregables y actividades están vencidos y qué bloqueos siguen abiertos.
/// El porcentaje de avance acompaña, no manda — desde que se calcula desde el árbol, un
/// proyecto puede tener un número creíble y aun así llevar un mes sin que nadie lo toque,
/// que es justamente lo que mide el indicador de «sin reportar».</para>
/// </summary>
public sealed record GetProyectosDashboardQuery(
    EstadoProyecto?    Estado        = null,
    Guid?              ResponsableId = null,
    PrioridadProyecto? Prioridad     = null) : IRequest<ProyectosDashboardDto>;

public sealed class GetProyectosDashboardQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetProyectosDashboardQuery, ProyectosDashboardDto>
{
    /// <summary>Días sin reporte a partir de los cuales un proyecto abierto se marca como
    /// desatendido. Mismo umbral que usa el listado del módulo, para que no digan cosas
    /// distintas sobre el mismo proyecto.</summary>
    private const int DiasSinReporte = 30;

    /// <summary>Ventana de «lo que viene» para entregables y actividades por vencer.</summary>
    private const int DiasProximos = 30;

    public async Task<ProyectosDashboardDto> Handle(GetProyectosDashboardQuery q, CancellationToken ct)
    {
        var hoy   = DateOnly.FromDateTime(DateTime.UtcNow);
        var corte = DateTime.UtcNow.AddDays(-DiasSinReporte);

        var baseQuery = ctx.Proyectos.AsNoTracking();
        if (q.Estado is { } e)        baseQuery = baseQuery.Where(p => p.Estado == e);
        if (q.ResponsableId is { } r) baseQuery = baseQuery.Where(p => p.ResponsableId == r);
        if (q.Prioridad is { } pr)    baseQuery = baseQuery.Where(p => p.Prioridad == pr);

        // Una sola pasada a la base: el resto se calcula en memoria sobre esta proyección.
        // El portafolio es de decenas de proyectos, no de miles.
        var filas = await baseQuery
            .Select(p => new
            {
                p.Id, p.Codigo, p.Nombre, p.Responsable, p.ResponsableId,
                p.Estado, p.Prioridad, p.AvancePct, p.FechaFinPlan,
                TotalEntregables       = p.Entregables.Count,
                EntregablesCompletados = p.Entregables.Count(x => x.Estado == EstadoEntregable.Completado),
                EntregablesVencidos    = p.Entregables.Count(x => x.FechaPlan.HasValue && x.FechaPlan < hoy
                                            && (x.Estado == EstadoEntregable.Pendiente || x.Estado == EstadoEntregable.EnProceso)),
                TotalActividades       = p.Entregables.Sum(x => x.Actividades.Count),
                ActividadesVencidas    = p.Entregables.Sum(x => x.Actividades.Count(a => a.FechaFinPlan.HasValue && a.FechaFinPlan < hoy
                                            && (a.Estado == EstadoActividad.Pendiente || a.Estado == EstadoActividad.EnProceso))),
                UltimoAvance     = ctx.ProyectoAvances.Where(a => a.ProyectoId == p.Id).Max(a => (DateTime?)a.Fecha),
                Reportes         = ctx.ProyectoAvances.Count(a => a.ProyectoId == p.Id)
            })
            .ToListAsync(ct);

        var abiertos = filas.Where(p => p.Estado is EstadoProyecto.Planificado
                                                 or EstadoProyecto.EnEjecucion
                                                 or EstadoProyecto.Suspendido).ToList();
        var enEjecucion = filas.Where(p => p.Estado == EstadoProyecto.EnEjecucion).ToList();

        static bool Abierto(EstadoProyecto est) =>
            est is EstadoProyecto.Planificado or EstadoProyecto.EnEjecucion or EstadoProyecto.Suspendido;

        bool Atrasado(DateOnly? fin, EstadoProyecto est) => fin.HasValue && fin < hoy && Abierto(est);

        // Un proyecto abierto sin fecha de cierre no está «a tiempo»: está sin comprometer. Se
        // cuenta aparte porque, mezclado con el resto, empuja el indicador de atrasados a cero y
        // hace leer el portafolio como si estuviera al día.
        static bool SinLineaBase(DateOnly? fin, EstadoProyecto est) => fin is null && Abierto(est);

        var semaforo = filas
            .Select(p => new ProyectoSemaforoDto(
                p.Id, p.Codigo, p.Nombre, p.Responsable, p.Estado, p.Prioridad, p.AvancePct,
                p.TotalEntregables, p.EntregablesCompletados, p.EntregablesVencidos,
                p.TotalActividades, p.ActividadesVencidas,
                p.FechaFinPlan, p.UltimoAvance,
                p.UltimoAvance is null ? null : (int)(DateTime.UtcNow - p.UltimoAvance.Value).TotalDays,
                Atrasado(p.FechaFinPlan, p.Estado),
                p.Estado == EstadoProyecto.EnEjecucion && (p.UltimoAvance is null || p.UltimoAvance < corte),
                SinLineaBase(p.FechaFinPlan, p.Estado)))
            // Primero lo que exige atención: atrasado, luego desatendido, luego prioridad.
            .OrderByDescending(p => p.Atrasado)
            .ThenByDescending(p => p.SinReportar)
            .ThenBy(p => p.Prioridad)
            .ThenBy(p => p.Nombre)
            .ToList();

        var idsFiltrados = filas.Select(f => f.Id).ToHashSet();
        var limite = hoy.AddDays(DiasProximos);

        // ── Entregables vencidos y próximos ──────────────────────────────────
        // Se trae la entidad con sus actividades, no una proyección: el avance del entregable lo
        // resuelve el dominio (AvanceCalculado) y su regla no se traduce a SQL. El cruce con el
        // proyecto se hace en memoria contra `filas`, que ya viene acotada por el alcance — un
        // Join en la consulta descartaría el Include.
        var entregables = await ctx.ProyectoEntregables.AsNoTracking()
            .Where(x => x.FechaPlan.HasValue
                        && (x.Estado == EstadoEntregable.Pendiente || x.Estado == EstadoEntregable.EnProceso)
                        && x.FechaPlan <= limite)
            .Where(x => ctx.Proyectos.Any(p => p.Id == x.ProyectoId
                        && p.Estado != EstadoProyecto.Cerrado && p.Estado != EstadoProyecto.Cancelado))
            .Include(x => x.Actividades)
            .ToListAsync(ct);

        var porProyecto = filas.ToDictionary(f => f.Id);
        var entregablesAtencion = entregables
            .Where(x => porProyecto.ContainsKey(x.ProyectoId))
            .Select(x =>
            {
                var p = porProyecto[x.ProyectoId];
                return new EntregableAtencionDto(
                    p.Id, p.Codigo, p.Nombre, x.Nombre,
                    x.Responsable ?? p.Responsable,
                    x.FechaPlan!.Value, x.Estado, x.AvanceCalculado);
            })
            .OrderBy(x => x.FechaPlan)
            .ToList();

        // ── Actividades vencidas y próximas ──────────────────────────────────
        // El nivel donde el atraso se ve primero. Se consulta aparte y no desde los entregables de
        // arriba: una actividad puede estar vencida dentro de un entregable cuya fecha todavía no
        // llega, y esa es justamente la que interesa ver.
        var actividadesAtencion = await ctx.ProyectoActividades.AsNoTracking()
            .Where(a => a.FechaFinPlan.HasValue
                        && (a.Estado == EstadoActividad.Pendiente || a.Estado == EstadoActividad.EnProceso)
                        && a.FechaFinPlan <= limite)
            .Join(ctx.ProyectoEntregables.AsNoTracking(), a => a.EntregableId, x => x.Id, (a, x) => new { a, x })
            .Join(ctx.Proyectos.AsNoTracking(), z => z.x.ProyectoId, p => p.Id, (z, p) => new { z.a, z.x, p })
            .Where(z => z.p.Estado != EstadoProyecto.Cerrado && z.p.Estado != EstadoProyecto.Cancelado)
            .Select(z => new ActividadAtencionDto(
                z.p.Id, z.p.Codigo, z.p.Nombre, z.x.Nombre, z.a.Nombre,
                z.a.Responsable ?? z.x.Responsable ?? z.p.Responsable,
                z.a.FechaInicioPlan, z.a.FechaFinPlan!.Value, z.a.AvancePct, z.a.Estado,
                z.a.CreatedAt == default ? null : z.a.CreatedAt))
            .ToListAsync(ct);

        actividadesAtencion = actividadesAtencion
            .Where(a => idsFiltrados.Contains(a.ProyectoId))
            .OrderBy(a => a.FechaFinPlan)
            .ToList();

        // ── Actividades bloqueadas por una dependencia ───────────────────────
        // No las encuentra ninguna consulta por fecha: una actividad puede tener su ventana entera
        // por delante y estar trancada igual porque otra no termina. Se resuelve en memoria — la
        // tabla de dependencias es chica y la regla («la predecesora sigue abierta») ya vive en el
        // dominio, no en SQL.
        var dependencias = await ctx.ProyectoDependencias.AsNoTracking()
            .Select(d => new { d.SucesoraId, d.PredecesoraId })
            .ToListAsync(ct);

        var bloqueadas = new List<ActividadBloqueadaDto>();
        if (dependencias.Count > 0)
        {
            var involucradas = dependencias
                .SelectMany(d => new[] { d.SucesoraId, d.PredecesoraId })
                .ToHashSet();

            // El cruce con Proyectos es lo que aplica el filtro de alcance: ProyectoActividades no
            // lo lleva, su ancla es el proyecto del que cuelga.
            var actividades = await ctx.ProyectoActividades.AsNoTracking()
                .Where(a => involucradas.Contains(a.Id))
                .Join(ctx.ProyectoEntregables.AsNoTracking(), a => a.EntregableId, x => x.Id, (a, x) => new { a, x })
                .Join(ctx.Proyectos.AsNoTracking(), z => z.x.ProyectoId, p => p.Id, (z, p) => new { z.a, z.x, p })
                .Where(z => z.p.Estado != EstadoProyecto.Cerrado && z.p.Estado != EstadoProyecto.Cancelado)
                .Select(z => new
                {
                    z.a.Id, z.a.Nombre, z.a.Estado, z.a.AvancePct, z.a.FechaInicioPlan,
                    CreadoEn    = z.a.CreatedAt == default ? (DateTime?)null : z.a.CreatedAt,
                    Entregable  = z.x.Nombre,
                    ProyectoId  = z.p.Id,
                    z.p.Codigo,
                    Proyecto    = z.p.Nombre,
                    Responsable = z.a.Responsable ?? z.x.Responsable ?? z.p.Responsable
                })
                .ToListAsync(ct);

            var porIdAct = actividades.ToDictionary(a => a.Id);

            bloqueadas = dependencias
                // Una dependencia con alguna punta fuera del alcance —o en un proyecto cerrado— no
                // se puede evaluar sin mostrar datos que el usuario no debería ver: se descarta.
                .Where(d => porIdAct.ContainsKey(d.SucesoraId) && porIdAct.ContainsKey(d.PredecesoraId))
                // Solo bloquea la predecesora que sigue abierta. La cancelada dejó de ser parte del
                // plan y la completada ya liberó a su sucesora.
                .Where(d => porIdAct[d.PredecesoraId].Estado is EstadoActividad.Pendiente
                                                             or EstadoActividad.EnProceso)
                .GroupBy(d => d.SucesoraId)
                .Select(g => new { Sucesora = porIdAct[g.Key], Espera = g.Select(d => porIdAct[d.PredecesoraId]).ToList() })
                .Where(x => x.Sucesora.Estado != EstadoActividad.Cancelada)
                .Where(x => idsFiltrados.Contains(x.Sucesora.ProyectoId))
                .Select(x => new ActividadBloqueadaDto(
                    x.Sucesora.ProyectoId, x.Sucesora.Codigo, x.Sucesora.Proyecto,
                    x.Sucesora.Entregable, x.Sucesora.Nombre, x.Sucesora.Responsable,
                    x.Sucesora.FechaInicioPlan, x.Sucesora.AvancePct, x.Sucesora.Estado,
                    x.Espera.OrderBy(e => e.Nombre).Select(e => e.Nombre).ToList(),
                    x.Sucesora.CreadoEn))
                // Primero la que se está trabajando pese al bloqueo, después la que ya debía haber
                // empezado: ese es el orden en que hay que decidir algo.
                .OrderByDescending(b => b.Arrancada)
                .ThenByDescending(b => b.DebioArrancar)
                .ThenBy(b => b.Proyecto)
                .ThenBy(b => b.Actividad)
                .ToList();
        }

        // ── Bloqueos vigentes ────────────────────────────────────────────────
        // Solo el último reporte de cada proyecto: un bloqueo que ya no se menciona en el
        // reporte siguiente se da por superado, sin obligar a nadie a cerrarlo a mano.
        var ultimos = await ctx.ProyectoAvances.AsNoTracking()
            .Where(a => a.Bloqueo != null)
            .Select(a => new { a.ProyectoId, a.Fecha, a.Autor, a.Bloqueo })
            .ToListAsync(ct);

        var fechaUltimo = filas.ToDictionary(f => f.Id, f => f.UltimoAvance);
        var bloqueos = ultimos
            .Where(a => idsFiltrados.Contains(a.ProyectoId)
                        && fechaUltimo.TryGetValue(a.ProyectoId, out var u) && u == a.Fecha)
            .Join(filas, a => a.ProyectoId, f => f.Id, (a, f) =>
                new BloqueoDto(f.Id, f.Codigo, f.Nombre, a.Bloqueo!, a.Autor, a.Fecha))
            .OrderBy(b => b.Fecha)
            .ToList();

        // ── Series y agrupaciones ────────────────────────────────────────────
        var porEstado = Enum.GetValues<EstadoProyecto>()
            .Select(est => new ConteoDto(Etiquetas.Estado(est), filas.Count(p => p.Estado == est)))
            .ToList();

        var porResponsable = filas
            .GroupBy(p => p.Responsable ?? "Sin asignar")
            .Select(g => new ConteoDto(g.Key, g.Count()))
            .OrderByDescending(c => c.Cantidad).ThenBy(c => c.Etiqueta)
            .ToList();

        var todosAvances = await ctx.ProyectoAvances.AsNoTracking()
            .Where(a => idsFiltrados.Contains(a.ProyectoId))
            .Select(a => new { a.Fecha })
            .ToListAsync(ct);

        var serie = Enumerable.Range(0, 6)
            .Select(i => DateTime.UtcNow.AddMonths(-5 + i))
            .Select(m => new SerieMensualDto(
                m.ToString("MMM yy", new System.Globalization.CultureInfo("es-HN")),
                todosAvances.Count(a => a.Fecha.Year == m.Year && a.Fecha.Month == m.Month)))
            .ToList();

        return new ProyectosDashboardDto(
            Total:            filas.Count,
            Abiertos:         abiertos.Count,
            EnEjecucion:      enEjecucion.Count,
            Cerrados:         filas.Count(p => p.Estado == EstadoProyecto.Cerrado),
            AvancePromedio:   enEjecucion.Count == 0 ? 0 : (int)Math.Round(enEjecucion.Average(p => p.AvancePct)),
            Atrasados:        semaforo.Count(p => p.Atrasado),
            SinLineaBase:     semaforo.Count(p => p.SinLineaBase),
            // Solo sobre proyectos abiertos: en uno cerrado la diferencia ya no acciona nada.
            ConDivergencia:   semaforo.Count(p => p.Divergente && p.Estado is EstadoProyecto.Planificado
                                                                          or EstadoProyecto.EnEjecucion
                                                                          or EstadoProyecto.Suspendido),
            SinReportar:      semaforo.Count(p => p.SinReportar),
            SinResponsable:   abiertos.Count(p => p.ResponsableId is null),
            EntregablesVencidos: entregablesAtencion.Count(x => x.FechaPlan < hoy),
            EntregablesProximos: entregablesAtencion.Count(x => x.FechaPlan >= hoy),
            ActividadesVencidas: actividadesAtencion.Count(a => a.FechaFinPlan < hoy),
            ActividadesProximas: actividadesAtencion.Count(a => a.FechaFinPlan >= hoy),
            SinDesglose:      semaforo.Count(p => p.SinDesglose),
            ActividadesBloqueadas: bloqueadas.Count,
            ArrancaronBloqueadas:  bloqueadas.Count(b => b.Arrancada),
            ReportesTotal:    filas.Sum(p => p.Reportes),
            PorEstado:        porEstado,
            PorResponsable:   porResponsable,
            ReportesPorMes:   serie,
            Semaforo:         semaforo,
            Entregables:      entregablesAtencion,
            Actividades:      actividadesAtencion,
            Bloqueos:         bloqueos,
            Bloqueadas:       bloqueadas);
    }
}

/// <summary>Etiquetas legibles de los enums del módulo, en un solo lugar para que el
/// tablero y el listado no se contradigan.</summary>
public static class Etiquetas
{
    public static string Estado(EstadoProyecto e) => e switch
    {
        EstadoProyecto.EnEjecucion => "En ejecución",
        _                          => e.ToString()
    };

    public static string Entregable(EstadoEntregable e) => e switch
    {
        EstadoEntregable.EnProceso => "En proceso",
        _                          => e.ToString()
    };

    public static string Actividad(EstadoActividad e) => e switch
    {
        EstadoActividad.EnProceso => "En proceso",
        _                         => e.ToString()
    };
}
