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

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string bodyHtml, TipoCorreo tipo, CancellationToken ct = default);
}
