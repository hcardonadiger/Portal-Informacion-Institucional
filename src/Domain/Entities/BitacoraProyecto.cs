namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Qué clase de cambio registra la entrada.
///
/// <para>No confundir con <see cref="AvanceProyecto"/>: eso es lo que <b>reporta</b> el
/// responsable sobre la ejecución. Esto es auditoría de quién tocó la ficha, y se escribe sola.</para>
/// </summary>
public enum TipoEventoProyecto
{
    CambioEstado,
    ModificacionFicha,
    ModificacionEstructura,
    CorreccionBitacora,
    Riesgo,
    Interesado,

    /// <summary>Alta, corrección, versión nueva o archivado de un documento del repositorio.
    /// <para>Agregar un miembro es seguro —la columna guarda texto—; renombrar uno obliga a
    /// reescribirla, como ya pasó con ModificacionEstructura.</para></summary>
    Documentacion
}

/// <summary>
/// Bitácora de auditoría del proyecto: cambios de estado, de la ficha y de la estructura de
/// entregables y actividades.
/// Registro acumulativo — cada entrada queda con su fecha y su autor, no se sobrescribe.
///
/// <para>Tabla independiente, <b>no</b> navegación del agregado <see cref="Proyecto"/>, por la misma
/// razón que <see cref="AvanceProyecto"/>: lo que cuelga del agregado se arrastra en sus operaciones
/// de colección. Mismo patrón y misma razón que <see cref="BitacoraExpediente"/>.</para>
///
/// <para>Cubre el hueco que quedó al hacer editable la bitácora de ejecución: si los avances se
/// pueden corregir y la ficha se puede reescribir, tiene que haber algo que no se pueda tocar.
/// Esto es eso.</para>
/// </summary>
public sealed class BitacoraProyecto : BaseEntity
{
    public int                ProyectoId { get; private set; }
    public TipoEventoProyecto Tipo       { get; private set; }
    public string             Detalle    { get; private set; } = default!;
    public string             Actor      { get; private set; } = default!;
    public DateTime           Fecha      { get; private set; }

    private BitacoraProyecto() { }   // EF

    public static BitacoraProyecto Crear(int proyectoId, TipoEventoProyecto tipo, string detalle, string actor)
    {
        if (proyectoId <= 0)
            throw new DomainException("La entrada de bitácora debe pertenecer a un proyecto.");

        var limpio = (detalle ?? "").Trim();
        if (limpio.Length == 0)
            throw new DomainException("El detalle de la bitácora no puede estar vacío.");

        return new BitacoraProyecto
        {
            ProyectoId = proyectoId,
            Tipo       = tipo,
            Detalle    = limpio.Length > MaxDetalle ? limpio[..MaxDetalle] : limpio,
            Actor      = string.IsNullOrWhiteSpace(actor) ? "—" : actor.Trim(),
            Fecha      = DateTime.UtcNow
        };
    }

    /// <summary>El detalle se arma solo, resumiendo cambios; se recorta en vez de reventar,
    /// porque perder una entrada de auditoría es peor que perder el final de una frase.</summary>
    public const int MaxDetalle = 1000;
}
