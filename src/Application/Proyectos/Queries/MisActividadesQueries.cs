using Diger.TramitesEstado.Application.Proyectos.Common;

namespace Diger.TramitesEstado.Application.Proyectos.Queries;

/// <summary>
/// Una actividad a cargo de la persona conectada, con el contexto mínimo para poder reportar sin
/// abrir el proyecto: de qué proyecto y entregable cuelga, en qué fecha vence y qué la traba.
/// </summary>
public sealed record MiActividadDto(
    int             ActividadId,
    int             EntregableId,
    int             ProyectoId,
    string          ProyectoCodigo,
    string          ProyectoNombre,
    string          Entregable,
    string          Actividad,
    string?         Descripcion,
    DateOnly?       FechaInicioPlan,
    DateOnly?       FechaFinPlan,
    int             AvancePct,
    EstadoActividad Estado,

    /// <summary>Las predecesoras que todavía no terminan. Vacío = nada la traba.</summary>
    IReadOnlyList<string> Espera,

    /// <summary>Último reporte que se imputó a esta actividad. Es lo que responde «¿ya reporté
    /// esto?» sin tener que abrir la bitácora del proyecto.</summary>
    DateTime? UltimoReporte,

    /// <summary>Cuándo se registró la actividad. Nulo en lo anterior al 26-08-2026.</summary>
    DateTime? CreadoEn = null)
{
    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.UtcNow);

    public bool Bloqueada  => Espera.Count > 0;
    public bool Terminada  => Estado == EstadoActividad.Completada;

    public bool Vencida =>
        FechaFinPlan is { } fin && fin < Hoy
        && Estado is EstadoActividad.Pendiente or EstadoActividad.EnProceso;

    /// <summary>Debía haber arrancado y sigue en cero. Aparece semanas antes que el vencimiento.</summary>
    public bool NoArrancada =>
        FechaInicioPlan is { } ini && ini < Hoy && Estado == EstadoActividad.Pendiente;

    /// <summary>Vence dentro de la ventana de atención y todavía no venció.</summary>
    public bool PorVencer =>
        FechaFinPlan is { } fin && fin >= Hoy && fin <= Hoy.AddDays(DiasProximos)
        && Estado is EstadoActividad.Pendiente or EstadoActividad.EnProceso;

    /// <summary>Abierta y sin fecha de cierre: no está atrasada porque no hay contra qué medirla.
    /// Se cuenta aparte para que el «0 vencidas» no se lea como estar al día.</summary>
    public bool SinFecha =>
        FechaFinPlan is null && Estado is EstadoActividad.Pendiente or EstadoActividad.EnProceso;

    public int? DiasParaVencer =>
        FechaFinPlan is { } fin ? fin.DayNumber - Hoy.DayNumber : null;

    public int? DiasSinReportar =>
        UltimoReporte is { } u ? (int)(DateTime.UtcNow - u).TotalDays : null;

    /// <summary>Nunca se reportó nada sobre ella y ya se está trabajando: el número subió sin que
    /// quede escrito por qué.</summary>
    public bool SinBitacora => UltimoReporte is null && AvancePct > 0;

    public const int DiasProximos = 30;
}

/// <summary>La bandeja completa, con los contadores que encabezan la pantalla.</summary>
public sealed record MisActividadesDto(
    IReadOnlyList<MiActividadDto> Actividades,
    int TotalAbiertas,
    int Vencidas,
    int PorVencer,
    int Bloqueadas,
    int SinFecha,
    int Terminadas);

/// <summary>
/// Las actividades a cargo de quien está conectado, de todos los proyectos que puede ver.
///
/// <para><b>El usuario no viaja en la consulta</b>, a propósito: lo resuelve
/// <c>ICurrentUserService</c>. Así nadie pide la bandeja de otro cambiando un valor en la petición
/// — es el mismo criterio con el que SGSEC dejó de aceptar el id del usuario desde el navegador.</para>
///
/// <para>El cruce con <c>Proyectos</c> es lo que aplica el alcance: <c>ProyectoActividades</c> no
/// lleva filtro propio, su ancla es el proyecto del que cuelga. En la práctica quien responde por
/// una actividad es interesado del proyecto —lo exige <c>ResponsablesProyecto</c>— así que lo ve;
/// las asignaciones heredadas de la carga inicial pueden no serlo, y en ese caso la actividad no
/// aparece, que es lo correcto: no se puede reportar sobre algo que no se puede abrir.</para>
/// </summary>
/// <param name="IncluirTerminadas">Por omisión la bandeja muestra lo que falta. Las terminadas se
/// piden aparte para revisar lo hecho sin que estorben todos los días.</param>
public sealed record GetMisActividadesQuery(bool IncluirTerminadas = false) : IRequest<MisActividadesDto>;

