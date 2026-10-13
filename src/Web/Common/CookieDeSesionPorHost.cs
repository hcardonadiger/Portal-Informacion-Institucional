using System.Net;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Diger.TramitesEstado.Web.Common;

/// <summary>
/// Decide el <c>Domain</c> de la cookie de sesión a partir del host de la petición, y lo aplica
/// en <b>todas</b> las escrituras y borrados.
///
/// <para>El dominio existe para compartir la sesión entre el portal y su subdominio de
/// certificados (<c>cert.*</c>). Antes se fijaba solo en <c>OnSigningIn</c>, y eso dejaba dos
/// agujeros, porque el navegador identifica una cookie por <b>nombre + dominio + ruta</b>:</para>
///
/// <list type="number">
/// <item>Al cerrar sesión, el borrado salía sin dominio y no tocaba la cookie guardada con
/// <c>.dominio</c>. La sesión sobrevivía, el navegador la seguía enviando y <c>/Cuenta/Login</c>
/// la veía viva y mandaba al tablero: el botón «Cerrar sesión» parecía llevar al tablero.</item>
/// <item>Con <c>SlidingExpiration</c>, al pasar la mitad de la vida de la cookie el framework la
/// reescribe por su cuenta —sin pasar por <c>OnSigningIn</c>— y la reemitía sin dominio. Quedaban
/// dos cookies con el mismo nombre y la sesión dejaba de compartirse con <c>cert.*</c>, que era
/// justamente lo que el dominio venía a resolver.</item>
/// </list>
///
/// <para>Entrando por IP nada de esto se notaba: ahí no se fija dominio, así que escritura y
/// borrado siempre coincidían. Por eso el mismo binario cerraba sesión bien por
/// <c>192.168.0.52</c> y mal por el nombre de dominio.</para>
///
/// <para>El <c>ICookieManager</c> es el único punto por el que pasan las tres operaciones
/// —inicio de sesión, renovación y cierre—, así que la decisión vive acá una sola vez.</para>
/// </summary>
public sealed class CookieDeSesionPorHost : ICookieManager
{
    private readonly ChunkingCookieManager _interno = new();

    /// <summary>Dominio que le corresponde a un host, o <c>null</c> si no debe llevar ninguno.</summary>
    /// <remarks>Las direcciones IP quedan fuera porque los navegadores rechazan cookies de
    /// dominio para ellas (no existe «.192.168.0.52»), y <c>localhost</c> tampoco es un dominio
    /// registrable.</remarks>
    public static string? DominioPara(string host)
    {
        if (string.IsNullOrEmpty(host)) return null;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return null;
        if (!host.Contains('.')) return null;
        if (IPAddress.TryParse(host, out _)) return null;

        var principal = host.StartsWith("cert.", StringComparison.OrdinalIgnoreCase) ? host[5..] : host;
        return "." + principal;
    }

    public string? GetRequestCookie(HttpContext context, string key) =>
        _interno.GetRequestCookie(context, key);

    public void AppendResponseCookie(HttpContext context, string key, string? value, CookieOptions options)
    {
        var dominio = DominioPara(context.Request.Host.Host);
        if (dominio is not null) options.Domain = dominio;

        _interno.AppendResponseCookie(context, key, value, options);
    }

    public void DeleteCookie(HttpContext context, string key, CookieOptions options)
    {
        var dominio = DominioPara(context.Request.Host.Host);

        if (dominio is null)
        {
            _interno.DeleteCookie(context, key, options);
            return;
        }

        options.Domain = dominio;
        _interno.DeleteCookie(context, key, options);

        // Y una segunda orden sin dominio, para barrer la cookie que dejaron las renovaciones
        // anteriores a este arreglo: quien tenga sesión abierta al momento de desplegar carga
        // las dos, y si sobrevive la vieja el cierre de sesión seguiría sin surtir efecto.
        //
        // Va por Append y no por Delete a propósito: Delete descarta las cabeceras Set-Cookie ya
        // emitidas para el mismo nombre, y se llevaría por delante la orden de arriba.
        context.Response.Cookies.Append(key, string.Empty, new CookieOptions
        {
            Path     = options.Path,
            Secure   = options.Secure,
            SameSite = options.SameSite,
            HttpOnly = options.HttpOnly,
            Expires  = DateTimeOffset.UnixEpoch
        });
    }
}
