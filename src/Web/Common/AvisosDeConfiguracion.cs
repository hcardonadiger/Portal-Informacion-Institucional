using Diger.TramitesEstado.Infrastructure.Email;

namespace Diger.TramitesEstado.Web.Common;

/// <summary>
/// Configuración de la instalación que conviene tener pero sin la cual el sistema igual arranca.
/// Se revisa al arrancar: lo que falta se avisa en el registro y el arranque sigue normal.
/// </summary>
public static class AvisosDeConfiguracion
{
    public static void Revisar(IConfiguration configuracion, ILogger logger)
    {
        // Sin la dirección pública del portal no sale el correo de recuperación de contraseña y los
        // enlaces de los recordatorios salen relativos, que no se abren desde el programa de correo.
        var correo = configuracion.GetSection(CorreoOptions.Seccion).Get<CorreoOptions>() ?? new CorreoOptions();
        if (correo.UrlBaseValida() is null)
            logger.LogWarning(
                "Correo:UrlBase no está configurada o no es una dirección http/https válida: no se enviará " +
                "el correo de recuperación de contraseña y los enlaces de los recordatorios saldrán relativos. " +
                "Configúrela con la dirección pública del portal (variable Correo__UrlBase).");

        // Sin servidor de correo no sale ningún correo: ni la recuperación de contraseña ni los
        // recordatorios. La campana sí funciona. Es el mismo criterio con el que SmtpEmailService
        // decide no intentar el envío.
        var smtp = configuracion.GetSection(SmtpSettings.SectionName).Get<SmtpSettings>() ?? new SmtpSettings();
        if (!smtp.EstaConfigurado)
            logger.LogWarning(
                "El servidor de correo no está configurado: no se enviará ningún correo (recuperación de " +
                "contraseña ni recordatorios); los avisos de la campana siguen funcionando. Revise la " +
                "sección Smtp (servidor y usuario).");
    }
}
