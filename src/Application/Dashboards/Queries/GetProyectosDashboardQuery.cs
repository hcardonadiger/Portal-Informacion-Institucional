using Diger.TramitesEstado.Application.Dashboards.Common;

namespace Diger.TramitesEstado.Application.Dashboards.Queries;

/// <summary>
/// Tablero gerencial del portafolio de proyectos internos.
///
/// <para>La pregunta que responde no es «cuánto llevamos» sino <b>a qué hay que ponerle
/// atención</b>: qué proyectos vencieron su fecha, cuáles llevan semanas sin que nadie
/// reporte, qué hitos están vencidos y qué bloqueos siguen abiertos. El porcentaje de
/// avance acompaña, no manda — lo declara el responsable y puede estar desactualizado,
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

    /// <summary>Ventana de «lo que viene» para los hitos por vencer.</summary>
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
                TotalHitos       = p.Hitos.Count,
                HitosCompletados = p.Hitos.Count(h => h.Estado == EstadoHito.Completado),
                HitosVencidos    = p.Hitos.Count(h => h.FechaPlan.HasValue && h.FechaPlan < hoy
                                                      && (h.Estado == EstadoHito.Pendiente || h.Estado == EstadoHito.EnProceso)),
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
                p.TotalHitos, p.HitosCompletados, p.HitosVencidos,
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

        // ── Hitos vencidos y próximos ────────────────────────────────────────
        var idsFiltrados = filas.Select(f => f.Id).ToHashSet();
        var limite = hoy.AddDays(DiasProximos);

        var hitos = await ctx.ProyectoHitos.AsNoTracking()
            .Where(h => h.FechaPlan.HasValue
                        && (h.Estado == EstadoHito.Pendiente || h.Estado == EstadoHito.EnProceso)
                        && h.FechaPlan <= limite)
            .Join(ctx.Proyectos.AsNoTracking(), h => h.ProyectoId, p => p.Id, (h, p) => new { h, p })
            .Where(x => x.p.Estado != EstadoProyecto.Cerrado && x.p.Estado != EstadoProyecto.Cancelado)
            .Select(x => new HitoAtencionDto(
                x.p.Id, x.p.Codigo, x.p.Nombre, x.h.Nombre,
                x.h.Responsable ?? x.p.Responsable,
                x.h.FechaPlan!.Value, x.h.Estado))
            .ToListAsync(ct);

        hitos = hitos.Where(h => idsFiltrados.Contains(h.ProyectoId))
                     .OrderBy(h => h.FechaPlan)
                     .ToList();

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
            HitosVencidos:    hitos.Count(h => h.FechaPlan < hoy),
            HitosProximos:    hitos.Count(h => h.FechaPlan >= hoy),
            ReportesTotal:    filas.Sum(p => p.Reportes),
            PorEstado:        porEstado,
            PorResponsable:   porResponsable,
            ReportesPorMes:   serie,
            Semaforo:         semaforo,
            Hitos:            hitos,
            Bloqueos:         bloqueos);
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

    public static string Hito(EstadoHito e) => e switch
    {
        EstadoHito.EnProceso => "En proceso",
        _                    => e.ToString()
    };

}
