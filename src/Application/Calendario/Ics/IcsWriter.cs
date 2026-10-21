using System.Globalization;
using System.Text;

namespace Diger.TramitesEstado.Application.Calendario.Ics;

/// <summary>
/// Escribe un calendario en formato iCalendar (RFC 5545), que es lo que entienden Outlook, Google
/// Calendar y Apple sin que el portal tenga que integrarse con ninguno.
///
/// <para><b>Se escribe a mano y sin biblioteca</b> porque lo que hace falta es un subconjunto
/// chico y estable del formato: eventos sin recurrencia, sin alarmas y sin adjuntos. Lo que sí hay
/// que respetar al pie de la letra —y es donde fallan las implementaciones caseras— son cuatro
/// reglas: fin de línea CRLF, plegado a 75 <b>octetos</b>, escape de <c>\ ; , </c> y salto de
/// línea, y fechas UTC con sufijo Z.</para>
///
/// <para><b>Por qué UTC y no TZID</b>: usar identificadores de zona obliga a emitir un bloque
/// <c>VTIMEZONE</c> con las reglas históricas de la zona, que es la parte más fácil de escribir mal.
/// Un instante en UTC es inequívoco y todos los clientes lo muestran en la hora local de quien lo
/// abre, que es justamente lo que se quiere.</para>
///
/// <para><b>Por qué <c>METHOD:PUBLISH</c> y no <c>REQUEST</c></b>: el archivo publica un evento para
/// que cada quien lo agregue; no convoca. Una convocatoria formal esperaría respuestas de
/// aceptación, y dos tercios de los asistentes de estas reuniones son de otras instituciones, que
/// responderían a un buzón del portal que nadie atiende.</para>
/// </summary>
public static class IcsWriter
{
    /// <summary>Máximo de octetos por línea antes de plegar (RFC 5545, sección 3.1).</summary>
    private const int MaxOctetos = 75;

    public static string Escribir(
        IEnumerable<EventoIcs> eventos,
        string prodId,
        string? nombreCalendario = null,
        DateTime? ahoraUtc = null)
    {
        var sb = new StringBuilder();
        var sello = Instante(ahoraUtc ?? DateTime.UtcNow);

        Linea(sb, "BEGIN:VCALENDAR");
        Linea(sb, "VERSION:2.0");
        Linea(sb, $"PRODID:-//{Escapar(prodId)}//ES");
        Linea(sb, "CALSCALE:GREGORIAN");
        Linea(sb, "METHOD:PUBLISH");

        if (!string.IsNullOrWhiteSpace(nombreCalendario))
        {
            // X-WR-CALNAME no es estándar, pero es lo que leen Google y Apple para rotular un
            // calendario suscrito. Sin él, la suscripción aparece con la URL como nombre.
            Linea(sb, $"X-WR-CALNAME:{Escapar(nombreCalendario)}");
            Linea(sb, $"NAME:{Escapar(nombreCalendario)}");
        }

        foreach (var e in eventos)
            EscribirEvento(sb, e, sello);

        Linea(sb, "END:VCALENDAR");
        return sb.ToString();
    }

    /// <summary>Un evento sin ventana no es representable. Se descarta entero y en silencio: emitir
    /// un VEVENT inválido hace que algunos clientes descarten el archivo completo, o sea que una
    /// reunión sin fecha se llevaría puestas a todas las demás.</summary>
    private static bool EsRepresentable(EventoIcs e) =>
        e.EsDiaCompleto || (e.Inicio is not null && e.Fin is not null);

