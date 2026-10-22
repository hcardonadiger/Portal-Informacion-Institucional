using System.Text;

namespace Diger.TramitesEstado.Application.Notificaciones;

/// <summary>
/// Ayudantes para armar el HTML de los correos.
///
/// <para>Se escapa <b>solo lo que viene de las personas</b> —nombres, títulos, el mensaje que se
/// escribe al enviar un recordatorio— y no el HTML de la plantilla, que sigue siendo exactamente el
/// de antes. Sin escapar, un título con <c>&lt;b&gt;</c> o un mensaje con <c>&lt;script&gt;</c> se
/// metían como marcado en el correo (corregido el 2026-10-22).</para>
/// </summary>
public static class PlantillasCorreo
{
    /// <summary>Escapa texto para meterlo en el HTML del correo. Solo los cinco caracteres que
    /// tienen significado en HTML: <c>WebUtility.HtmlEncode</c> además convierte las tildes en
    /// entidades (é → &amp;#233;), y el correo dejaría de ser igual al de antes.</summary>
    public static string E(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return "";
        var sb = new StringBuilder(texto.Length + 16);
        foreach (var c in texto)
            sb.Append(c switch
            {
                '&'  => "&amp;",
                '<'  => "&lt;",
                '>'  => "&gt;",
                '"'  => "&quot;",
                '\'' => "&#39;",
                _    => c.ToString(),
            });
        return sb.ToString();
    }
}
