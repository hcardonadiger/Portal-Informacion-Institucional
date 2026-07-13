namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>Fundamento legal que sustenta el/los trámite(s).</summary>
public sealed class FundamentoLegal : BaseEntity
{
    public int     ExpedienteId { get; set; }
    public int     Orden        { get; set; }
    public string  Instrumento  { get; set; } = default!;
    public string? Articulos    { get; set; }
    public string? Obs          { get; set; }

    /// <summary>Plantilla de la que se copió (null = escrito a mano para este expediente).</summary>
    public int?  PlantillaOrigenId { get; set; }
    /// <summary>true = ya no se sincroniza con la plantilla; queda fijo y editable para este expediente.</summary>
    public bool  EsPersonalizado   { get; set; }
}

/// <summary>Documento solicitado a la institución.</summary>
public sealed class DocumentoSolicitado : BaseEntity
{
    public int       ExpedienteId { get; set; }
    public int       Orden        { get; set; }
    public string    Nombre       { get; set; } = default!;
    public string?   Tipo         { get; set; }
    public bool      Recibido     { get; set; }
    public DateOnly? Fecha        { get; set; }
    public string?   Url          { get; set; }
}

/// <summary>Documento interno que maneja la institución para el trámite.</summary>
public sealed class DocumentoInterno : BaseEntity
{
    public int     ExpedienteId { get; set; }
    public int     Orden        { get; set; }
    public string  Documento    { get; set; } = default!;
    public string? Area         { get; set; }
    public string? Obs          { get; set; }
}

/// <summary>Estado de avance de una sección del expediente.</summary>
public sealed class ExpedienteSeccionEstado : BaseEntity
{
    public int           ExpedienteId { get; set; }
    public int           Seccion      { get; set; } // índice de sección 0..6
    public EstadoSeccion Estado       { get; set; } = EstadoSeccion.Pendiente;
    public string?       Nota         { get; set; }
}
