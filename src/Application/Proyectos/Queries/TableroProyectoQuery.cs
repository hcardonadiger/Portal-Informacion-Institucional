using Diger.TramitesEstado.Application.Proyectos.Common;

namespace Diger.TramitesEstado.Application.Proyectos.Queries;

/// <summary>Una fila de la carga por persona.</summary>
public sealed record CargaResponsableDto(
    string Responsable,
    int    Total,
    int    Terminadas,
    int    Vencidas,
    int    Bloqueadas)
{
    public int Abiertas => Total - Terminadas;
    public int PctTerminado => Total == 0 ? 0 : (int)Math.Round(Terminadas * 100.0 / Total);
}

/// <summary>Un mes de la serie de ritmo.</summary>
public sealed record MesRitmoDto(string Etiqueta, int Reportes, int PuntosGanados);

/// <summary>Una actividad que pide atención, con el motivo por el que aparece.</summary>
public sealed record ActividadAtencionProyectoDto(
    string    Entregable,
    string    Nombre,
    DateOnly? FechaFinPlan,
    int       AvancePct,
    string?   Responsable,
    string    Motivo,
    IReadOnlyList<string> Espera);

/// <summary>
/// Los indicadores de UN proyecto.
/// </summary>
/// <param name="ConFechas">Actividades que tienen inicio y fin planificados. <b>Es el número que
/// hay que leer primero.</b> Todo lo que compara contra el plan —el avance esperado, las vencidas,
/// la proyección— se calcula solo sobre estas; si son pocas, los indicadores de cronograma no
/// significan nada y el tablero lo dice en vez de dar un falso verde.</param>
public sealed record TableroProyectoDto(
    // ── Identidad ──
    int      ProyectoId,
    string   Codigo,
    string   Nombre,
    EstadoProyecto Estado,
    string?  Responsable,
    DateOnly? FechaInicioPlan,
    DateOnly? FechaFinPlan,

    // ── Cumplimiento ──
    int AvanceReal,
    int AvanceFisico,
    int AvanceEsperado,
    int TotalActividades,
    int ConFechas,

    // ── Cronograma ──
    int ActTerminadas,
    int ActEnProceso,
    int ActPendientes,
    int ActVencidas,
    int ActProximas,
    int ActBloqueadas,
    int EntTotal,
    int EntCumplidos,
    int EntVencidos,

    // ── Ritmo ──
    int?      DiasSinReportar,
    int       TotalReportes,
    IReadOnlyList<MesRitmoDto> Ritmo,
    decimal   PuntosPorMes,
    DateOnly? CierreProyectado,

    // ── Equipo ──
    IReadOnlyList<CargaResponsableDto> Carga,
    int ActSinResponsable,

    // ── Riesgos ──
    int RiesgosAbiertos,
    int RiesgosAltos,
    int RiesgosRevisionVencida,
    string? BloqueoVigente,

    // ── Calidad del registro ──
    int ActConResponsable,
    int EntSinDesglosar,

    IReadOnlyList<ActividadAtencionProyectoDto> Atencion)
{
    /// <summary>Puntos entre lo que debería estar hecho y lo que está. Negativa: va por detrás.</summary>
    public int Desviacion => AvanceReal - AvanceEsperado;

    /// <summary>Cobertura del cronograma. Debajo de la mitad, los indicadores contra plan son
    /// anecdóticos y la pantalla lo advierte.</summary>
    public int PctConFechas => TotalActividades == 0 ? 0
        : (int)Math.Round(ConFechas * 100.0 / TotalActividades);

    public int PctConResponsable => TotalActividades == 0 ? 0
        : (int)Math.Round(ActConResponsable * 100.0 / TotalActividades);

    /// <summary>Se puede leer el bloque de cumplimiento contra plan.</summary>
    public bool HayPlan => ConFechas > 0;

    /// <summary>Menos de la mitad del cronograma tiene fechas: los números salen, pero de una
    /// muestra que no representa al proyecto.</summary>
    public bool PlanIncompleto => TotalActividades > 0 && PctConFechas < 50;

    public bool EstaAtrasado =>
        FechaFinPlan is { } f && f < DateOnly.FromDateTime(DateTime.UtcNow)
        && Estado is EstadoProyecto.Planificado or EstadoProyecto.EnEjecucion or EstadoProyecto.Suspendido;

    public int? DiasAlCierre => FechaFinPlan is { } f
        ? f.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber
        : null;
}

/// <summary>
/// El tablero de un proyecto.
///
/// <para><b>Se calcula sobre el árbol completo traído una vez</b>, no con una consulta por
/// indicador: son decenas de actividades y la aritmética —interpolar el avance esperado, agrupar
/// por responsable, proyectar el cierre— no se traduce a SQL sin volverla ilegible.</para>
///
/// <para>El alcance lo pone la consulta del proyecto: pedir el tablero de uno ajeno devuelve null,
/// igual que pedir su ficha.</para>
/// </summary>
public sealed record GetTableroProyectoQuery(int ProyectoId) : IRequest<TableroProyectoDto?>;