public sealed class GetMisActividadesQueryHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<GetMisActividadesQuery, MisActividadesDto>
{
    public async Task<MisActividadesDto> Handle(GetMisActividadesQuery q, CancellationToken ct)
    {
        if (currentUser.UserId is not { } usuarioId)
            return new MisActividadesDto([], 0, 0, 0, 0, 0, 0);

        // Un proyecto cerrado o cancelado ya no admite reportes: sus actividades no son trabajo
        // pendiente de nadie, aunque sigan asignadas.
        var filas = await ctx.ProyectoActividades.AsNoTracking()
            .Where(a => a.ResponsableId == usuarioId && a.Estado != EstadoActividad.Cancelada)
            .Join(ctx.ProyectoEntregables.AsNoTracking(), a => a.EntregableId, e => e.Id, (a, e) => new { a, e })
            .Join(ctx.Proyectos.AsNoTracking(), z => z.e.ProyectoId, p => p.Id, (z, p) => new { z.a, z.e, p })
            .Where(z => z.p.Estado != EstadoProyecto.Cerrado && z.p.Estado != EstadoProyecto.Cancelado)
            .Select(z => new
            {
                z.a.Id, z.a.Nombre, z.a.Descripcion, z.a.FechaInicioPlan, z.a.FechaFinPlan,
                z.a.AvancePct, z.a.Estado, z.a.CreatedAt,
                EntregableId     = z.e.Id,
                Entregable       = z.e.Nombre,
                ProyectoId       = z.p.Id,
                ProyectoCodigo   = z.p.Codigo,
                ProyectoNombre   = z.p.Nombre
            })
            .ToListAsync(ct);

        if (filas.Count == 0)
            return new MisActividadesDto([], 0, 0, 0, 0, 0, 0);

        var ids = filas.Select(f => f.Id).ToList();

        // Qué traba cada una. Dos consultas planas y el cruce en memoria: la regla —«la predecesora
        // que sigue abierta bloquea»— vive en el dominio y no se traduce a SQL.
        var dependencias = await ctx.ProyectoDependencias.AsNoTracking()
            .Where(d => ids.Contains(d.SucesoraId))
            .Select(d => new { d.SucesoraId, d.PredecesoraId })
            .ToListAsync(ct);

        var predecesoraIds = dependencias.Select(d => d.PredecesoraId).Distinct().ToList();
        var predecesoras = predecesoraIds.Count == 0
            ? []
            : await ctx.ProyectoActividades.AsNoTracking()
                .Where(a => predecesoraIds.Contains(a.Id))
                .Select(a => new { a.Id, a.Nombre, a.Estado })
                .ToListAsync(ct);

        var porId = predecesoras.ToDictionary(p => p.Id);

        var espera = dependencias
            .Where(d => porId.TryGetValue(d.PredecesoraId, out var pre)
                     && pre.Estado is EstadoActividad.Pendiente or EstadoActividad.EnProceso)
            .GroupBy(d => d.SucesoraId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(d => porId[d.PredecesoraId].Nombre)
                                                                   .OrderBy(n => n).ToList());

        // Último reporte imputado a cada actividad, en una sola agregación.
        var ultimos = await ctx.ProyectoAvances.AsNoTracking()
            .Where(a => a.ActividadId != null && ids.Contains(a.ActividadId.Value))
            .GroupBy(a => a.ActividadId!.Value)
            .Select(g => new { ActividadId = g.Key, Ultimo = g.Max(x => x.Fecha) })
            .ToDictionaryAsync(x => x.ActividadId, x => x.Ultimo, ct);

        var actividades = filas
            .Select(f => new MiActividadDto(
                f.Id, f.EntregableId, f.ProyectoId, f.ProyectoCodigo, f.ProyectoNombre,
                f.Entregable, f.Nombre, f.Descripcion,
                f.FechaInicioPlan, f.FechaFinPlan, f.AvancePct, f.Estado,
                espera.GetValueOrDefault(f.Id, []),
                ultimos.TryGetValue(f.Id, out var u) ? u : null,
                f.CreatedAt == default ? null : f.CreatedAt))
            .ToList();

        var abiertas = actividades.Where(a => !a.Terminada).ToList();

        var visibles = (q.IncluirTerminadas ? actividades : abiertas)
            // El orden es el de la urgencia, no el alfabético: primero lo vencido, después lo que
            // está por vencer, y al final lo que no tiene fecha contra la cual medirse.
            .OrderByDescending(a => a.Vencida)
            .ThenBy(a => a.FechaFinPlan ?? DateOnly.MaxValue)
            .ThenByDescending(a => a.Bloqueada)
            .ThenBy(a => a.ProyectoCodigo)
            .ThenBy(a => a.Actividad)
            .ToList();

        return new MisActividadesDto(
            visibles,
            TotalAbiertas: abiertas.Count,
            Vencidas:      abiertas.Count(a => a.Vencida),
            PorVencer:     abiertas.Count(a => a.PorVencer),
            Bloqueadas:    abiertas.Count(a => a.Bloqueada),
            SinFecha:      abiertas.Count(a => a.SinFecha),
            Terminadas:    actividades.Count(a => a.Terminada));
    }
}
