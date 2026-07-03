namespace Diger.TramitesEstado.Application.Reuniones.Common;

// Clases mutables para enlace de modelo (Razor) y transporte al comando.
public sealed class AsistenteInput
{
    public string  Nombre       { get; set; } = string.Empty;
    public string? Cargo        { get; set; }
    public string? Institucion  { get; set; }
    public string? Departamento { get; set; }
    public string? Correo       { get; set; }
    public string? Telefono     { get; set; }

    // Trazabilidad del auto-registro (se conserva al reemplazar los hijos en bloque).
    public bool      AutoRegistro { get; set; }
    public DateTime? RegistradoEl { get; set; }
}

public sealed class AcuerdoInput
{
    public string    Compromiso  { get; set; } = string.Empty;
    public string?   Responsable { get; set; }
    public DateOnly? Plazo       { get; set; }

    // ── Seguimiento (se transporta de ida y vuelta para conservarlo al reemplazar los hijos en bloque) ──
    public EstadoCompromiso Estado            { get; set; } = EstadoCompromiso.Pendiente;
    public DateOnly?        FechaCumplimiento { get; set; }
    public string?          NotaSeguimiento   { get; set; }
}

public sealed class ReunionFormDto
{
    public string    Titulo    { get; set; } = string.Empty;
    public DateOnly? Fecha     { get; set; }
    public string?   Hora      { get; set; }
    public string?   Duracion  { get; set; }
    public string?   Modalidad { get; set; }
    public string?   Lugar     { get; set; }
    public int?      InstitucionId { get; set; }
    public string?   Tipo      { get; set; }
    public bool      EsCapacitacionPlataforma { get; set; }
    public VisibilidadReunion Visibilidad { get; set; } = VisibilidadReunion.Publica;

    public string? ObjetivoAgenda { get; set; }
    public string? Desarrollo     { get; set; }

    public string? Tema        { get; set; }
    public string? ObjetivoCap  { get; set; }
    public string? Contenido    { get; set; }

    public string? EpNombre { get; set; }
    public string? EpCargo  { get; set; }
    public string? EpCorreo { get; set; }
    public string? EpTel    { get; set; }

    public string? FacNombre { get; set; }
    public string? FacCargo  { get; set; }
    public string? FacCorreo { get; set; }

    public int?    Convocados    { get; set; }
    public int?    NumAsistentes { get; set; }
    public int?    PctAsistencia { get; set; }
    public string? Satisfaccion  { get; set; }
    public string? Compromisos   { get; set; }

    public string? ValDiger     { get; set; }
    public string? ValInst      { get; set; }
    public string? DocsRecursos { get; set; }
    public string? Foto1Url  { get; set; }
    public string? Foto1Desc { get; set; }
    public string? Foto2Url  { get; set; }
    public string? Foto2Desc { get; set; }
}
