namespace Diger.TramitesEstado.Application.Common.Interfaces;

/// <summary>Qué correo se envía. Es lo que queda en el registro de un envío: el cuerpo, los
/// enlaces y los tokens nunca se escriben ahí.</summary>
public enum TipoCorreo
{
    RecuperacionContrasena,
    RecordatorioTicket,
    RecordatorioExpediente,
    RecordatorioReunion,
    RecordatorioCompromiso,
}

/// <summary>Cómo terminó un envío. Lo usa la pantalla del recordatorio para decir la verdad: que el
/// aviso llegó a la campana pero el correo no, en vez de un «enviado exitosamente» que no siempre
/// es cierto. <see cref="Enviado"/> va primero a propósito: es el valor por defecto.</summary>
public enum ResultadoCorreo
{
    Enviado,
    Fallido,
    /// <summary>La instalación no tiene servidor de correo configurado: no se intentó enviar.</summary>
    SinConfigurar,
}

public interface IEmailService
{
    /// <summary>Envía un correo. No lanza si el envío falla: lo registra (sin el contenido) y lo
    /// devuelve como resultado.</summary>
    Task<ResultadoCorreo> SendEmailAsync(string to, string subject, string bodyHtml, TipoCorreo tipo, CancellationToken ct = default);
}
