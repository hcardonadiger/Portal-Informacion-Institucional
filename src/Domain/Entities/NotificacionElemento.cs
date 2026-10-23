namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Una cosa dentro de un aviso: el compromiso, el ticket, la etapa. Un aviso agrupado tiene varios.
/// <see cref="Clave"/> identifica al elemento —por defecto su URL— y es contra lo que se mide «ya se
/// avisó hoy»: con avisos agrupados, la URL del aviso dejó de servir para eso.
/// </summary>
public sealed class NotificacionElemento : BaseEntity
{
    public const int LargoClave = 500;

    public int      NotificacionId { get; private set; }
    public string   Clave          { get; private set; } = "";
    public string   Titulo         { get; private set; } = "";
    public string?  Url            { get; private set; }
    public DateTime FechaUtc       { get; private set; }

    private NotificacionElemento() { }

    internal static NotificacionElemento Crear(string clave, string titulo, string? url, DateTime fechaUtc) => new()
    {
        Clave    = Notificacion.Recortar(clave, LargoClave),
        Titulo   = Notificacion.Recortar(titulo, Notificacion.LargoTitulo),
        Url      = url,
        FechaUtc = fechaUtc,
    };

    /// <summary>
    /// El elemento volvió a avisarse: toma la fecha de ahora, para que «ya se avisó hoy» lo encuentre
    /// en las revisiones siguientes del día aunque el aviso siga sin leer desde ayer.
    /// </summary>
    internal void Tocar(DateTime fechaUtc) => FechaUtc = fechaUtc;
}
