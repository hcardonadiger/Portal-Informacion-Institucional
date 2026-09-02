using Diger.TramitesEstado.Domain.Enums;

namespace Diger.TramitesEstado.Application.Dashboards.Common;

/// <summary>Conteo etiquetado para una barra/segmento de gráfico.</summary>
public sealed record ConteoDto(string Etiqueta, int Cantidad);

/// <summary>Punto de una serie temporal mensual (etiqueta "MMM yy" + valor).</summary>
public sealed record SerieMensualDto(string Mes, int Cantidad);

/// <summary>Valor actual + valor del período anterior, para indicadores de tendencia ▲▼.</summary>
public sealed record TendenciaDto(int Actual, int Anterior)
{
    public int Delta => Actual - Anterior;
    public int PorcentajeCambio => Anterior == 0 ? (Actual > 0 ? 100 : 0) : (int)Math.Round((Actual - Anterior) * 100.0 / Anterior);
}

// ── Resumen ejecutivo (Inicio) ────────────────────────────────────────────
public sealed record ResumenDto(
    // Tickets
    int TicketsAbiertos, int TicketsEnProgreso, int TicketsCriticos, int TicketsTotal,
    int TicketsResueltos, int TicketsDiasPromedioResolucion,
    TendenciaDto TicketsCreados,
    IReadOnlyList<SerieMensualDto> TicketsPorMes,
    IReadOnlyList<SerieMensualDto> TicketsResueltosPorMes,
    // Expedientes
    int ExpedientesTotal, int ExpedientesCerrados, int ExpedientesEnProceso,
    // Reuniones y acuerdos
    int ReunionesTotal, int ReunionesMes,
    int AcuerdosVencidos, int AcuerdosProximos,
    int AcuerdosTotal, int AcuerdosCumplidos, int TasaCumplimiento,
    // Capacitación
    int PersonasCapacitadas, int AsistenciasCapacitacion,
    // Digitalización
    int DigTotalTramites, int DigEnOperacion, int DigEnProceso, int DigNoIniciados,
    double DigAvanceGlobal,
    IReadOnlyList<AvanceInstitucionDto> DigPorInstitucion,
    IReadOnlyList<AnalistaAvanceDto> DigPorAnalista);

public sealed record ResumenTicketDto(
    int Id, string Numero, string Titulo, EstadoTicket Estado, PrioridadTicket Prioridad, string? Institucion);

public sealed record SemaforoInstitucionDto(
    string Institucion, int Expedientes,
    double AvanceDigitalizacion, int TicketsAbiertos, int AcuerdosVencidos);

// ── Tickets ───────────────────────────────────────────────────────────────
public sealed record TicketsDashboardDto(
    int Total, int Abiertos, int CriticosAbiertos,
    int Resueltos, int DiasPromedioResolucion, int PorcentajeResueltos,
    int SlaVencidos,
    TendenciaDto TendenciaCreados,
    IReadOnlyList<ConteoDto> PorEstado,
    IReadOnlyList<ConteoDto> PorPrioridad,
    IReadOnlyList<ConteoDto> PorCategoria,
    IReadOnlyList<ConteoDto> PorTema,
    IReadOnlyList<ConteoDto> PorInstitucion,
    IReadOnlyList<SerieMensualDto> CreadosPorMes,
    IReadOnlyList<SerieMensualDto> ResueltosPorMes,
    IReadOnlyList<TicketAntiguedadDto> AbiertosAntiguos);

public sealed record TicketAntiguedadDto(
    int Id, string Numero, string Titulo, string? Institucion, int DiasAbierto, PrioridadTicket Prioridad);

// ── Expedientes ───────────────────────────────────────────────────────────
public sealed record ExpedientesDashboardDto(
    int Total, int TramitesTotal, int Cerrados,
    IReadOnlyList<ConteoDto> PorEstado,
    IReadOnlyList<ConteoDto> PorInstitucion,
    IReadOnlyList<SerieMensualDto> CreadosPorMes);

