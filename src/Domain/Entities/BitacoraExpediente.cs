namespace Diger.TramitesEstado.Domain.Entities;

public enum TipoEventoBitacora { CambioEstado, ModificacionHijos, AvanceMetodologico, Validacion }

/// <summary>
/// Bitácora de auditoría del expediente: cambios de estado, modificaciones en bloque
/// de las colecciones hijas, avance metodológico y validaciones. Registro acumulativo —
/// cada entrada queda con su fecha y su autor, no se sobrescribe. Tabla independiente
/// (no navegación del agregado Expediente), mismo patrón que NotaSeguimientoExpediente.
/// </summary>
public sealed class BitacoraExpediente : BaseEntity
{
    public int ExpedienteId { get; private set; }
    public TipoEventoBitacora Tipo { get; private set; }
    public string Detalle { get; private set; } = default!;
    public string Actor { get; private set; } = default!;
    public DateTime Fecha { get; private set; }

    private BitacoraExpediente() { }   // EF

    public static BitacoraExpediente Crear(int expedienteId, TipoEventoBitacora tipo, string detalle, string actor)
    {
        if (expedienteId <= 0)
            throw new DomainException("La entrada de bitácora debe pertenecer a un expediente.");

        var limpio = (detalle ?? "").Trim();
        if (limpio.Length == 0)
            throw new DomainException("El detalle de la bitácora no puede estar vacío.");

        return new BitacoraExpediente
        {
            ExpedienteId = expedienteId,
            Tipo = tipo,
            Detalle = limpio,
            Actor = string.IsNullOrWhiteSpace(actor) ? "—" : actor.Trim(),
            Fecha = DateTime.UtcNow,
        };
    }
}
