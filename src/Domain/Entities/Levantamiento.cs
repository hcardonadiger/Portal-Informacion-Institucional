namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>Visita/levantamiento de campo realizado por DIGER a una institución.</summary>
public sealed class Levantamiento : BaseAuditableEntity
{
    public string    Institucion            { get; set; } = default!;
    public string    Encargado              { get; set; } = default!;
    public string?   Correo                 { get; set; }
    public string?   Celular                { get; set; }
    public EstadoLevantamientoExp Estado   { get; set; }
    public string?   ObsEstado              { get; set; }
    public bool      MigradaSOL             { get; set; }
    public bool      Limitante              { get; set; }
    public string?   LimitanteObs           { get; set; }
    public bool      Personal               { get; set; }
    public string?   PersonalObs            { get; set; }
    public bool      RequiereAcompanamiento { get; set; }
    public bool      Habilidad              { get; set; }
    public string?   HabilidadObs           { get; set; }
    public string?   ObsGenerales           { get; set; }

    public ICollection<TramiteChecklist>  Tramites   { get; set; } = [];
    public ICollection<MiembroEquipo>     Equipo     { get; set; } = [];
    public ICollection<DocumentoAdjunto>  Documentos { get; set; } = [];
}

public sealed class TramiteChecklist : BaseEntity
{
    public int     LevantamientoId  { get; set; }
    public string  NombreTramite    { get; set; } = default!;
    public int     Orden            { get; set; }
    public bool    ActaFirmada      { get; set; }
    public bool    RequiereMejoras  { get; set; }
    public bool    TieneInstructivo { get; set; }
    public bool    Socializado      { get; set; }
    public string? Observaciones    { get; set; }
}

public sealed class MiembroEquipo : BaseEntity
{
    public int     LevantamientoId { get; set; }
    public string  Funcion         { get; set; } = default!;
    public string  Nombre          { get; set; } = default!;
    public string? Contacto        { get; set; }
    public int     Orden           { get; set; }
}

public sealed class DocumentoAdjunto : BaseEntity
{
    public int       LevantamientoId { get; set; }
    public string    Nombre          { get; set; } = default!;
    public string?   Tipo            { get; set; }
    public string    Url             { get; set; } = default!;
    public DateOnly? FechaDocumento  { get; set; }
    public DateTime  FechaRegistro   { get; set; }
}
