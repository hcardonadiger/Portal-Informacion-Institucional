using Diger.TramitesEstado.Domain.Enums;

namespace Diger.TramitesEstado.Application.Dashboards.Common;

/// <summary>
/// Las reglas con las que los tableros de proyectos deciden qué está abierto, qué está
/// atrasado y de qué no reporta nadie.
///
/// <para>Viven aquí y no en cada consulta porque los tres tableros —Institución, Área y
/// Unidad— tienen que decir lo mismo del mismo proyecto: si el umbral de «sin reportar»
/// cambia, o si Suspendido deja de contar como abierto, el cambio ocurre una sola vez y
/// alcanza a los tres. Copiadas, se separan sin que nadie lo note hasta que dos pantallas
/// se contradicen delante de un jefe.</para>
///
/// <para>Son reglas <b>en memoria</b>, para evaluarse sobre lo ya materializado. No se usan
/// dentro de un <c>IQueryable</c>: una llamada a método no la traduce EF a SQL y terminaría
/// evaluando el filtro en el cliente, trayéndose la tabla entera.</para>
/// </summary>
public static class ProyectoEstadoReglas
{
    /// <summary>Días sin reporte a partir de los cuales un proyecto en ejecución se marca como
    /// desatendido. Mismo umbral que usa el listado del módulo, para que no digan cosas
    /// distintas sobre el mismo proyecto.</summary>
    public const int DiasSinReporte = 30;

    /// <summary>El proyecto sigue vivo: ni cerrado ni cancelado. Suspendido cuenta como abierto
    /// —está detenido, no terminado— y por eso puede atrasarse.</summary>
    public static bool Abierto(EstadoProyecto estado) =>
        estado is EstadoProyecto.Planificado or EstadoProyecto.EnEjecucion or EstadoProyecto.Suspendido;

    /// <summary>Fecha a partir de la cual un reporte ya no cuenta como reciente.</summary>
    public static DateTime CorteSinReporte(DateTime ahoraUtc) => ahoraUtc.AddDays(-DiasSinReporte);

    /// <summary>Pasó la fecha planificada de cierre y el proyecto sigue abierto. Sin fecha no hay
    /// atraso posible: ese caso es «sin línea base», que se cuenta aparte.</summary>
    public static bool Atrasado(DateOnly? fechaFinPlan, EstadoProyecto estado, DateOnly hoy) =>
        fechaFinPlan.HasValue && fechaFinPlan < hoy && Abierto(estado);

    /// <summary>En ejecución y sin avances desde el corte. Es el síntoma que el porcentaje no
    /// muestra: un proyecto puede tener un avance creíble y llevar un mes sin que nadie lo toque.</summary>
    public static bool SinReportar(EstadoProyecto estado, DateTime? ultimoAvance, DateTime corte) =>
        estado == EstadoProyecto.EnEjecucion && (ultimoAvance is null || ultimoAvance < corte);

    /// <summary>Avance promedio del portafolio, redondeado a entero. Se calcula solo sobre los
    /// proyectos en ejecución —los planificados arrastran el número a cero y los cerrados lo
    /// inflan— y sin ninguno devuelve 0 en lugar de reventar.</summary>
    public static int AvancePromedio(IEnumerable<int> avances)
    {
        var valores = avances.ToList();
        return valores.Count == 0 ? 0 : (int)Math.Round(valores.Average());
    }

    // ── Avance esperado (contra el plan) ──────────────────────────────────

    /// <summary>
    /// Cuánto debería llevar hecho un proyecto hoy según su plan: cada actividad se interpola
    /// sobre su ventana —0 antes de empezar, 100 pasada la fecha de fin, proporcional en medio— y
    /// se promedian. Recibe solo actividades vigentes (no canceladas) con <b>las dos</b> fechas
    /// planificadas: comparar contra un plan que no existe daría un número inventado.
    ///
    /// <para>Es la fórmula del tablero de un proyecto (<c>GetTableroProyectoQuery</c>), que ahora
    /// la llama desde aquí; el tablero ejecutivo la usa para cada proyecto del portafolio. Una
    /// sola definición para que las dos pantallas no digan cosas distintas del mismo proyecto.
    /// El redondeo es el de <see cref="Math.Round(double)"/> sin modo explícito, el mismo que
    /// usaba el tablero del proyecto, para no mover ningún número que ya estuviera a la vista.</para>
    /// </summary>
    /// <returns><c>null</c> cuando no hay ninguna ventana: el proyecto no tiene línea base.</returns>
    public static int? AvanceEsperado(IEnumerable<(DateOnly Ini, DateOnly Fin)> ventanas, DateOnly hoy)
    {
        var lista = ventanas.ToList();
        if (lista.Count == 0) return null;

        return (int)Math.Round(lista.Average(v =>
        {
            if (hoy <= v.Ini) return 0d;
            if (hoy >= v.Fin) return 100d;
            var total = v.Fin.DayNumber - v.Ini.DayNumber;
            return total <= 0 ? 100d : (hoy.DayNumber - v.Ini.DayNumber) * 100d / total;
        }));
    }

