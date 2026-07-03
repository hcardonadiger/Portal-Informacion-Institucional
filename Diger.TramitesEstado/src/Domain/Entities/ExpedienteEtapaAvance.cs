namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Avance del seguimiento de la metodología de digitalización, por <b>trámite</b> del expediente.
/// La clave estable del trámite es <c>(ExpedienteId, TramiteIndex)</c> (misma que usan Requisitos/Flujos),
/// por lo que sobrevive al reemplazo en bloque de los hijos del editor.
/// Fila dispersa: <c>SubId = "3.4"</c> con <c>Estado</c> 0/1/2 (Pendiente/En proceso/Completado);
/// o <c>SubId = "APLICA:IV"</c> con <c>Estado</c> 0/1 para marcar si una etapa especial aplica.
/// Tabla independiente (no navegación del agregado Expediente).
/// </summary>
public sealed class ExpedienteEtapaAvance : BaseEntity
{
    public int    ExpedienteId { get; set; }
    public int    TramiteIndex { get; set; } // trámite dentro del expediente (0-based)
    public string SubId        { get; set; } = default!;
    public int    Estado       { get; set; }
}