    private static void EscribirEvento(StringBuilder sb, EventoIcs e, string sello)
    {
        if (!EsRepresentable(e)) return;

        Linea(sb, "BEGIN:VEVENT");
        Linea(sb, $"UID:{Escapar(e.Uid)}");
        Linea(sb, $"DTSTAMP:{sello}");

        if (e.EsDiaCompleto)
        {
            // VALUE=DATE deja el evento sin hora. DTEND es exclusivo, de ahí el +1 día por omisión.
            Linea(sb, $"DTSTART;VALUE=DATE:{Dia(e.Dia!.Value)}");
            Linea(sb, $"DTEND;VALUE=DATE:{Dia(e.DiaFin ?? e.Dia!.Value.AddDays(1))}");
        }
        else
        {
            Linea(sb, $"DTSTART:{Instante(e.Inicio!.Value.UtcDateTime)}");
            Linea(sb, $"DTEND:{Instante(e.Fin!.Value.UtcDateTime)}");
        }

        Linea(sb, $"SUMMARY:{Escapar(e.Titulo)}");

        if (!string.IsNullOrWhiteSpace(e.Descripcion)) Linea(sb, $"DESCRIPTION:{Escapar(e.Descripcion)}");
        if (!string.IsNullOrWhiteSpace(e.Lugar))       Linea(sb, $"LOCATION:{Escapar(e.Lugar)}");
        if (!string.IsNullOrWhiteSpace(e.Url))         Linea(sb, $"URL:{Escapar(e.Url)}");

        if (!string.IsNullOrWhiteSpace(e.OrganizadorCorreo))
            Linea(sb, $"ORGANIZER{Cn(e.OrganizadorNombre)}:mailto:{e.OrganizadorCorreo.Trim()}");

        foreach (var a in e.Asistentes)
        {
            if (string.IsNullOrWhiteSpace(a.Correo)) continue;
            Linea(sb, $"ATTENDEE{Cn(a.Nombre)};ROLE=REQ-PARTICIPANT;PARTSTAT=NEEDS-ACTION:mailto:{a.Correo.Trim()}");
        }

        if (e.UltimaModificacionUtc is { } mod)
            Linea(sb, $"LAST-MODIFIED:{Instante(mod)}");

        Linea(sb, "STATUS:CONFIRMED");
        Linea(sb, "END:VEVENT");
    }

    // ── Formato ───────────────────────────────────────────────────────────────

    private static string Instante(DateTime utc) =>
        utc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string Dia(DateOnly d) =>
        d.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    /// <summary>
    /// El parámetro <c>CN</c> de ORGANIZER y ATTENDEE, entre comillas.
    ///
    /// <para>Los parámetros <b>no</b> usan los escapes con barra invertida de los valores: ahí
    /// <c>\,</c> se lee literal y rompe la línea. La forma correcta es la cadena entrecomillada
    /// (RFC 5545, 3.2), que admite comas y dos puntos y solo prohíbe la comilla doble. Sin esto, un
    /// nombre escrito «Ortez, Ana» —normal en un directorio— generaba un archivo inválido.</para>
    /// </summary>
    private static string Cn(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return "";
        var limpio = nombre.Trim().Replace("\"", "").Replace("\r", " ").Replace("\n", " ");
        return limpio.Length == 0 ? "" : $";CN=\"{limpio}\"";
    }

    /// <summary>
    /// Escapes de RFC 5545. La barra invertida va primero: si se hiciera después, escaparía las
    /// barras que acaban de introducir los otros reemplazos.
    /// </summary>
    internal static string Escapar(string? valor) =>
        (valor ?? "")
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n")
            .Replace("\r", "\\n");

    /// <summary>
    /// Agrega una línea plegada a 75 octetos. El corte se cuenta en <b>bytes UTF-8</b> y no en
    /// caracteres: con acentos —que acá hay en casi todos los títulos— un corte por caracteres
    /// produce líneas demasiado largas, y peor, puede partir un carácter a la mitad.
    /// </summary>
    internal static void Linea(StringBuilder sb, string linea)
    {
        if (Encoding.UTF8.GetByteCount(linea) <= MaxOctetos)
        {
            sb.Append(linea).Append("\r\n");
            return;
        }

        var octetos = 0;
        var primera = true;
        var trozo = new StringBuilder();

        foreach (var runa in linea.EnumerateRunes())
        {
            var largo = Encoding.UTF8.GetByteCount(runa.ToString());

            // A partir de la segunda línea se antepone un espacio, que también ocupa un octeto.
            var tope = primera ? MaxOctetos : MaxOctetos - 1;

            if (octetos + largo > tope)
            {
                sb.Append(primera ? "" : " ").Append(trozo).Append("\r\n");
                trozo.Clear();
                octetos = 0;
                primera = false;
            }

            trozo.Append(runa);
            octetos += largo;
        }

        if (trozo.Length > 0)
            sb.Append(primera ? "" : " ").Append(trozo).Append("\r\n");
    }
}
