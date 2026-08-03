namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Bitácora de seguimiento del expediente: en qué punto está el trabajo con la
/// institución. Es un registro acumulativo — cada nota queda con su fecha y su
/// autor, no se sobrescribe. Tabla independiente (no navegación del agregado
/// Expediente) para no arrastrarla en el reemplazo en bloque de los hijos del editor.
/// </summary>
public sealed class NotaSeguimientoExpediente : BaseEntity
{
    public int ExpedienteId { get; private set; }
    public string Texto { get; private set; } = default!;
    public string CreadoPor { get; private set; } = default!;
    public DateTime CreadoEl { get; private set; }

    private NotaSeguimientoExpediente() { }   // EF

    public static NotaSeguimientoExpediente Crear(int expedienteId, string texto, string creadoPor)
    {
        if (expedienteId <= 0)
            throw new DomainException("La nota debe pertenecer a un expediente.");

        var limpio = (texto ?? "").Trim();
        if (limpio.Length == 0)
            throw new DomainException("La nota no puede estar vacía.");
        if (limpio.Length > MaxTexto)
            throw new DomainException($"La nota no puede superar los {MaxTexto} caracteres.");

        return new NotaSeguimientoExpediente
        {
            ExpedienteId = expedienteId,
            Texto = limpio,
            CreadoPor = string.IsNullOrWhiteSpace(creadoPor) ? "—" : creadoPor.Trim(),
            CreadoEl = DateTime.UtcNow,
        };
    }

    public const int MaxTexto = 1000;
}
