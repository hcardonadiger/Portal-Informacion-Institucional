namespace Diger.TramitesEstado.Application.Common.Models;

/// <summary>
/// Sección <c>Correo</c> de la configuración.
///
/// <para><see cref="UrlBase"/> es la dirección pública del portal, tal como la escribe una persona
/// en el navegador, incluida la carpeta virtual de IIS si la hay (por ejemplo,
/// <c>https://servidor.dominio/GestionDigital</c>). Con ella se arman los enlaces que salen del
/// portal. Antes se tomaba la dirección de la petición (<c>Request.Host</c>), que la manda quien
/// hace la petición: pidiendo la recuperación de contraseña de otra persona con un host propio,
/// su correo llevaba el token a ese host (corregido el 2026-10-22).</para>
/// </summary>
public sealed class CorreoOptions
{
    public const string Seccion = "Correo";

    public string? UrlBase { get; set; }

    /// <summary>La dirección base sin barra final, o <c>null</c> si falta o no es una dirección
    /// http/https absoluta. Una mal escrita se trata igual que una que falta: no se arma ningún
    /// enlace con ella.</summary>
    public string? UrlBaseValida()
    {
        if (string.IsNullOrWhiteSpace(UrlBase)) return null;
        if (!Uri.TryCreate(UrlBase.Trim(), UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return null;
        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }

    /// <summary>Convierte una ruta del portal («/Tickets/Detalle/5») en un enlace absoluto para un
    /// correo: un enlace relativo no se abre desde el programa de correo. Sin una
    /// <see cref="UrlBase"/> válida la ruta queda relativa, como antes; el arranque lo advierte.</summary>
    public string Enlace(string ruta) => UrlBaseValida() is { } b ? b + ruta : ruta;
}