public sealed class GetTableroProyectoQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetTableroProyectoQuery, TableroProyectoDto?>
{
    /// <summary>Ventana de «lo que viene»: dos semanas es lo que cabe en una reunión de seguimiento
    /// quincenal, que es el ritmo al que se mira esto.</summary>
    private const int DiasProximos = 15;

    public async Task<TableroProyectoDto?> Handle(GetTableroProyectoQuery q, CancellationToken ct)
    {
        var p = await ctx.Proyectos.AsNoTracking()
            .Include(x => x.Entregables).ThenInclude(e => e.Actividades)
                                        .ThenInclude(a => a.Predecesoras)
            .FirstOrDefaultAsync(x => x.Id == q.ProyectoId, ct);

        if (p is null) return null;

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var actividades = p.Entregables.SelectMany(e => e.Actividades.Select(a => (Ent: e, Act: a)))
                                       .Where(x => x.Act.Estado != EstadoActividad.Cancelada)
                                       .ToList();

        // ── Cumplimiento ──────────────────────────────────────────────────
        // El avance esperado interpola cada actividad sobre su ventana: 0 antes de empezar, 100
        // pasada la fecha de fin, y proporcional en medio. Solo cuentan las que tienen las dos
        // fechas — comparar contra un plan que no existe daría un número inventado.
        var conFechas = actividades
            .Where(x => x.Act.FechaInicioPlan is not null && x.Act.FechaFinPlan is not null)
            .ToList();

        var esperado = conFechas.Count == 0 ? 0 : (int)Math.Round(conFechas.Average(x =>
        {
            var ini = x.Act.FechaInicioPlan!.Value;
            var fin = x.Act.FechaFinPlan!.Value;
            if (hoy <= ini) return 0d;
            if (hoy >= fin) return 100d;
            var total = fin.DayNumber - ini.DayNumber;
            return total <= 0 ? 100d : (hoy.DayNumber - ini.DayNumber) * 100d / total;
        }));

        var fisico = p.Entregables.Count == 0 ? 0
            : (int)Math.Round(p.Entregables.Count(e => e.Estado == EstadoEntregable.Completado)
                              * 100.0 / p.Entregables.Count);

        // ── Cronograma ────────────────────────────────────────────────────
        bool Abierta(ActividadProyecto a) =>
            a.Estado is EstadoActividad.Pendiente or EstadoActividad.EnProceso;

        var terminadas = actividades.Count(x => x.Act.Estado == EstadoActividad.Completada);
        var enProceso  = actividades.Count(x => x.Act.Estado == EstadoActividad.EnProceso);
        var pendientes = actividades.Count(x => x.Act.Estado == EstadoActividad.Pendiente);

        var vencidas = actividades
            .Where(x => Abierta(x.Act) && x.Act.FechaFinPlan is { } f && f < hoy).ToList();

        var proximas = actividades
            .Count(x => Abierta(x.Act) && x.Act.FechaFinPlan is { } f
                     && f >= hoy && f.DayNumber - hoy.DayNumber <= DiasProximos);

        // Bloqueada: abierta y con alguna predecesora que todavía no termina. Es atraso que aún no
        // se ve en ninguna fecha —puede tener su ventana entera por delante y no poder arrancar—.
        var porId = actividades.ToDictionary(x => x.Act.Id, x => x.Act);
        List<string> Espera(ActividadProyecto a) => a.PredecesoraIds
            .Where(porId.ContainsKey)
            .Where(id => porId[id].Estado != EstadoActividad.Completada)
            .Select(id => porId[id].Nombre)
            .ToList();

        var bloqueadas = actividades.Where(x => Abierta(x.Act) && Espera(x.Act).Count > 0).ToList();

        // ── Ritmo ─────────────────────────────────────────────────────────
        var avances = await ctx.ProyectoAvances.AsNoTracking()
            .Where(a => a.ProyectoId == q.ProyectoId)
            .Join(ctx.Proyectos, a => a.ProyectoId, x => x.Id, (a, _) => a)   // aplica el alcance
            .Select(a => new { a.Fecha, a.PorcentajeReportado, a.Bloqueo })
            .ToListAsync(ct);

        var reporteFinal = avances.OrderByDescending(a => a.Fecha).FirstOrDefault();
        var ultimo = reporteFinal?.Fecha;

        // Vigente = el del último reporte. Un bloqueo que el reporte siguiente ya no menciona se da
        // por superado; contar todos los que alguna vez se escribieron da un número que solo sube.
        // Mismo criterio que el tablero del portafolio.
        var bloqueoVigente = string.IsNullOrWhiteSpace(reporteFinal?.Bloqueo) ? null : reporteFinal!.Bloqueo;
        var diasSinReportar = ultimo is { } u ? (int)(DateTime.UtcNow - u).TotalDays : (int?)null;

        // Seis meses de ventana: menos no deja ver tendencia y más satura el gráfico.
        var desde = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-5);
        var ritmo = new List<MesRitmoDto>();
        for (var m = desde; m <= DateTime.UtcNow; m = m.AddMonths(1))
        {
            var delMes = avances.Where(a => a.Fecha.Year == m.Year && a.Fecha.Month == m.Month).ToList();
            ritmo.Add(new MesRitmoDto(
                CronogramaProyecto.Cultura.MesCorto(m.Month),
                delMes.Count,
                delMes.Sum(a => a.PorcentajeReportado ?? 0)));
        }