    // ── Semáforo de salud del tablero ejecutivo ───────────────────────────

    /// <summary>Puntos de desviación (real − esperado) a partir de los cuales el proyecto está en
    /// rojo.</summary>
    public const int DesviacionRoja = -20;

    /// <summary>Puntos de desviación a partir de los cuales está en ámbar. Entre este umbral y 0 se
    /// considera en plan.</summary>
    public const int DesviacionAmbar = -5;

    /// <summary>
    /// Salud de un proyecto para el tablero ejecutivo. La primera regla que aplica gana; el orden
    /// está escrito en la especificación (<c>docs/tableros/ejecutivo-proyectos.md</c>, §3) y la
    /// pantalla lo explica en su pie, así que cambiarlo aquí obliga a cambiarlo allá.
    ///
    /// <para>La prioridad del proyecto no entra: salud es cómo va, no cuánto importa.</para>
    /// </summary>
    /// <param name="fechaInicioPlan">Solo para el motivo de «No iniciado».</param>
    /// <param name="fechaFinReal">Solo para el motivo de «Finalizado».</param>
    public static SaludEvaluada Salud(
        EstadoProyecto estado, DateOnly? fechaFinPlan, DateTime? ultimoAvance,
        bool bloqueoVigente, int totalEntregables, int? desviacion,
        DateOnly hoy, DateTime ahoraUtc,
        DateOnly? fechaInicioPlan = null, DateOnly? fechaFinReal = null)
    {
        // 1-3: lo que no se evalúa contra un plan.
        if (estado == EstadoProyecto.Cerrado)
            return new(SaludProyecto.Neutro, "Finalizado",
                fechaFinReal is { } fr ? $"cerrado el {FormatoEjecutivo.DiaMes(fr)}" : "");

        if (estado == EstadoProyecto.Cancelado)
            return new(SaludProyecto.Neutro, "Cancelado", "");

        if (totalEntregables == 0)
            return new(SaludProyecto.Neutro, "Sin dato", "sin entregables cargados");

        // 4-6: rojo.
        if (Atrasado(fechaFinPlan, estado, hoy))
            return new(SaludProyecto.Rojo, "Vencido",
                $"cierre planificado el {FormatoEjecutivo.DiaMes(fechaFinPlan!.Value)}, " +
                FormatoEjecutivo.Hace(hoy.DayNumber - fechaFinPlan.Value.DayNumber));

        if (bloqueoVigente)
            return new(SaludProyecto.Rojo, "Bloqueado", "bloqueo en el último reporte");

        // Un planificado con desviación roja debía haber arrancado: misma etiqueta y mismo rojo, pero
        // el motivo dice que ni siquiera pasó a ejecución (Revisión 1, R4).
        if (desviacion is { } dr && dr <= DesviacionRoja)
            return new(SaludProyecto.Rojo, "Atrasado", estado == EstadoProyecto.Planificado
                ? $"{-dr} pts por detrás del plan · sigue en planificado"
                : $"{-dr} pts por detrás del plan");

        // 7-10: ámbar.
        if (desviacion is { } da && da <= DesviacionAmbar)
            return new(SaludProyecto.Ambar, "En riesgo", $"{-da} pts por detrás del plan");

        if (SinReportar(estado, ultimoAvance, CorteSinReporte(ahoraUtc)))
            return new(SaludProyecto.Ambar, "Sin reportar",
                ultimoAvance is { } u ? FormatoEjecutivo.Hace((int)(ahoraUtc - u).TotalDays) : "nunca");

        if (estado == EstadoProyecto.Suspendido)
            return new(SaludProyecto.Ambar, "Suspendido", "");

        if (estado == EstadoProyecto.EnEjecucion && (fechaFinPlan is null || desviacion is null))
            return new(SaludProyecto.Ambar, "Sin línea base",
                fechaFinPlan is null ? "sin fecha de cierre comprometida" : "sin fechas en las actividades");

        // 11: planificado. Si trae avance es incoherencia de datos, y el tablero no la maquilla.
        if (estado == EstadoProyecto.Planificado)
            return new(SaludProyecto.Neutro, "No iniciado",
                fechaInicioPlan is { } fi ? $"inicio planificado el {FormatoEjecutivo.DiaMes(fi)}" : "sin fecha de inicio");

        // 12: en plan.
        return new(SaludProyecto.Verde, "En plan", desviacion switch
        {
            null       => "sin desviación medible",
            > 0        => FormatoEjecutivo.Puntos(desviacion.Value),
            0          => "en el plan",
            _          => $"{FormatoEjecutivo.Puntos(desviacion.Value)}, dentro del margen"
        });
    }

