namespace Diger.TramitesEstado.Application.Reuniones.Asistencia;

/// <summary>Datos capturados en el formulario público de auto-registro (mutable para enlace de modelo).</summary>
public sealed class AsistenteAutoInput
{
    public string  Nombre       { get; set; } = string.Empty;
    public string? Institucion  { get; set; }
    public string? Departamento { get; set; }
    public string? Cargo        { get; set; }
    public string? Correo       { get; set; }
    public string? CodigoPais   { get; set; }   // p. ej. "+504"
    public string? Telefono     { get; set; }
}

/// <summary>Vista pública de la reunión para el formulario de auto-registro.</summary>
public sealed record ReunionPublicaDto(
    Guid Token, string Titulo, DateOnly? Fecha, string? Hora, string? Modalidad, string? Lugar,
    string? Institucion, bool RegistroAbierto, int YaRegistrados,
    IReadOnlyList<string> Instituciones);

/// <summary>Gestión de la lista de asistencia (vista del organizador).</summary>
public sealed record AsistenciaAdminDto(
    int ReunionId, string Titulo, Guid Token, bool RegistroAbierto,
    int? Convocados, string? InstitucionId, string? Institucion,
    DateOnly? Fecha, string? Tipo,
    IReadOnlyList<string> InstitucionesNombres, IReadOnlyList<AsistenteVm> Asistentes);

public sealed record AsistenteVm(
    int Id, string Nombre, string? Cargo, string? Institucion, string? Departamento,
    string? Correo, string? Telefono, bool AutoRegistro, DateTime? RegistradoEl);
