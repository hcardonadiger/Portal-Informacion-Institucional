namespace Diger.TramitesEstado.Domain.Entities;

public sealed class ComentarioCompromiso : BaseEntity
{
    public int AcuerdoReunionId { get; set; }
    public AcuerdoReunion Acuerdo { get; set; } = default!;

    public string? Comentario { get; set; }
    public string? ArchivoNombre { get; set; }
    public string? ArchivoUrl { get; set; }
    public long? ArchivoTamano { get; set; }

    public string CreadoPor { get; set; } = default!;
    public string? CreadoPorRol { get; set; }
    public DateTime CreadoEl { get; set; } = DateTime.UtcNow;
}
