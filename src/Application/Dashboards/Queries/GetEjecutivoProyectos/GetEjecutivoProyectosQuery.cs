using Diger.TramitesEstado.Application.Dashboards.Common;

namespace Diger.TramitesEstado.Application.Dashboards.Queries.GetEjecutivoProyectos;

/// <summary>
/// Tablero ejecutivo de proyectos: la cartera de un área leída por una jefatura en cinco
/// segundos —cuántos hay, si van en plan, cuáles están en problemas y qué espera una decisión—.
/// Especificación: <c>docs/tableros/ejecutivo-proyectos.md</c>.
///
/// <para><b>No consulta por su cuenta.</b> Todo sale de <see cref="GetProyectosDashboardQuery"/>,
/// que ya resuelve el alcance, el semáforo, los bloqueos vigentes y las actividades bloqueadas, y
/// que desde esta entrega trae además el avance esperado de cada proyecto. Aquí solo se recorta
/// por unidad, se evalúa la salud (<see cref="ProyectoEstadoReglas.Salud"/>) y se arman los
/// agregados. Así el ejecutivo y el operativo no pueden contar distinto el mismo proyecto.</para>
/// </summary>
/// <param name="AreaId">Cartera. <c>null</c> = todas las áreas visibles. Filtro de presentación:
/// recorta lo que dejó pasar el alcance, nunca lo amplía.</param>
/// <param name="UnidadId">Recorte por unidad dentro de la categoría, en memoria sobre el
/// semáforo. Se ignora si ninguna fila de la categoría es de esa unidad.</param>
/// <param name="ResponsableId">Recorte por responsable dentro de la categoría, con la misma
/// regla.</param>
/// <param name="CategoriaId">Categoría del catálogo (las «Q»). <c>null</c> = todas. Va a la
/// consulta del portafolio: define la cartera sobre la que se ofrecen unidad y responsable.</param>
public sealed record GetEjecutivoProyectosQuery(
    string? AreaId, string? UnidadId = null, Guid? ResponsableId = null, int? CategoriaId = null)
    : IRequest<EjecutivoProyectosDto>;

public sealed record EjecutivoProyectosDto(
    int Total, int Finalizados, int EnProceso, int NoIniciados, int Suspendidos, int Cancelados, int SinEstructura,
    int ConEstructura, decimal AvanceReal,
    int ConLineaBase, decimal? AvanceEsperado, decimal? Desviacion,

    /// <summary>Avance real promediado solo sobre los proyectos con línea base: la cifra que se pone
    /// junto al esperado. <see cref="AvanceReal"/> promedia otra población (los que tienen
    /// estructura) y no se compara con el esperado (Revisión 1, R1).</summary>
    decimal? AvanceRealConLineaBase,
    int BloqueosVigentes, int? DiasBloqueoMasAntiguo,
    int Rojos, int Ambar, int Verdes, int Neutros,
    DateTime? UltimoReporte, DateTime CalculadoEnUtc,

    /// <summary>Segmentos de la dona, solo los que tienen proyectos, con su estado para que la
    /// vista elija el color sin comparar textos.</summary>
    IReadOnlyList<SegmentoEstadoDto> PorEstado,

    /// <summary>Ya ordenados: rojo, ámbar, verde, neutro (§5.3 de la especificación).</summary>
    IReadOnlyList<ProyectoEjecutivoDto> Proyectos,

    /// <summary>Ya ordenadas: bloqueos, cierres vencidos, arrancadas bloqueadas; dentro de cada
    /// tipo, lo más antiguo primero.</summary>
    IReadOnlyList<DecisionDto> Decisiones,

    /// <summary>Unidades y responsables con proyectos en la cartera (área y categoría), antes de
    /// recortar por ellos: las opciones del filtro no se achican con la elección propia.</summary>
    IReadOnlyList<OpcionFiltroDto> Unidades,
    IReadOnlyList<OpcionFiltroDto> Responsables,

    /// <summary>El recorte que de verdad se aplicó: null si no se pidió o si no pertenecía a la
    /// cartera.</summary>
    string? UnidadAplicada,
    Guid?   ResponsableAplicado)
{
    /// <summary>En ejecución con salud roja: el subtítulo del KPI «En proceso».</summary>
    public int EnProcesoEnRojo => Proyectos.Count(p => p.Estado == EstadoProyecto.EnEjecucion && p.Salud == SaludProyecto.Rojo);
}

