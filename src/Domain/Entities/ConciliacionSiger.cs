namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Decisión humana sobre el enlace de un trámite de expediente con el inventario SIGER.
/// </summary>
/// <remarks>
/// Existe para que la bandeja de conciliación recuerde lo ya revisado. Sin esto, un trámite
/// descartado a mano vuelve a proponerse en cada pasada y la bandeja deja de ser usable.
/// Hay a lo sumo una decisión vigente por trámite (índice único en <see cref="ExpedienteTramiteId"/>);
/// al desenlazar se borra la fila y el trámite regresa a pendientes.
/// </remarks>
public sealed class ConciliacionSiger : BaseAuditableEntity
{
    public int ExpedienteTramiteId { get; set; }

    /// <summary>Ficha SIGER elegida. Null cuando la decisión fue descartar o proponer ficha nueva.</summary>
    public int? TramiteSigerId { get; set; }

    public DecisionConciliacion Decision { get; set; }

    public string? Nota { get; set; }
}
