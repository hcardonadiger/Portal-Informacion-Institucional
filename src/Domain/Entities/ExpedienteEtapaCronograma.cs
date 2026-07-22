namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Fechas comprometidas por etapa de la metodología de digitalización, por trámite del expediente.
/// Una fila por (ExpedienteId, TramiteIndex, EtapaNum); ausencia de fila = sin fechas definidas.
/// </summary>
public sealed class ExpedienteEtapaCronograma : BaseEntity
{
    public int       ExpedienteId  { get; set; }
    public int       TramiteIndex  { get; set; }
    public string    EtapaNum     { get; set; } = default!; // "I" – "XI"
    public DateOnly? FechaInicio   { get; set; }
    public DateOnly? FechaFin      { get; set; }
    public DateOnly? FechaRealFin  { get; set; }
    public string?   Responsable   { get; set; }
    public string?   Observacion   { get; set; }
}