        // Proyección: puntos de avance ganados por mes en los últimos tres, y con eso los meses
        // que faltarían para llegar a 100. Es una recta sobre el pasado reciente, no una promesa;
        // sin ritmo o ya cerrado, no se proyecta nada en vez de dividir entre cero.
        var ultimos = ritmo.TakeLast(3).ToList();
        var puntosPorMes = ultimos.Count == 0 ? 0m
            : Math.Round((decimal)ultimos.Sum(r => r.PuntosGanados) / ultimos.Count, 1);

        DateOnly? proyectado = null;
        if (puntosPorMes > 0 && p.AvancePct < 100)
        {
            // Los puntos reportados no equivalen a puntos de avance del proyecto: se reparten entre
            // las actividades. Se normaliza por el número de actividades vigentes.
            var porMesProyecto = actividades.Count == 0 ? 0m : puntosPorMes / actividades.Count;
            if (porMesProyecto > 0)
            {
                var meses = (double)((100 - p.AvancePct) / porMesProyecto);
                if (meses is > 0 and < 120)   // más de diez años no es una proyección, es un síntoma
                    proyectado = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths((int)Math.Ceiling(meses)));
            }
        }

        // ── Equipo ────────────────────────────────────────────────────────
        var idsVencidas   = vencidas.Select(x => x.Act.Id).ToHashSet();
        var idsBloqueadas = bloqueadas.Select(x => x.Act.Id).ToHashSet();

        var carga = actividades
            .Where(x => !string.IsNullOrWhiteSpace(x.Act.Responsable))
            .GroupBy(x => x.Act.Responsable!)
            .Select(g => new CargaResponsableDto(
                g.Key,
                g.Count(),
                g.Count(x => x.Act.Estado == EstadoActividad.Completada),
                g.Count(x => idsVencidas.Contains(x.Act.Id)),
                g.Count(x => idsBloqueadas.Contains(x.Act.Id))))
            .OrderByDescending(c => c.Vencidas).ThenByDescending(c => c.Abiertas).ThenBy(c => c.Responsable)
            .ToList();

        // ── Riesgos ───────────────────────────────────────────────────────
        var riesgos = await ctx.ProyectoRiesgos.AsNoTracking()
            .Where(r => r.ProyectoId == q.ProyectoId)
            .Join(ctx.Proyectos, r => r.ProyectoId, x => x.Id, (r, _) => r)   // aplica el alcance
            .Select(r => new { r.Estado, r.Probabilidad, r.Impacto, r.FechaRevision })
            .ToListAsync(ct);

        var abiertos = riesgos.Where(r => r.Estado is EstadoRiesgo.Abierto or EstadoRiesgo.EnTratamiento).ToList();
        static int Sev(NivelCualitativo n) => n switch
        {
            NivelCualitativo.Alta => 3, NivelCualitativo.Media => 2, _ => 1
        };

        // ── Atención: lo que hay que mirar hoy, con su motivo ──────────────
        var atencion = vencidas
            .Select(x => new ActividadAtencionProyectoDto(
                x.Ent.Nombre, x.Act.Nombre, x.Act.FechaFinPlan, x.Act.AvancePct,
                x.Act.Responsable, "vencida", []))
            .Concat(bloqueadas
                .Where(x => !idsVencidas.Contains(x.Act.Id))
                .Select(x => new ActividadAtencionProyectoDto(
                    x.Ent.Nombre, x.Act.Nombre, x.Act.FechaFinPlan, x.Act.AvancePct,
                    x.Act.Responsable, "bloqueada", Espera(x.Act))))
            .OrderBy(a => a.FechaFinPlan ?? DateOnly.MaxValue)
            .Take(25)
            .ToList();

        return new TableroProyectoDto(
            p.Id, p.Codigo, p.Nombre, p.Estado, p.Responsable,
            p.FechaInicioPlan, p.FechaFinPlan,

            p.AvancePct, fisico, esperado, actividades.Count, conFechas.Count,

            terminadas, enProceso, pendientes, vencidas.Count, proximas, bloqueadas.Count,
            p.Entregables.Count,
            p.Entregables.Count(e => e.Estado == EstadoEntregable.Completado),
            p.Entregables.Count(e => e.FechaPlan is { } f && f < hoy
                && e.Estado is EstadoEntregable.Pendiente or EstadoEntregable.EnProceso),

            diasSinReportar, avances.Count, ritmo, puntosPorMes, proyectado,

            carga,
            actividades.Count(x => string.IsNullOrWhiteSpace(x.Act.Responsable)),

            abiertos.Count,
            abiertos.Count(r => Sev(r.Probabilidad) * Sev(r.Impacto) >= 6),
            abiertos.Count(r => r.FechaRevision is { } f && f < hoy),
            bloqueoVigente,

            actividades.Count(x => !string.IsNullOrWhiteSpace(x.Act.Responsable)),
            p.Entregables.Count(e => e.Actividades.Count == 0),

            atencion);
    }
}
