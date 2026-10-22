using System.Net;
using System.Net.Mail;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Infrastructure.Email;

/// <summary>
/// Envío de correo por SMTP.
///
/// <para><b>El registro nunca lleva el contenido del correo</b>: ni el cuerpo, ni los enlaces, ni
/// los tokens que viajan en ellos (el de recuperación de contraseña, por ejemplo), ni la dirección
/// de destino. Antes, cada falla —y cada envío simulado sin servidor— escribía el cuerpo completo,
/// y en IIS eso terminaba en el Visor de eventos (corregido el 2026-10-22). Ahora se registra el
/// tipo de correo, un identificador del envío para cruzarlo con otros registros, y el error.</para>
/// </summary>
public sealed class SmtpEmailService(
    IOptions<SmtpSettings> settingsOpt,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    private readonly SmtpSettings _settings = settingsOpt.Value;

    public async Task SendEmailAsync(string to, string subject, string bodyHtml, TipoCorreo tipo, CancellationToken ct = default)
    {
        var envio = Guid.NewGuid().ToString("N")[..8];

        // Sin credenciales configuradas (ambiente local) no se intenta: se deja constancia de que
        // el correo no salió, sin su contenido. Para ver los correos en local, un servidor SMTP
        // de prueba como smtp4dev.
        if (string.IsNullOrWhiteSpace(_settings.Host) ||
            string.IsNullOrWhiteSpace(_settings.Username) ||
            _settings.Username.Contains("ejemplo", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Correo {Tipo} (envío {Envio}) no enviado: el servidor de correo (sección Smtp) no está configurado.",
                tipo, envio);
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
            logger.LogInformation("Correo {Tipo} (envío {Envio}) enviado.", tipo, envio);
        }
        catch (Exception ex)
        {
            // La excepción describe la falla de conexión o la respuesta del servidor; no lleva el
            // cuerpo del mensaje.
            logger.LogError(ex, "Correo {Tipo} (envío {Envio}) no enviado.", tipo, envio);
        }
    }
}
