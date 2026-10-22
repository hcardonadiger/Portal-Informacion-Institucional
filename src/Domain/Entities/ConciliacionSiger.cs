namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Decisión humana sobre el enlace de un trámite de expediente con el inventario SIGER.
/// </summary>
/// <remarks>
/// <para>
/// Existe para que la bandeja de conciliación recuerde lo ya revisado. Sin esto, un trámite
/// descartado a mano vuelve a proponerse en cada pasada y la bandeja deja de ser usable.
/// Hay a lo sumo una decisión vigente por trámite; al desenlazar se borra la fila y el trámite
/// regresa a pendientes.
/// </para>
/// <para>
/// <b>Se identifica por <see cref="ClaveTramite"/> y no por el Id del trámite.</b> Hasta el
/// 24-08-2026 esta tabla apuntaba a <c>ExpedienteTramite.Id</c> con borrado en cascada, y como
/// guardar un expediente borra y reinserta todos sus hijos, cada guardado se llevaba por delante
/// las decisiones: un trámite descartado a mano reaparecía en la bandeja al siguiente guardado,
/// justo lo que este registro existe para evitar. Rekeyar sobre <c>(ExpedienteId, TramiteIndex)</c>
/// tampoco servía —el índice se renumera al quitar un trámite del medio o al reordenar— y habría
/// cambiado «la decisión se pierde» por «la decisión queda pegada al trámite equivocado», que es
/// peor: dato callado y falso en vez de dato callado y ausente.
/// </para>
/// <para>
/// <see cref="ExpedienteId"/> se guarda aparte para que borrar un expediente se lleve sus
/// decisiones. Si en cambio desaparece un trámite suelto, su decisión queda huérfana: la bandeja
/// simplemente no la encuentra, que es un fallo inofensivo comparado con el anterior.
/// </para>
/// </remarks>
public sealed class ConciliacionSiger : BaseAuditableEntity
{
    /// <summary>Clave estable del trámite de expediente. Ver <c>ExpedienteTramite.ClaveEstable</c>.</summary>
    public Guid ClaveTramite { get; set; }

    /// <summary>Expediente al que pertenece el trámite. Solo para poder limpiar en cascada.</summary>
    public int ExpedienteId { get; set; }

    /// <summary>Ficha SIGER elegida. Null cuando la decisión fue descartar o proponer ficha nueva.</summary>
    public int? TramiteSigerId { get; set; }

    public DecisionConciliacion Decision { get; set; }

    public string? Nota { get; set; }
}