// ── Reuniones / acuerdos ──────────────────────────────────────────────────
public sealed record ReunionesDashboardDto(
    int Total, int Mes, int AsistentesTotal,
    int AcuerdosVencidos, int AcuerdosProximos, int AcuerdosSinPlazo,
    IReadOnlyList<ConteoDto> PorTipo,
    IReadOnlyList<ConteoDto> PorInstitucion,
    IReadOnlyList<SerieMensualDto> PorMes,
    IReadOnlyList<AcuerdoPendienteDto> Acuerdos,
    int AcuerdosTotal, int AcuerdosCumplidos, int TasaCumplimiento,
    IReadOnlyList<ConteoDto> PorEstadoAcuerdo,
    IReadOnlyList<PersonaCapacitadaDto> PersonasCapacitadas,
    /// <summary>Filas de asistencia registradas en reuniones de capacitación (sin DIGER).</summary>
    int AsistenciasEnCapacitaciones,
    /// <summary>Filas de asistencia registradas en todas las reuniones.</summary>
    int AsistenciasTotales);

public sealed record AcuerdoPendienteDto(
    string Compromiso, string? Responsable, DateOnly? Plazo, string ReunionTitulo, int ReunionId,
    bool Vencido, EstadoCompromiso Estado);

/// <summary>Persona única que asistió a una o más capacitaciones. Se deduplica por correo
/// (clave principal) o, a falta de éste, por nombre normalizado. Excluye al personal de DIGER
/// (facilitadores, no capacitados).</summary>
public sealed record PersonaCapacitadaDto(
    string Nombre, string? Institucion, IReadOnlyList<string> Capacitaciones)
{
    /// <summary>Cantidad de capacitaciones distintas a las que asistió.</summary>
    public int Veces => Capacitaciones.Count;
    public bool EsMultiple => Capacitaciones.Count > 1;
}

// ── Digitalización ───────────────────────────────────────────────────────
public sealed record DigitalizacionDashboardDto(
    int TotalTramites,
    int TramitesEnOperacion, int TramitesEnProceso, int TramitesNoIniciados,
    int InstitucionesActivas, double AvanceGlobalPromedio,
    IReadOnlyList<AvanceInstitucionDto> PorInstitucion,
    IReadOnlyList<AvanceEtapaDto> PorEtapaMetodologia,
    IReadOnlyList<ConteoDto> DistribucionAvance,
    IReadOnlyList<AnalistaAvanceDto> PorAnalista,
    IReadOnlyList<TramiteAvanceDto> TramitesRezagados,
    IReadOnlyList<TramiteEtapaDetalleDto> DetallePorEtapa,
    IReadOnlyList<TramiteAvanceDto> TodosLosTramites);

public sealed record AvanceInstitucionDto(
    string Institucion, int TotalTramites, int Completados, double AvancePromedio);

public sealed record AvanceEtapaDto(
    string EtapaNum, string Etiqueta, double AvancePromedio, int Completados, int Total);

public sealed record AnalistaAvanceDto(
    string Analista, int TotalTramites, int Completados, double AvancePromedio);

public sealed record TramiteAvanceDto(
    int ExpedienteId, string Institucion, string Tramite, string? Analista, double Avance);

public sealed record TramiteEtapaDetalleDto(
    int ExpedienteId, string Institucion, string Tramite, string? Analista,
    string EtapaNum, double AvanceEtapa);

// ── Helper de series mensuales ─────────────────────────────────────────────
public static class SerieMensual
{
    /// <summary>Construye una serie de los últimos 12 meses (rellena con 0 los meses sin datos).</summary>
    public static IReadOnlyList<SerieMensualDto> Ultimos12(IEnumerable<(int Anio, int Mes, int Cantidad)> datos)
    {
        var dict = datos.ToDictionary(d => (d.Anio, d.Mes), d => d.Cantidad);
        var cult = System.Globalization.CultureInfo.GetCultureInfo("es");
        var baseMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var res = new List<SerieMensualDto>(12);
        for (var i = 11; i >= 0; i--)
        {
            var f = baseMes.AddMonths(-i);
            var c = dict.TryGetValue((f.Year, f.Month), out var v) ? v : 0;
            res.Add(new SerieMensualDto(f.ToString("MMM yy", cult), c));
        }
        return res;
    }
}

