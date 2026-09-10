namespace Diger.TramitesEstado.Application.Reuniones.Common;

/// <summary>Referencia liviana a una reunión (para listados y selectores de hilo).</summary>
public sealed record HiloMiembroRefDto(int Id, string Titulo, DateOnly? Fecha, string? Institucion);

/// <summary>Hilo de una reunión: su HiloId y las OTRAS reuniones enlazadas (excluye la propia).</summary>
public sealed record HiloDeReunionDto(Guid? HiloId, IReadOnlyList<HiloMiembroRefDto> Otras);

/// <summary>Acuerdo/compromiso (acción o tarea) dentro de la vista de hilo.</summary>
public sealed record HiloAcuerdoDto(
    int Id, string Compromiso, string? Responsable, DateOnly? Plazo,
    EstadoCompromiso Estado, DateOnly? FechaCumplimiento, string? NotaSeguimiento);

/// <summary>Una reunión del hilo con sus acuerdos (para la línea de tiempo).</summary>
public sealed record HiloReunionDto(
    int Id, string Titulo, DateOnly? Fecha, string? Institucion, string? Tipo,
    IReadOnlyList<HiloAcuerdoDto> Acuerdos);

/// <summary>Vista completa de un hilo: reuniones ordenadas cronológicamente con sus acuerdos.</summary>
public sealed record HiloDetalleDto(Guid HiloId, IReadOnlyList<HiloReunionDto> Reuniones);