    /// <summary>Orden de los niveles para contar y ordenar: lo que exige atención primero.</summary>
    public static int Orden(SaludProyecto nivel) => nivel switch
    {
        SaludProyecto.Rojo   => 0,
        SaludProyecto.Ambar  => 1,
        SaludProyecto.Verde  => 2,
        _                    => 3
    };
}

/// <summary>Nivel del semáforo de salud del tablero ejecutivo. Neutro = no se mide contra un plan
/// (cerrado, cancelado, no iniciado, sin estructura).</summary>
public enum SaludProyecto { Rojo, Ambar, Verde, Neutro }

/// <summary>Resultado del semáforo: el nivel, la etiqueta que lo acompaña siempre (el color nunca
/// va solo) y el motivo corto que lo explica.</summary>
public sealed record SaludEvaluada(SaludProyecto Nivel, string Etiqueta, string Motivo);

/// <summary>
/// Formatos del tablero ejecutivo (es-HN). En un solo lugar para que las reglas de salud, que
/// escriben motivos, y la vista, que escribe cifras, hablen igual.
/// </summary>
public static class FormatoEjecutivo
{
    public static readonly System.Globalization.CultureInfo Cultura =
        System.Globalization.CultureInfo.GetCultureInfo("es-HN");

    /// <summary>Signo menos tipográfico (U+2212), no el guion: en una cifra se lee mejor.</summary>
    public const string Menos = "−";

    /// <summary>«5 de octubre».</summary>
    public static string DiaMes(DateOnly f) => f.ToString("d 'de' MMMM", Cultura);

    /// <summary>«19 de octubre de 2026».</summary>
    public static string FechaLarga(DateTime f) => f.ToString("d 'de' MMMM 'de' yyyy", Cultura);

    /// <summary>«19 de octubre de 2026, 14:30».</summary>
    public static string FechaHora(DateTime f) => f.ToString("d 'de' MMMM 'de' yyyy', 'HH:mm", Cultura);

    /// <summary>«hoy», «hace 1 día», «hace N días».</summary>
    public static string Hace(int dias) => dias switch
    {
        <= 0 => "hoy",
        1    => "hace 1 día",
        _    => $"hace {dias} días"
    };

    /// <summary>Puntos con signo explícito: «−15 pts», «+4 pts», «0 pts».</summary>
    public static string Puntos(int puntos) => puntos switch
    {
        > 0 => $"+{puntos} pts",
        < 0 => $"{Menos}{-puntos} pts",
        _   => "0 pts"
    };

    /// <summary>Puntos con un decimal y signo explícito: «−7.7 pts».</summary>
    public static string Puntos(decimal puntos) => puntos switch
    {
        > 0 => $"+{puntos.ToString("0.0", Cultura)} pts",
        < 0 => $"{Menos}{(-puntos).ToString("0.0", Cultura)} pts",
        _   => "0 pts"
    };

    /// <summary>Porcentaje con un decimal, sin el símbolo: «53.3».</summary>
    public static string Decimal1(decimal valor) => valor.ToString("0.0", Cultura);
}
