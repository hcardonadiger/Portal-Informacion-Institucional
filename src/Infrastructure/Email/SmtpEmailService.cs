using System.Net;
using System.Net.Mail;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Infrastructure.Email;

public sealed class SmtpEmailService(
    IOptions<SmtpSettings> settingsOpt,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    private readonly SmtpSettings _settings = settingsOpt.Value;

    public async Task SendEmailAsync(string to, string subject, string bodyHtml, CancellationToken ct = default)
    {
        // En ambiente local sin credenciales configuradas, simular envío mediante ILogger
        if (string.IsNullOrWhiteSpace(_settings.Host) ||
            string.IsNullOrWhiteSpace(_settings.Username) ||
            _settings.Username.Contains("ejemplo", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("[SMTP MOCK/DEV] Correo a {To} | Asunto: {Subject}\nCuerpo:\n{Body}", to, subject, bodyHtml);
            return;
        }

        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(_settings.FromEmail, _settings.FromName);
            message.To.Add(new MailAddress(to));
            message.Subject = subject;
            message.Body = bodyHtml;
            message.IsBodyHtml = true;

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(_settings.Username, _settings.Password)
            };

            await client.SendMailAsync(message, ct);
            logger.LogInformation("Correo de recuperación enviado exitosamente a {To}", to);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al enviar correo SMTP a {To}. Se registra el contenido en el log para desarrollo:\n{Body}", to, bodyHtml);
        }
    }
}
