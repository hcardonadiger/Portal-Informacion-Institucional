using Diger.TramitesEstado.Web.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Fija un fallo reportado desde producción: entrando por <c>192.168.0.52</c> el botón «Cerrar
/// sesión» funcionaba, y entrando por <c>gestiondigital.diger.gob.hn</c> —mismo binario, misma
/// base— no cerraba nada y dejaba al usuario en el tablero.
///
/// <para>La causa: el navegador identifica una cookie por <b>nombre + dominio + ruta</b>. La
/// cookie se escribía con <c>Domain=.gestiondigital.diger.gob.hn</c> pero se borraba sin dominio,
/// así que la orden de borrado no la alcanzaba. La sesión sobrevivía y <c>/Cuenta/Login</c>, al
/// verla viva, redirigía al tablero. Por IP nunca se notó porque ahí no se fija dominio y las dos
/// órdenes coincidían.</para>
///
/// <para>El segundo agujero, del mismo origen: <c>SlidingExpiration</c> reescribe la cookie a
/// mitad de su vida sin pasar por <c>OnSigningIn</c>, y la reemitía sin dominio.</para>
/// </summary>
public sealed class CierreDeSesionPorDominioTests
{
    private const string Cookie = ".AspNetCore.Cookies";

    private static HttpContext Peticion(string host)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Host = new HostString(host);
        return ctx;
    }

    private static IReadOnlyList<string> Ordenes(HttpContext ctx) =>
        ctx.Response.Headers["Set-Cookie"].Select(v => (v ?? string.Empty).ToLowerInvariant()).ToList();

    [Fact]
    public void El_portal_y_su_subdominio_de_certificados_comparten_el_mismo_dominio()
    {
        // Es la razón por la que el dominio se fija: que la sesión valga en cert.* y en el portal.
        CookieDeSesionPorHost.DominioPara("gestiondigital.diger.gob.hn")
            .Should().Be(".gestiondigital.diger.gob.hn");

        CookieDeSesionPorHost.DominioPara("cert.gestiondigital.diger.gob.hn")
            .Should().Be(".gestiondigital.diger.gob.hn");
    }

    [Fact]
    public void Una_direccion_IP_no_lleva_dominio()
    {
        // Los navegadores rechazan cookies de dominio para direcciones IP: no existe
        // ".192.168.0.52". Sin dominio al escribir y sin dominio al borrar, las dos coinciden.
        CookieDeSesionPorHost.DominioPara("192.168.0.52").Should().BeNull();
        CookieDeSesionPorHost.DominioPara("localhost").Should().BeNull();
    }

    [Fact]
    public void El_borrado_lleva_el_mismo_dominio_con_el_que_se_escribio()
    {
        // El corazón del fallo: sin esto la cookie sobrevive al cierre de sesión.
        var ctx = Peticion("gestiondigital.diger.gob.hn");

        new CookieDeSesionPorHost().DeleteCookie(ctx, Cookie, new CookieOptions { Path = "/" });

        Ordenes(ctx).Should().Contain(o => o.Contains("domain=.gestiondigital.diger.gob.hn"),
            "una orden de borrado sin dominio no toca una cookie guardada con dominio");
    }

    [Fact]
    public void El_borrado_tambien_barre_la_cookie_sin_dominio_que_dejo_la_renovacion()
    {
        // Quien tenga sesión abierta al desplegar carga las dos variantes. Si sobrevive la vieja,
        // el cierre de sesión seguiría sin surtir efecto pese al arreglo.
        var ctx = Peticion("gestiondigital.diger.gob.hn");

        new CookieDeSesionPorHost().DeleteCookie(ctx, Cookie, new CookieOptions { Path = "/" });

        var ordenes = Ordenes(ctx);
        ordenes.Should().HaveCount(2);
        ordenes.Should().Contain(o => !o.Contains("domain="));
    }

    [Fact]
    public void La_renovacion_de_la_cookie_conserva_el_dominio()
    {
        // SlidingExpiration reescribe por esta vía, sin pasar por OnSigningIn. Cuando perdía el
        // dominio quedaban dos cookies con el mismo nombre y la sesión dejaba de valer en cert.*.
        var ctx = Peticion("gestiondigital.diger.gob.hn");

        new CookieDeSesionPorHost()
            .AppendResponseCookie(ctx, Cookie, "valor-renovado", new CookieOptions { Path = "/" });

        Ordenes(ctx).Should().ContainSingle()
            .Which.Should().Contain("domain=.gestiondigital.diger.gob.hn");
    }

    [Fact]
    public void Entrando_por_IP_el_borrado_queda_como_estaba()
    {
        // La contraparte: el camino que hoy funciona no puede cambiar de comportamiento.
        var ctx = Peticion("192.168.0.52");

        new CookieDeSesionPorHost().DeleteCookie(ctx, Cookie, new CookieOptions { Path = "/" });

        Ordenes(ctx).Should().ContainSingle()
            .Which.Should().NotContain("domain=");
    }
}