// ── Portafolio de proyectos internos ──────────────────────────────────────
public sealed record ProyectosDashboardDto(
    int Total,
    int Abiertos,
    int EnEjecucion,
    int Cerrados,
    int AvancePromedio,
    int Atrasados,

    /// <summary>Proyectos abiertos sin fecha de cierre planificada.
    ///
    /// <para>Acompaña a <see cref="Atrasados"/> y no se puede leer sin él: un proyecto sin fecha
    /// comprometida nunca cuenta como atrasado, así que un «0 atrasados» junto a un
    /// <c>SinLineaBase</c> alto no significa que el portafolio esté al día — significa que no hay
    /// contra qué medirlo. Sin este número el tablero da falso verde.</para></summary>
    int SinLineaBase,

    /// <summary>Proyectos abiertos donde el trabajo reportado en las actividades y los entregables
    /// efectivamente cerrados cuentan historias distintas. Cuando se separan, una de las dos
    /// medidas dejó de mantenerse: o se reporta trabajo que no cierra nada, o se cierran
    /// entregables sin que nadie reportara el trabajo.</summary>
    int ConDivergencia,

    int SinReportar,
    int SinResponsable,
    int EntregablesVencidos,
    int EntregablesProximos,

    /// <summary>Actividades abiertas cuya fecha de fin ya pasó. Es la señal fina: aparece semanas
    /// antes de que venza el entregable que las contiene.</summary>
    int ActividadesVencidas,
    int ActividadesProximas,

    /// <summary>Proyectos en ejecución sin una sola actividad cargada. No pueden reportar avance:
    /// su porcentaje depende solo del estado de sus entregables.</summary>
    int SinDesglose,

    /// <summary>Actividades abiertas que esperan a otra que todavía no termina. Es atraso que
    /// todavía no se ve en ninguna fecha: la actividad puede tener su ventana entera por delante
    /// y aun así no poder arrancar.</summary>
    int ActividadesBloqueadas,

    /// <summary>De las anteriores, las que ya se están trabajando igual. O el plan está mal o la
    /// dependencia no era tal: en cualquier caso alguien tiene que mirarlo.</summary>
    int ArrancaronBloqueadas,

    int ReportesTotal,
    IReadOnlyList<ConteoDto>          PorEstado,
    IReadOnlyList<ConteoDto>          PorResponsable,
    IReadOnlyList<SerieMensualDto>    ReportesPorMes,
    IReadOnlyList<ProyectoSemaforoDto>    Semaforo,
    IReadOnlyList<EntregableAtencionDto>  Entregables,
    IReadOnlyList<ActividadAtencionDto>   Actividades,
    IReadOnlyList<BloqueoDto>         Bloqueos,
    IReadOnlyList<ActividadBloqueadaDto>  Bloqueadas);

/// <summary>Una fila del semáforo del portafolio: el estado de un proyecto y las dos
/// señales que definen si necesita atención (atraso de fecha y silencio del responsable).</summary>
public sealed record ProyectoSemaforoDto(
    int               ProyectoId,
    string            Codigo,
    string            Nombre,
    string?           Responsable,
    EstadoProyecto    Estado,
    PrioridadProyecto Prioridad,
    /// <summary>Promedio de las actividades del proyecto, subido por los entregables. Ya no lo
    /// declara el responsable: lo calcula el árbol.</summary>
    int               AvancePct,
    int               TotalEntregables,
    int               EntregablesCompletados,
    int               EntregablesVencidos,
    int               TotalActividades,
    int               ActividadesVencidas,
    DateOnly?         FechaFinPlan,
    DateTime?         UltimoAvance,
    int?              DiasSinReporte,
    bool              Atrasado,
    bool              SinReportar,

    /// <summary>Abierto y sin fecha de cierre comprometida. No es lo mismo que ir a tiempo:
    /// es que no hay fecha contra la cual estar atrasado.</summary>
    bool              SinLineaBase)
{
    /// <summary>Entregables cerrados sobre el total: lo que está efectivamente entregado, frente
    /// a <see cref="AvancePct"/>, que promedia el trabajo reportado en las actividades.</summary>
    public int AvanceFisico => TotalEntregables == 0 ? 0 : (int)Math.Round(EntregablesCompletados * 100.0 / TotalEntregables);

    /// <summary>Puntos de diferencia entre el trabajo reportado y los entregables cerrados.
    /// Positiva: se avanza en actividades que todavía no cierran nada. Negativa: se cierran
    /// entregables sin que las actividades lo reflejen.</summary>
    public int Brecha => AvancePct - AvanceFisico;

    /// <summary>Diferencia lo bastante grande como para que las dos medidas del proyecto estén
    /// contando cosas distintas. Solo aplica a proyectos con estructura cargada.</summary>
    public bool Divergente => TotalEntregables > 0 && Math.Abs(Brecha) >= BrechaAtencion;

    /// <summary>Umbral de la señal, en puntos porcentuales. 30 deja pasar el desfase normal entre
    /// un entregable grande y el reporte del mes, y marca los casos en que una de las dos medidas
    /// dejó de mantenerse.</summary>
    public const int BrechaAtencion = 30;

    /// <summary>En ejecución y sin actividades: el porcentaje sale solo del estado de los
    /// entregables, así que no hay nada que reportar hasta que se cargue el desglose.</summary>
    public bool SinDesglose => TotalActividades == 0 && Estado == EstadoProyecto.EnEjecucion;
}

