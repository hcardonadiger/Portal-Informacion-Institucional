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
}
