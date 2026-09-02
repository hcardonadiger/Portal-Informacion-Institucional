namespace Diger.TramitesEstado.Domain.Entities;

public sealed class Notificacion : BaseEntity
{
    public Guid              DestinatarioId { get; private set; }
    public TipoNotificacion  Tipo           { get; private set; }
    public string            Titulo         { get; private set; } = "";
    public string?           Url            { get; private set; }
    public bool              Leida          { get; private set; }
    public DateTime          FechaCreacion  { get; private set; }
    public DateTime?         FechaLectura   { get; private set; }

    private Notificacion() { }

    public static Notificacion Crear(Guid destinatarioId, TipoNotificacion tipo, string titulo, string? url = null)
        => new()
        {
            DestinatarioId = destinatarioId,
            Tipo           = tipo,
            Titulo         = titulo,
            Url            = url,
            Leida          = false,
            FechaCreacion  = DateTime.UtcNow,
        };

    public void MarcarLeida()
    {
        if (Leida) return;
        Leida        = true;
        FechaLectura = DateTime.UtcNow;
    }
}
