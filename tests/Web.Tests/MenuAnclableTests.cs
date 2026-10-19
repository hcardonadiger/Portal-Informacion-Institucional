using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El menú principal anclable (<c>_Layout.cshtml</c>, <c>diger.js</c>, <c>diger.css</c>).
///
/// <para>Hasta el 2026-10-15 el menú era un cajón <em>pasajero</em>: se abría con la hamburguesa,
/// tendía un velo sobre la página y se cerraba con Escape o con un clic en cualquier parte. Ese
/// velo es justo lo que limitaba el uso de los módulos — intercepta todo clic sobre el contenido,
/// así que con el menú abierto no se podía trabajar.</para>
///
/// <para>Ahora el mismo botón <em>ancla</em> el cajón: el contenido se corre a su derecha en vez
/// de quedar tapado, no hay velo, y solo la hamburguesa lo vuelve a ocultar.</para>
///
/// <para><b>Por qué la elección viaja en cookie y no en localStorage.</b> Cada enlace del portal
/// es una recarga completa. Si el estado viviera en el navegador, habría que reponerlo con un
/// script y el contenido daría un salto visible en cada página — o se perdería el anclaje al
/// primer clic. La cookie la lee el servidor, así que la clase ya sale en el HTML. Es el mismo
/// camino que tomó <c>diger_tema</c> cuando el modo oscuro por script se eliminó el 2026-09-24
/// por pintar mal en el camino crítico.</para>
///
/// <para>Se prueba sobre el HTML renderizado y sobre la hoja que sirve el portal, no sobre los
/// fuentes: lo que importa es lo que le llega al navegador.</para>
/// </summary>
public sealed class MenuAnclableTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Reuniones.Ver");
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Pide una página del portal con la cookie del cajón en el estado indicado.
    /// <paramref name="estado"/> en <c>null</c> simula la primera visita, sin cookie.</summary>
    private async Task<string> PaginaAsync(string? estado)
    {
        var cliente = _portal.ClienteComo("JefeArea",
            area: PortalFactory.AreaDeCasa, unidad: PortalFactory.UnidadDeCasa);

        if (estado is not null)
            cliente.DefaultRequestHeaders.Add("Cookie", $"diger_menu={estado}");

        return await cliente.GetStringAsync("/Reuniones");
    }

    private async Task<string> HojaDeEstiloAsync() =>
        await _portal.ClienteAnonimo().GetStringAsync("/css/diger.css");

    private async Task<string> HojaDeTokensAsync() =>
        await _portal.ClienteAnonimo().GetStringAsync("/css/tokens.css");

    private async Task<string> GuionAsync() =>
        await _portal.ClienteAnonimo().GetStringAsync("/js/diger.js");

    // ── Arranca anclado ─────────────────────────────────────────────────────

    [Fact]
    public async Task EnLaPrimeraVisitaElCajonLlegaAnclado()
    {
        // Cambió el 2026-10-19: antes arrancaba oculto y había que descubrir la
        // hamburguesa para sacar la navegación. El menú es la estructura del
        // portal, así que se ve; quien no lo quiera lo contrae con el botón.
        var html = await PaginaAsync(null);

        html.Should().Contain("nav-anclado",
            "sin cookie el cajón llega anclado y el shell lleva la clase");
        html.Should().Contain("aria-expanded=\"true\"",
            "la hamburguesa tiene que decir que está abierto");
    }

    [Fact]
    public async Task UnaCookieConBasuraDejaElMenuVISIBLE()
    {
        // La condición pregunta `!= "oculto"` y no `== "anclado"`: solo un
        // ocultamiento explícito esconde el menú. Una cookie recortada o
        // manipulada cae del lado visible, que es el que no deja al usuario sin
        // navegación y sin saber dónde buscarla.
        var html = await PaginaAsync("ancl");

        html.Should().Contain("nav-anclado");
    }

    [Fact]
    public async Task LaCookieEnOcultoDejaElCajonCerrado()
    {
        var html = await PaginaAsync("oculto");

        html.Should().NotContain("nav-anclado");
    }

    // ── Anclado: sale del servidor, ya abierto ──────────────────────────────

    [Fact]
    public async Task LaCookieAncladaLlegaComoClaseEnElHtml()
    {
        // Esto es lo que evita el salto al navegar de módulo en módulo: la clase viene
        // en el HTML, no la pone un script después de pintar.
        var html = await PaginaAsync("anclado");

        html.Should().Contain("nav-anclado",
            "el servidor lee la cookie y marca el shell antes de pintar");
    }

    [Fact]
    public async Task AncladoElCajonYaSaleAbiertoYElBotonLoDice()
    {
        var html = await PaginaAsync("anclado");

        html.Should().MatchRegex("class=\"main-nav[^\"]*\\bopen\\b",
            "el cajón anclado se pinta ya desplegado, no cerrado esperando al script");
        html.Should().Contain("aria-expanded=\"true\"",
            "un lector de pantalla no tiene otra forma de saber que está abierto");
    }

    [Fact]
    public async Task ElCajonAncladoNoTiendeElVelo()
    {
        // El velo abierto es lo que limitaba el uso de los módulos. Anclado no se pinta.
        var html = await PaginaAsync("anclado");

        html.Should().NotContain("nav-backdrop open",
            "anclado no hay velo: el contenido se corre, no se tapa");
    }

    // ── El contenido se corre en vez de quedar tapado ───────────────────────

    [Fact]
    public async Task AncladoElContenidoSeCorreAlAnchoDelCajon()
    {
        var css = await HojaDeEstiloAsync();

        css.Should().Contain(".nav-anclado > .app-main",
            "el contenido principal cede el ancho del cajón");
        css.Should().Contain(".nav-anclado > .diger-footer",
            "el pie se corre con el contenido, o queda desalineado bajo el cajón");
        css.Should().Contain("--diger-nav-w",
            "el ancho del cajón es un token, para que el corrimiento no se desincronice");
    }

    [Fact]
    public async Task ElCajonYElCorrimientoMidenLoMismo()
    {
        // Si el cajón mide 264px y el contenido se corre otra cantidad, queda una franja
        // muerta o el cajón pisa el contenido. Los dos leen el mismo token, que se declara
        // en tokens.css —donde vive la capa de valores— y se consume en diger.css.
        var tokens = await HojaDeTokensAsync();
        var css    = await HojaDeEstiloAsync();

        tokens.Should().MatchRegex(@"--diger-nav-w:\s*264px",
            "el token vale lo que mide el cajón");
        css.Should().MatchRegex(@"(?s)\.main-nav\s*\{[^}]*width:\s*var\(--diger-nav-w\)",
            "el cajón lee su ancho del token en vez de repetir el número");
    }

    [Fact]
    public async Task EnPantallaChicaElCajonVuelveAFlotar()
    {
        // Abajo de 900px no hay 264px que ceder sin dejar el contenido en una tira, así
        // que ahí el cajón sigue siendo pasajero: flota y el velo vuelve.
        var css = await HojaDeEstiloAsync();

        css.Should().Contain("ANCLAJE DEL CAJÓN",
            "el bloque va rotulado, como el resto de la hoja");
        css.Should().MatchRegex(@"(?s)@media\s*\(max-width:\s*900px\)[^@]*\.nav-anclado\s*>\s*\.app-main",
            "en pantalla chica el corrimiento se anula");
    }

    [Fact]
    public async Task ElVeloSeCallaSoloEnPantallaAnchaYAnclada()
    {
        var css = await HojaDeEstiloAsync();

        css.Should().MatchRegex(@"(?s)@media\s*\(min-width:\s*901px\)[^@]*\.nav-anclado\s+\.nav-backdrop",
            "el velo se calla cuando el cajón está anclado y hay ancho para correr el contenido; "
          + "en pantalla chica se conserva, que ahí el cajón sí tapa");
    }

    // ── Persistencia y cierre ───────────────────────────────────────────────

    [Fact]
    public async Task AlAnclarSeGuardaLaEleccionEnLaCookie()
    {
        var js = await GuionAsync();

        js.Should().Contain("diger_menu=",
            "la elección se guarda para que sobreviva a la recarga del próximo módulo");
        js.Should().Contain("max-age=31536000",
            "es una preferencia, no una sesión: un año, como el tema");
        js.Should().Contain("samesite=lax");
        js.Should().NotContain("localStorage.setItem('diger-menu'",
            "el estado NO viaja por localStorage: se pinta desde el servidor");
    }

    [Fact]
    public async Task EscapeYaNoCierraElCajonAncladoEnPantallaAncha()
    {
        // Anclado hay que volver a tocar la hamburguesa. Si Escape lo cerrara, el menú
        // se desanclaría al teclear Escape en cualquier formulario.
        var js = await GuionAsync();

        js.Should().Contain("cajonEsPasajero",
            "hay una sola pregunta —¿es pasajero?— y de ella dependen Escape y el velo");
        js.Should().MatchRegex(@"(?s)function cajonEsPasajero[^}]*900",
            "pasajero es: no anclado, o pantalla angosta");
    }
}