/// <summary>Una opción del filtro «Unidad o responsable», con cuántos proyectos tiene.</summary>
public sealed record OpcionFiltroDto(string Id, string Nombre, int Cantidad);

/// <summary>Un segmento de la dona «Estado de los proyectos».</summary>
public sealed record SegmentoEstadoDto(EstadoProyecto Estado, string Etiqueta, int Cantidad);

public sealed record ProyectoEjecutivoDto(
    int ProyectoId, string Codigo, string Nombre, string? Responsable, string? UnidadNombre,
    EstadoProyecto Estado, int AvancePct, int? AvanceEsperado, int? Desviacion, bool SinEstructura,
    DateTime? UltimoAvance, int? DiasSinReporte, bool BloqueoVigente,
    SaludProyecto Salud, string EtiquetaSalud, string MotivoSalud,
    /// <summary>Id de la unidad, por si el nombre no resuelve (unidad de otra institución).</summary>
    string? UnidadId = null,

    /// <summary>Nombre corto de la unidad («DAI»), el que va en el subtítulo de la fila.</summary>
    string? UnidadNombreCorto = null,

    /// <summary>Reportes que declararon un bloqueo, en toda la historia del proyecto: la gráfica
    /// «Bloqueos por proyecto».</summary>
    int BloqueosRegistrados = 0)
{
    /// <summary>Lo que se escribe como unidad en la fila: corto, si no el nombre, si no el Id.</summary>
    public string? UnidadEtiqueta => UnidadNombreCorto ?? UnidadNombre ?? UnidadId;
}

public enum TipoDecision { BloqueoVigente, CierreVencido, ArrancoBloqueada }

/// <summary>Un asunto que espera una decisión de la jefatura.</summary>
public sealed record DecisionDto(
    TipoDecision Tipo, int ProyectoId, string Proyecto, string Texto, string? Autor, DateTime Fecha, int Dias);

