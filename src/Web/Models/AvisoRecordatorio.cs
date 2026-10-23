using Diger.TramitesEstado.Application.Notificaciones.Commands.EnviarRecordatorioManual;

namespace Diger.TramitesEstado.Web.Models;

/// <summary>
/// Lo que la pantalla dice después de un recordatorio manual (ticket, reunión, compromiso,
/// expediente).
///
/// <para>Hasta el 2026-10-22 las cuatro decían «enviado» aunque el correo fallara. El aviso de la
/// campana se guarda siempre; el correo puede fallar o no estar configurado, y la persona tiene que
/// saberlo. Si todo salió bien, cada pantalla conserva su texto de siempre. Reglas y textos de la
/// propuesta del agente de interfaz (diger-ui), en un solo lugar para que las cuatro pantallas no
/// se separen. No se sugiere «intentarlo de nuevo»: cada intento vuelve a dejar el aviso en la
/// campana de quien ya lo tiene.</para>
/// </summary>
public static class AvisoRecordatorio
{
    public enum Nivel { Exito, Advertencia, Error }

    /// <summary>Clave de TempData de la advertencia; la pinta <c>_WarningBanner</c>.</summary>
    public const string ClaveAdvertencia = "WarningMsg";

    private const string Revisar = "Avise al administrador para que revise el servidor de correo.";

    /// <param name="exito">El texto de éxito de siempre de la pantalla.</param>
    /// <param name="destinatarioUnico">El recordatorio de un compromiso va solo a su responsable: los
    /// errores hablan de él en vez de «las personas».</param>
    public static (Nivel Nivel, string Texto) Para(ResultadoRecordatorio r, string exito, bool destinatarioUnico = false)
    {
        var campana = r.AvisosCampana > 0;
        var quien   = destinatarioUnico
            ? "el responsable no tiene usuario en el portal"
            : "ninguna de las personas tiene usuario en el portal";

        if (r.CorreoApagado)
            return campana
                ? (Nivel.Advertencia,
                   "El aviso quedó en la campana de notificaciones. No se envió correo porque el correo de este módulo está apagado en Panel de control → Notificaciones.")
                : (Nivel.Error,
                   $"No se pudo enviar el recordatorio: {quien}, y el correo de este módulo está apagado en Panel de control → Notificaciones.");

        if (!campana && r.CorreosEnviados == 0 && r.ServidorSinConfigurar)
            return (Nivel.Error,
                $"No se pudo enviar el recordatorio: {quien}, y el portal todavía no tiene configurado su servidor de correo.");

        if (!campana && r.CorreosEnviados == 0 && r.CorreosFallidos > 0)
            return (Nivel.Error, $"No se pudo enviar el recordatorio: {quien}, y el correo no salió. {Revisar}");

        if (!campana && r.CorreosIntentados == 0)
            return (Nivel.Error, destinatarioUnico
                ? "No se pudo enviar el recordatorio: el compromiso no tiene un responsable con usuario en el portal ni correo registrado."
                : "No se pudo enviar el recordatorio: ninguna de las personas tiene usuario en el portal ni correo registrado.");

        if (r.ServidorSinConfigurar)
            return (Nivel.Advertencia,
                "El aviso quedó en la campana de notificaciones. No se envió correo porque el portal todavía no tiene configurado su servidor de correo.");

        if (r.CorreosFallidos > 0 && r.CorreosEnviados == 0)
            return (Nivel.Advertencia, $"El aviso quedó en la campana de notificaciones, pero el correo no salió. {Revisar}");

        if (r.CorreosFallidos > 0 && campana)
        {
            var cuantos = r.CorreosFallidos == 1
                ? $"no salió 1 de los {r.CorreosIntentados} correos"
                : $"no salieron {r.CorreosFallidos} de los {r.CorreosIntentados} correos";
            return (Nivel.Advertencia, $"El aviso quedó en la campana de notificaciones, pero {cuantos}. {Revisar}");
        }

        if (r.CorreosFallidos > 0)
            return (Nivel.Advertencia,
                $"El recordatorio salió por correo a {r.CorreosEnviados} de {r.CorreosIntentados} destinatarios; los demás correos no salieron. {Revisar}");

        return (Nivel.Exito, exito);
    }
}
