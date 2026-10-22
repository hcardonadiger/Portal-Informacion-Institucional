namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Un aviso que tiene que salir por correo. Los avisos no mandan correo en el acto: se anotan acá y
/// el despachador junta lo de cada persona en un solo correo al final de cada revisión. En la
/// entrega 2, las reglas de correo deciden aquí cuándo sale.
/// </summary>
public sealed class CorreoPendiente : BaseEntity
{
    public Guid                  DestinatarioId { get; private set; }
    public ModuloNotificacion    Modulo         { get; private set; }
    public TipoNotificacion      Tipo           { get; private set; }
    public string                Titulo         { get; private set; } = "";
    public string?               Url            { get; private set; }
    public string                Clave          { get; private set; } = "";
    public DateTime              CreadoUtc      { get; private set; }
    public EstadoCorreoPendiente Estado         { get; private set; }
    public int                   Intentos       { get; private set; }
    public string?               UltimoError    { get; private set; }
    public DateTime?             EnviadoUtc     { get; private set; }

    private CorreoPendiente() { }

    public static CorreoPendiente Crear(Guid destinatarioId, ModuloNotificacion modulo, TipoNotificacion tipo,
                                        string titulo, string? url, string clave, DateTime creadoUtc) => new()
    {
        DestinatarioId = destinatarioId,
        Modulo         = modulo,
        Tipo           = tipo,
        Titulo         = Notificacion.Recortar(titulo, Notificacion.LargoTitulo),
        Url            = url,
        Clave          = Notificacion.Recortar(clave, NotificacionElemento.LargoClave),
        CreadoUtc      = creadoUtc,
        Estado         = EstadoCorreoPendiente.Pendiente,
    };

    public void MarcarEnviado(DateTime utc)
    {
        Estado      = EstadoCorreoPendiente.Enviado;
        EnviadoUtc  = utc;
        UltimoError = null;
    }

    public void RegistrarFallo(string error, int maxIntentos)
    {
        Intentos++;
        UltimoError = Notificacion.Recortar(error, 500);
        if (Intentos >= maxIntentos) Estado = EstadoCorreoPendiente.Fallido;
    }

    /// <summary>No tiene sentido reintentar (destinatario sin correo o inactivo).</summary>
    public void Descartar(string motivo)
    {
        Estado      = EstadoCorreoPendiente.Fallido;
        UltimoError = Notificacion.Recortar(motivo, 500);
    }
}
