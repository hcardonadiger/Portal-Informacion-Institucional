using Diger.TramitesEstado.Domain.Common;

namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Configuración de las notificaciones que se administra desde Panel de control → Notificaciones.
/// Es una sola fila (<see cref="IdUnico"/>). Mientras <see cref="SmtpConfigurado"/> sea false, el
/// correo sale con la sección <c>Smtp</c> de appsettings: el panel toma el control en el primer
/// guardado de su bloque de correo.
/// </summary>
public sealed class AjustesNotificaciones : BaseAuditableEntity
{
    public const int IdUnico             = 1;
    public const int MinimoMinutos       = 5;
    public const int MaximoMinutos       = 24 * 60;
    public const int IntervaloPorDefecto = 240;

    public int       IntervaloMinutos    { get; private set; }
    public bool      SmtpConfigurado     { get; private set; }
    public string?   SmtpHost            { get; private set; }
    public int       SmtpPuerto          { get; private set; } = 587;
    public bool      SmtpSsl             { get; private set; } = true;
    public string?   SmtpUsuario         { get; private set; }
    public string?   SmtpRemitente       { get; private set; }
    public string?   SmtpNombreRemitente { get; private set; }

    /// <summary>Cifrada con Data Protection (propósito <c>PanelControl.Notificaciones.Smtp</c>).
    /// Nunca en claro, nunca hacia el navegador.</summary>
    public string?   SmtpPasswordCifrada { get; private set; }

    public DateTime? UltimaRevisionUtc   { get; private set; }

    private AjustesNotificaciones() { }

    public static AjustesNotificaciones CrearPorDefecto(int intervaloMinutos = IntervaloPorDefecto)
    {
        var a = new AjustesNotificaciones { Id = IdUnico };
        a.CambiarIntervalo(Math.Clamp(intervaloMinutos, MinimoMinutos, MaximoMinutos));
        return a;
    }

    public void CambiarIntervalo(int minutos)
    {
        if (minutos < MinimoMinutos || minutos > MaximoMinutos)
            throw new DomainException("La revisión automática tiene que ser de entre 5 minutos y 24 horas.");
        IntervaloMinutos = minutos;
    }

    public void ConfigurarSmtp(string host, int puerto, bool ssl, string usuario, string remitente, string? nombreRemitente)
    {
        if (string.IsNullOrWhiteSpace(host))      throw new DomainException("Falta el servidor de correo.");
        if (puerto is < 1 or > 65535)             throw new DomainException("El puerto tiene que estar entre 1 y 65535.");
        if (string.IsNullOrWhiteSpace(usuario))   throw new DomainException("Falta el usuario del correo.");
        if (string.IsNullOrWhiteSpace(remitente)) throw new DomainException("Falta el correo remitente.");

        SmtpHost            = host.Trim();
        SmtpPuerto          = puerto;
        SmtpSsl             = ssl;
        SmtpUsuario         = usuario.Trim();
        SmtpRemitente       = remitente.Trim();
        SmtpNombreRemitente = string.IsNullOrWhiteSpace(nombreRemitente) ? null : nombreRemitente.Trim();
        SmtpConfigurado     = true;
    }

    public void FijarPasswordCifrada(string? cifrada) => SmtpPasswordCifrada = cifrada;

    public void MarcarRevision(DateTime utc) => UltimaRevisionUtc = utc;
}
