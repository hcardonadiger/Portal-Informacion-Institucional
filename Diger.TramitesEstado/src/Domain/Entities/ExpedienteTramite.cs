namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>Trámite a modelar dentro de un expediente (ficha del trámite).</summary>
public sealed class ExpedienteTramite : BaseEntity
{
    public int    ExpedienteId  { get; set; }
    public int    TramiteIndex  { get; set; } // posición 0-based dentro del expediente

    public string  NombreTramite   { get; set; } = default!;
    public string? NombreCorto     { get; set; }
    public string? AreaResponsable { get; set; }

    // Ficha
    public string? Modalidad   { get; set; }
    public string? PlazoLegal  { get; set; }
    public string? Tercero     { get; set; }
    public string? TiempoReal  { get; set; }
    public string? MetodoPago  { get; set; }
    public string? PagoBanco   { get; set; }
    public string? PagoCuenta  { get; set; }
    public string? TgrInst     { get; set; }
    public string? TgrRubro    { get; set; }
    public string? TgrMonto    { get; set; }
    public string? DocEntregado { get; set; }
    public string? Objetivo    { get; set; }
    public string? Alcance     { get; set; }
    public string? AlcanceObs  { get; set; }
    public string? Descripcion { get; set; }
    public string? Dirigido    { get; set; }
    public string? Horario     { get; set; }
    public string? Telefono    { get; set; }
    public string? EmailTramite { get; set; }
    public string? SitioWeb    { get; set; }
}

/// <summary>Requisito de un trámite y la acción propuesta en el modelo.</summary>
public sealed class TramiteRequisito : BaseEntity
{
    public int    ExpedienteId { get; set; }
    public int    TramiteIndex { get; set; }
    public int    Orden        { get; set; }
    public string Requisito    { get; set; } = default!;
    public string? Obs         { get; set; }
    public AccionRequisito? Accion { get; set; }
    public string? Justificacion  { get; set; }
}

/// <summary>Nodo del constructor de flujos (actual o propuesto) de un trámite.</summary>
public sealed class FlujoNodo : BaseEntity
{
    public int           ExpedienteId { get; set; }
    public int           TramiteIndex { get; set; }
    public FaseFlujo     Fase         { get; set; }
    public int           Orden        { get; set; }
    public TipoNodoFlujo Tipo         { get; set; }
    public string?       Titulo       { get; set; }
    public string?       Area         { get; set; }
    public string?       Tiempo       { get; set; }
    public string?       DocEmitido   { get; set; }
    public string?       Obs          { get; set; }
    public string?       RetornoA     { get; set; }
}
