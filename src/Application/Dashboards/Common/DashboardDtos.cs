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

    /// <summary>Proyectos abiertos cuyo avance declarado y cuyo cronograma cuentan historias
    /// distintas: el porcentaje lo escribe el responsable y los hitos son lo verificable, y
    /// cuando se separan una de las dos medidas dejó de mantenerse.</summary>
    int ConDivergencia,

    int SinReportar,
    int SinResponsable,
    int HitosVencidos,
    int HitosProximos,
    int ReportesTotal,
    IReadOnlyList<ConteoDto>          PorEstado,
    IReadOnlyList<ConteoDto>          PorResponsable,
    IReadOnlyList<SerieMensualDto>    ReportesPorMes,
    IReadOnlyList<ProyectoSemaforoDto> Semaforo,
    IReadOnlyList<HitoAtencionDto>    Hitos,
    IReadOnlyList<BloqueoDto>         Bloqueos);

/// <summary>Una fila del semáforo del portafolio: el estado de un proyecto y las dos
/// señales que definen si necesita atención (atraso de fecha y silencio del responsable).</summary>
public sealed record ProyectoSemaforoDto(
    int               ProyectoId,
    string            Codigo,
    string            Nombre,
    string?           Responsable,
    EstadoProyecto    Estado,
    PrioridadProyecto Prioridad,
    int               AvancePct,
    int               TotalHitos,
    int               HitosCompletados,
    int               HitosVencidos,
    DateOnly?         FechaFinPlan,
    DateTime?         UltimoAvance,
    int?              DiasSinReporte,
    bool              Atrasado,
    bool              SinReportar,

    /// <summary>Abierto y sin fecha de cierre comprometida. No es lo mismo que ir a tiempo:
    /// es que no hay fecha contra la cual estar atrasado.</summary>
    bool              SinLineaBase)
{
    /// <summary>Avance según el cronograma: hitos completados sobre el total. Es la única medida
    /// verificable que tiene el proyecto — <see cref="AvancePct"/> lo declara el responsable.</summary>
    public int AvanceFisico => TotalHitos == 0 ? 0 : (int)Math.Round(HitosCompletados * 100.0 / TotalHitos);

    /// <summary>Puntos de diferencia entre lo declarado y lo que muestran los hitos. Positiva:
    /// se reporta más de lo que se cierra. Negativa: se cierran hitos sin reportarlos.</summary>
    public int Brecha => AvancePct - AvanceFisico;

    /// <summary>Diferencia lo bastante grande como para que las dos medidas del proyecto estén
    /// contando cosas distintas. Solo aplica a proyectos con cronograma cargado.</summary>
    public bool Divergente => TotalHitos > 0 && Math.Abs(Brecha) >= BrechaAtencion;

    /// <summary>Umbral de la señal, en puntos porcentuales. 30 deja pasar el desfase normal entre
    /// un hito grande y el reporte del mes, y marca los casos en que una de las dos medidas dejó
    /// de mantenerse.</summary>
    public const int BrechaAtencion = 30;
}

/// <summary>Hito abierto con fecha comprometida: vencido o dentro de la ventana próxima.</summary>
public sealed record HitoAtencionDto(
    int        ProyectoId,
    string     Codigo,
    string     Proyecto,
    string     Hito,
    string?    Responsable,
    DateOnly   FechaPlan,
    EstadoHito Estado);

/// <summary>Bloqueo declarado en el último reporte de avance de un proyecto.</summary>
public sealed record BloqueoDto(
    int      ProyectoId,
    string   Codigo,
    string   Proyecto,
    string   Texto,
    string   Autor,
    DateTime Fecha);
