using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Common.Models;
using Diger.TramitesEstado.Application.Notificaciones;
using Microsoft.Extensions.Logging;

namespace Diger.TramitesEstado.Infrastructure.Email;

/// <summary>
/// Envío de correo por SMTP.
///
/// <para><b>El registro nunca lleva el contenido del correo</b>: ni el cuerpo, ni los enlaces, ni
/// los tokens que viajan en ellos (el de recuperación de contraseña, por ejemplo), ni la dirección
/// de destino. Antes, cada falla —y cada envío simulado sin servidor— escribía el cuerpo completo,
/// y en IIS eso terminaba en el Visor de eventos (corregido el 2026-10-22). Ahora se registra el
/// tipo de correo, un identificador del envío para cruzarlo con otros registros, y el error.</para>
///
/// <para>El envío tiene un tiempo máximo (<see cref="SmtpSettings.TimeoutSegundos"/>, 15 s por
/// defecto) para que la página no se quede colgada esperando a un servidor que no responde.</para>
///
/// <para>La configuración se pide en cada envío a <see cref="IAjustesNotificaciones"/>: la que se
/// guardó en Panel de control → Notificaciones o, si nunca se guardó, la sección <c>Smtp</c>.
/// Cambiar la contraseña en el panel aplica al siguiente correo, sin reiniciar.</para>
/// </summary>
public sealed class SmtpEmailService(
    IAjustesNotificaciones ajustes,
    ILogger<SmtpEmailService> logger) : IEmailService, IProbadorCorreo
{
    public async Task<ResultadoCorreo> SendEmailAsync(string to, string subject, string bodyHtml, TipoCorreo tipo, CancellationToken ct = default)
        => (await EnviarAsync(to, subject, bodyHtml, tipo, ct)).Resultado;

    public Task<ResultadoPruebaCorreo> ProbarAsync(string destino, CancellationToken ct = default) =>
        EnviarAsync(destino, $"Correo de prueba — {Marca.Nombre}",
            $"<p>Este es un correo de prueba enviado desde el Panel de control de {PlantillasCorreo.E(Marca.Nombre)}.</p>" +
            "<p>Si lo está leyendo, el correo saliente funciona.</p>",
            TipoCorreo.Prueba, ct);

    private async Task<ResultadoPruebaCorreo> EnviarAsync(string to, string subject, string bodyHtml, TipoCorreo tipo, CancellationToken ct)
    {
        var envio = Guid.NewGuid().ToString("N")[..8];

        // La configuración sale de la base: con la caché fría, leerla puede fallar. Eso tampoco
        // se escapa del envío (este servicio promete no lanzar); solo se deja pasar la cancelación
        // pedida por quien llama.
        SmtpVigente s;
        try
        {
            s = await ajustes.SmtpAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Correo {Tipo} (envío {Envio}) no enviado: no se pudo leer la configuración del correo.",
                tipo, envio);
            return new(ResultadoCorreo.Fallido, "No se pudo leer la configuración del correo.");
        }

        if (s.PasswordIlegible)
        {
            logger.LogWarning(
                "Correo {Tipo} (envío {Envio}) no enviado: la contraseña guardada en el Panel de control no se puede descifrar en este servidor.",
                tipo, envio);
            return new(ResultadoCorreo.SinConfigurar,
                "La contraseña guardada no se puede leer en este servidor: vuelva a escribirla en el Panel de control.");
        }

        // Sin credenciales configuradas (ambiente local) no se intenta: se deja constancia de que
        // el correo no salió, sin su contenido. Para ver los correos en local, un servidor SMTP
        // de prueba como smtp4dev.
        if (!s.EstaConfigurado)
        {
            logger.LogWarning(
                "Correo {Tipo} (envío {Envio}) no enviado: el servidor de correo no está configurado (Panel de control o sección Smtp).",
                tipo, envio);
            return new(ResultadoCorreo.SinConfigurar, "El servidor de correo no está configurado.");
        }

        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(s.Remitente, s.NombreRemitente);
            message.To.Add(new MailAddress(to));
            message.Subject = subject;
            message.Body = bodyHtml;
            message.IsBodyHtml = true;

            var limite = TimeSpan.FromSeconds(Math.Max(1, s.TimeoutSegundos));
            using var client = new SmtpClient(s.Host, s.Puerto)
            {
                EnableSsl   = s.Ssl,
                Credentials = new NetworkCredential(s.Usuario, s.Password),
                Timeout     = (int)limite.TotalMilliseconds,
            };

            // SmtpClient.Timeout solo rige el envío sincrónico; el asincrónico se corta con el token.
            using var tiempo = CancellationTokenSource.CreateLinkedTokenSource(ct);
            tiempo.CancelAfter(limite);

            await client.SendMailAsync(message, tiempo.Token);
            logger.LogInformation("Correo {Tipo} (envío {Envio}) enviado.", tipo, envio);
            return new(ResultadoCorreo.Enviado, "El servidor aceptó el correo.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogError(
                "Correo {Tipo} (envío {Envio}) no enviado: el servidor de correo no respondió en {Segundos} s.",
                tipo, envio, s.TimeoutSegundos);
            return new(ResultadoCorreo.Fallido, $"El servidor de correo no respondió en {s.TimeoutSegundos} segundos.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // La excepción describe la falla de conexión o la respuesta del servidor; no lleva el cuerpo.
            logger.LogError(ex, "Correo {Tipo} (envío {Envio}) no enviado.", tipo, envio);
            return new(ResultadoCorreo.Fallido, Explicar(ex));
        }
    }

    /// <summary>Un motivo que el administrador entienda. Va antes el «5.7»: los servidores lo usan
    /// para rechazos de autenticación aunque el código de estado diga otra cosa.</summary>
    internal static string Explicar(Exception ex) => ex switch
    {
        SmtpException se when se.Message.Contains("5.7", StringComparison.Ordinal)
            => "El servidor rechazó el usuario o la contraseña.",
        SmtpException { StatusCode: SmtpStatusCode.MustIssueStartTlsFirst }
            => "El servidor exige conexión segura: active SSL.",
        SmtpException { InnerException: SocketException } or SocketException
            => "No se pudo conectar con el servidor de correo. Revise el servidor y el puerto.",
        SmtpException se
            => $"El servidor de correo respondió: {se.StatusCode}.",
        _   => "No se pudo enviar el correo.",
    };
}