/// <summary>Entregable abierto con fecha comprometida: vencido o dentro de la ventana próxima.</summary>
public sealed record EntregableAtencionDto(
    int              ProyectoId,
    string           Codigo,
    string           Proyecto,
    string           Entregable,
    string?          Responsable,
    DateOnly         FechaPlan,
    EstadoEntregable Estado,
    int              AvancePct);

/// <summary>Actividad abierta cuya ventana ya venció o está por vencer. Es el nivel donde el
/// atraso se ve primero: un entregable a tres meses puede tener actividades vencidas hoy.</summary>
public sealed record ActividadAtencionDto(
    int             ProyectoId,
    string          Codigo,
    string          Proyecto,
    string          Entregable,
    string          Actividad,
    string?         Responsable,
    DateOnly?       FechaInicioPlan,
    DateOnly        FechaFinPlan,
    int             AvancePct,
    EstadoActividad Estado,

    /// <summary>Cuándo se registró la actividad en el portal. Nulo en lo anterior al 26-08-2026:
    /// hasta esa fecha la entidad no llevaba auditoría.</summary>
    DateTime?       CreadoEn = null)
{
    /// <summary>Debía haber arrancado y sigue en cero. Peor señal que ir atrasada: nadie la
    /// empezó.</summary>
    public bool NoArrancada =>
        FechaInicioPlan.HasValue
        && FechaInicioPlan < DateOnly.FromDateTime(DateTime.UtcNow)
        && Estado == EstadoActividad.Pendiente;
}

/// <summary>Bloqueo declarado en el último reporte de avance de un proyecto.</summary>
public sealed record BloqueoDto(
    int      ProyectoId,
    string   Codigo,
    string   Proyecto,
    string   Texto,
    string   Autor,
    DateTime Fecha);

/// <summary>
/// Una actividad que no debería haber arrancado: alguna de las actividades de las que depende
/// sigue abierta.
///
/// <para>No la detecta ninguna fecha. Una actividad bloqueada puede tener su ventana entera por
/// delante y estar igual de trancada, y por eso viaja separada de
/// <see cref="ActividadAtencionDto"/>, que ordena por vencimiento.</para>
/// </summary>
public sealed record ActividadBloqueadaDto(
    int             ProyectoId,
    string          Codigo,
    string          Proyecto,
    string          Entregable,
    string          Actividad,
    string?         Responsable,
    DateOnly?       FechaInicioPlan,
    int             AvancePct,
    EstadoActividad Estado,

    /// <summary>Las predecesoras que todavía no terminan, por nombre. Es lo que hay que destrabar.</summary>
    IReadOnlyList<string> Espera,

    /// <summary>Ver <see cref="ActividadAtencionDto.CreadoEn"/>.</summary>
    DateTime? CreadoEn = null)
{
    /// <summary>Ya se está trabajando en ella pese al bloqueo. Encabeza la lista: es la fila que
    /// obliga a decidir algo, en vez de solo esperar.</summary>
    public bool Arrancada => Estado is EstadoActividad.EnProceso or EstadoActividad.Completada;

    /// <summary>Debía haber empezado y encima está trancada.</summary>
    public bool DebioArrancar =>
        FechaInicioPlan.HasValue
        && FechaInicioPlan < DateOnly.FromDateTime(DateTime.UtcNow)
        && Estado == EstadoActividad.Pendiente;
}