public sealed class GetEjecutivoProyectosQueryHandler(ISender sender)
    : IRequestHandler<GetEjecutivoProyectosQuery, EjecutivoProyectosDto>
{
    public async Task<EjecutivoProyectosDto> Handle(GetEjecutivoProyectosQuery q, CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;
        var hoy   = DateOnly.FromDateTime(ahora);

        var d = await sender.Send(new GetProyectosDashboardQuery(
            AreaIds: string.IsNullOrEmpty(q.AreaId) ? null : [q.AreaId],
            CategoriaId: q.CategoriaId), ct);

        // Unidad y responsable se recortan aquí, en memoria, y no en la consulta del portafolio:
        // así la misma pasada da las opciones del filtro (lo que hay en la cartera) y el recorte.
        // Un recorte que no está entre las opciones se descarta en vez de dar un tablero vacío.
        // Bloqueos y bloqueadas se recortan por el mismo conjunto de ids, para que las decisiones
        // cuadren con la lista.
        var unidades = d.Semaforo
            .Where(p => p.UnidadId != null)
            .GroupBy(p => p.UnidadId!)
            .Select(g => new OpcionFiltroDto(g.Key,
                g.First().UnidadNombre ?? g.First().UnidadNombreCorto ?? g.Key, g.Count()))
            .OrderBy(o => o.Nombre)
            .ToList();
        var responsables = d.Semaforo
            .Where(p => p.ResponsableId != null)
            .GroupBy(p => p.ResponsableId!.Value)
            .Select(g => new OpcionFiltroDto(g.Key.ToString(), g.First().Responsable ?? "sin nombre", g.Count()))
            .OrderBy(o => o.Nombre)
            .ToList();

        var unidadAplicada = unidades.Any(o => o.Id == q.UnidadId) ? q.UnidadId : null;
        var responsableAplicado = q.ResponsableId is { } r && responsables.Any(o => o.Id == r.ToString())
            ? q.ResponsableId : null;

        var semaforo = d.Semaforo
            .Where(p => unidadAplicada is null || p.UnidadId == unidadAplicada)
            .Where(p => responsableAplicado is null || p.ResponsableId == responsableAplicado)
            .ToList();

        var ids        = semaforo.Select(p => p.ProyectoId).ToHashSet();
        var bloqueos   = d.Bloqueos.Where(b => ids.Contains(b.ProyectoId)).ToList();
        var bloqueadas = d.Bloqueadas.Where(b => ids.Contains(b.ProyectoId)).ToList();
        var conBloqueo = bloqueos.Select(b => b.ProyectoId).ToHashSet();

        var proyectos = semaforo
            .Select(p =>
            {
                int? desviacion = p.AvanceEsperado is { } esp ? p.AvancePct - esp : null;
                var salud = ProyectoEstadoReglas.Salud(
                    p.Estado, p.FechaFinPlan, p.UltimoAvance, conBloqueo.Contains(p.ProyectoId),
                    p.TotalEntregables, desviacion, hoy, ahora, p.FechaInicioPlan, p.FechaFinReal);

                return new ProyectoEjecutivoDto(
                    p.ProyectoId, p.Codigo, p.Nombre, p.Responsable, p.UnidadNombre,
                    p.Estado, p.AvancePct, p.AvanceEsperado, desviacion, p.TotalEntregables == 0,
                    p.UltimoAvance, p.DiasSinReporte, conBloqueo.Contains(p.ProyectoId),
                    salud.Nivel, salud.Etiqueta, salud.Motivo, p.UnidadId, p.UnidadNombreCorto,
                    p.BloqueosRegistrados);
            })
            .OrderBy(p => ProyectoEstadoReglas.Orden(p.Salud))
            .ThenBy(p => p.Salud switch
            {
                SaludProyecto.Rojo   => OrdenRojo(p.EtiquetaSalud),
                SaludProyecto.Neutro => OrdenNeutro(p.EtiquetaSalud),
                _                    => 0
            })
            .ThenBy(p => p.Desviacion is null ? 1 : 0)
            .ThenBy(p => p.Desviacion ?? 0)
            // Solo desempata dentro del rojo: a igual desviación, el que lleva más tiempo callado.
            .ThenByDescending(p => p.Salud == SaludProyecto.Rojo ? p.DiasSinReporte ?? int.MaxValue : 0)
            .ThenBy(p => p.Nombre)
            .ToList();

        // ── Avance real contra esperado ──────────────────────────────────────
        // El real promedia los proyectos con estructura (sin entregables no hay contra qué medir);
        // el esperado y la desviación, los que tienen línea base. Cerrados incluidos en los dos:
        // la cartera es la cartera.
        var conEstructura = semaforo.Where(p => p.TotalEntregables > 0).ToList();
        var conLineaBase  = semaforo.Where(p => p.AvanceEsperado is not null).ToList();

        decimal Redondear(double x) => (decimal)Math.Round(x, 1, MidpointRounding.AwayFromZero);

        var avanceReal = conEstructura.Count == 0 ? 0m : Redondear(conEstructura.Average(p => (double)p.AvancePct));
        decimal? avanceEsperado = conLineaBase.Count == 0 ? null : Redondear(conLineaBase.Average(p => (double)p.AvanceEsperado!.Value));
        decimal? realConLineaBase = conLineaBase.Count == 0 ? null : Redondear(conLineaBase.Average(p => (double)p.AvancePct));
        decimal? desviacionCartera = conLineaBase.Count == 0 ? null
            : Redondear(conLineaBase.Average(p => (double)(p.AvancePct - p.AvanceEsperado!.Value)));

        // ── Dona por estado real ─────────────────────────────────────────────
        var porEstado = new[]
            {
                EstadoProyecto.EnEjecucion, EstadoProyecto.Planificado, EstadoProyecto.Suspendido,
                EstadoProyecto.Cerrado, EstadoProyecto.Cancelado
            }
            .Select(e => new SegmentoEstadoDto(e, Etiquetas.EstadoEjecutivo(e), semaforo.Count(p => p.Estado == e)))
            .Where(s => s.Cantidad > 0)
            .ToList();

        // ── Necesita decisión ────────────────────────────────────────────────
        int Dias(DateTime f) => Math.Max(0, (int)(ahora - f).TotalDays);

        var decisiones = new List<DecisionDto>();

        decisiones.AddRange(bloqueos
            .OrderBy(b => b.Fecha)
            .Select(b => new DecisionDto(TipoDecision.BloqueoVigente, b.ProyectoId, b.Proyecto,
                b.Texto, b.Autor, b.Fecha, Dias(b.Fecha))));

        decisiones.AddRange(semaforo
            .Where(p => p.Atrasado)
            .OrderBy(p => p.FechaFinPlan)
            .Select(p =>
            {
                var fin  = p.FechaFinPlan!.Value;
                var dias = hoy.DayNumber - fin.DayNumber;
                return new DecisionDto(TipoDecision.CierreVencido, p.ProyectoId, p.Nombre,
                    $"cierre planificado el {FormatoEjecutivo.DiaMes(fin)}, vencido {FormatoEjecutivo.Hace(dias)}",
                    null, fin.ToDateTime(TimeOnly.MinValue), dias);
            }));

        decisiones.AddRange(bloqueadas
            .Where(b => b.Arrancada)
            .Select(b =>
            {
                // La fecha que fecha el asunto: cuándo debía arrancar o, sin plan, cuándo se cargó.
                var fecha = b.FechaInicioPlan?.ToDateTime(TimeOnly.MinValue) ?? b.CreadoEn ?? ahora;
                var espera = string.Join("», «", b.Espera);
                return new DecisionDto(TipoDecision.ArrancoBloqueada, b.ProyectoId, b.Proyecto,
                    $"«{b.Actividad}» avanza al {b.AvancePct} % pese a esperar a «{espera}»",
                    null, fecha, Dias(fecha));
            })
            .OrderBy(x => x.Fecha));

        return new EjecutivoProyectosDto(
            Total:         semaforo.Count,
            Finalizados:   semaforo.Count(p => p.Estado == EstadoProyecto.Cerrado),
            EnProceso:     semaforo.Count(p => p.Estado == EstadoProyecto.EnEjecucion),
            NoIniciados:   semaforo.Count(p => p.Estado == EstadoProyecto.Planificado),
            Suspendidos:   semaforo.Count(p => p.Estado == EstadoProyecto.Suspendido),
            Cancelados:    semaforo.Count(p => p.Estado == EstadoProyecto.Cancelado),
            SinEstructura: semaforo.Count(p => p.TotalEntregables == 0),
            ConEstructura: conEstructura.Count,
            AvanceReal:    avanceReal,
            ConLineaBase:  conLineaBase.Count,
            AvanceEsperado: avanceEsperado,
            Desviacion:    desviacionCartera,
            AvanceRealConLineaBase: realConLineaBase,
            BloqueosVigentes: bloqueos.Count,
            DiasBloqueoMasAntiguo: bloqueos.Count == 0 ? null : bloqueos.Max(b => Dias(b.Fecha)),
            Rojos:   proyectos.Count(p => p.Salud == SaludProyecto.Rojo),
            Ambar:   proyectos.Count(p => p.Salud == SaludProyecto.Ambar),
            Verdes:  proyectos.Count(p => p.Salud == SaludProyecto.Verde),
            Neutros: proyectos.Count(p => p.Salud == SaludProyecto.Neutro),
            UltimoReporte: semaforo.Max(p => p.UltimoAvance),
            CalculadoEnUtc: ahora,
            PorEstado:  porEstado,
            Proyectos:  proyectos,
            Decisiones: decisiones,
            Unidades:     unidades,
            Responsables: responsables,
            UnidadAplicada:      unidadAplicada,
            ResponsableAplicado: responsableAplicado);
    }

    /// <summary>Orden dentro del rojo: lo que exige una decisión primero. Un cierre vencido es un
    /// compromiso roto, un bloqueo espera a alguien; el atraso por desviación viene después
    /// (Revisión 1, R3).</summary>
    private static int OrdenRojo(string etiqueta) => etiqueta switch
    {
        "Vencido"   => 0,
        "Bloqueado" => 1,
        _           => 2
    };

    /// <summary>Orden de los neutros al final de la lista: lo terminado, lo que no empezó, lo que
    /// no tiene estructura y lo cancelado.</summary>
    private static int OrdenNeutro(string etiqueta) => etiqueta switch
    {
        "Finalizado"  => 0,
        "No iniciado" => 1,
        "Sin dato"    => 2,
        _             => 3
    };
}
