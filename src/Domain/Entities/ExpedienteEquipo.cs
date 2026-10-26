namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Integrante del equipo institucional del expediente: la gente de la institución a la que
/// DIGER le consulta. <see cref="Prioridad"/> es el orden en que se le escribe —la 1 atiende
/// las consultas y, si no está disponible, se pasa a la siguiente—. La 1 es además el
/// contacto principal: el expediente copia sus datos a <c>Contacto*</c>, que es lo que leen
/// el resumen, los recordatorios y los informes.
/// </summary>
/// <remarks>No es <see cref="MiembroEquipo"/>, que es el equipo de un <see cref="Levantamiento"/>
/// importado del portal demo y no tiene pantalla.</remarks>
public sealed class MiembroEquipoExpediente : BaseEntity
{
    public int     ExpedienteId { get; set; }
    public int     Prioridad    { get; set; }
    public string  Nombre       { get; set; } = default!;
    public string? Cargo        { get; set; }
    public string? Correo       { get; set; }
    public string? Telefono     { get; set; }
}
