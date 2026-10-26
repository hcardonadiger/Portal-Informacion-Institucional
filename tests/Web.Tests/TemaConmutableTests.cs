using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Conmutador de tema claro/oscuro (<c>_Layout.cshtml</c>).
///
/// <para>Esta clase existe por una regresión concreta. El portal YA tuvo un modo oscuro
/// conmutable, agregado el 2026-08-07 y eliminado el 2026-09-24. Se eliminó porque la
/// elección de tema viajaba por JavaScript: un <c>&lt;script&gt;</c> escribía
/// <c>data-theme</c> en el <c>&lt;html&gt;</c> antes de las hojas de estilo, y cuando no
/// alcanzaba a correr —un teléfono que lo bloquea, o que lo ejecuta tarde— el registro
/// público de asistencia salía oscuro. Esa página la abre gente de fuera escaneando un QR.</para>
///
/// <para>El conmutador volvió el 2026-10-19 con la elección en una cookie que lee el
/// SERVIDOR, de modo que la clase ya sale resuelta en el HTML. Lo que se prueba acá es
/// justamente lo que falló aquella vez: que el registro de asistencia no obedece al tema
/// pase lo que pase, y que el resto del portal sí.</para>
///
/// <para>Desde el 2026-10-26 el oscuro está OCULTO (<c>_temaOscuroDisponible = false</c>):
/// todos ven el claro, la cookie se ignora y el botón no se dibuja. Las reglas
/// <c>.tema-oscuro</c> del CSS siguen vivas para poder reactivarlo, y por eso sus pruebas
/// de hoja siguen acá.</para>
///
/// <para>Se prueba por HTML renderizado y no por el PageModel a propósito: el defecto de
/// 2026-09 no estaba en ningún modelo, estaba en lo que terminaba en la página.</para>
/// </summary>
public sealed class TemaConmutableTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private Guid _tokenRegistro;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Reuniones.Ver");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var reunion = Reunion.Crear("Mesa de interoperabilidad");
        reunion.InstitucionId = "DIGER";
        reunion.InstitucionDuenaId = "DIGER";
        reunion.AreaId = PortalFactory.AreaDeCasa;
        reunion.UnidadId = PortalFactory.UnidadDeCasa;
        reunion.Fecha = new DateOnly(2026, 10, 15);

        db.Reuniones.Add(reunion);
        await db.SaveChangesAsync();

        // El enlace del QR: es lo único que hace pública a esa página.
        _tokenRegistro = reunion.RegistroToken;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private HttpClient ClienteDelPortal(string? tema = null)
    {
        var c = _portal.ClienteComo("JefeArea",
            area: PortalFactory.AreaDeCasa, unidad: PortalFactory.UnidadDeCasa);
        if (tema is not null)
            c.DefaultRequestHeaders.Add("Cookie", $"diger_tema={tema}");
        return c;
    }

    // ── El portal: el tema es del usuario ────────────────────────────────

    [Fact]
    public async Task Sin_cookie_el_portal_sale_CLARO()
    {
        // El claro es el estado de salida desde el 2026-10-19. Antes lo era el
        // oscuro, y eso rompía el login: su tarjeta es blanca fija, así que
        // heredaba los tokens de texto claros sobre blanco.
        var html = await ClienteDelPortal().GetStringAsync("/Reuniones/Index");

        html.Should().Contain("class=\"tema-claro\"",
            "el claro es el tema por omisión del portal");
        html.Should().NotContain("class=\"tema-oscuro\"");
        html.Should().Contain("content=\"light\"");
    }

    [Fact]
    public async Task Con_el_oscuro_oculto_la_cookie_oscura_se_ignora()
    {
        // Desde el 2026-10-26 el oscuro está oculto (_temaOscuroDisponible en
        // _Layout). Quien lo había elegido conserva la cookie, y sin botón no
        // tendría cómo salir: por eso la cookie se ignora, no solo el botón.
        var html = await ClienteDelPortal("oscuro").GetStringAsync("/Reuniones/Index");

        html.Should().Contain("class=\"tema-claro\"");
        html.Should().NotContain("class=\"tema-oscuro\"");
        html.Should().Contain("content=\"light\"",
            "el <meta name=color-scheme> es lo que pinta <select> y barras de " +
            "desplazamiento; tiene que acompañar al claro");
    }

    [Fact]
    public async Task Una_cookie_con_basura_cae_del_lado_claro()
    {
        // La condición compara por igualdad con "oscuro" y no niega "claro": una
        // cookie recortada o manipulada tiene que caer del lado seguro, que es
        // el que no deja texto claro sobre fondo claro.
        var html = await ClienteDelPortal("osc").GetStringAsync("/Reuniones/Index");

        html.Should().Contain("class=\"tema-claro\"");
    }

    [Fact]
    public async Task El_login_sale_claro_aunque_la_cookie_diga_oscuro()
    {
        // La tarjeta del login tiene background #fff fijo. Con los tokens del
        // tema oscuro encima, la letra salía clara sobre blanco y Chrome
        // remataba pintando el autorrelleno con su paleta oscura. Declara su
        // tema, así que la cookie de quien venga no la alcanza.
        var cliente = _portal.ClienteAnonimo();
        cliente.DefaultRequestHeaders.Add("Cookie", "diger_tema=oscuro");

        var html = await cliente.GetStringAsync("/Cuenta/Login");

        html.Should().NotContain("tema-oscuro",
            "su tarjeta es blanca fija: no puede recibir los tokens del oscuro");
        html.Should().Contain("content=\"light\"");
    }

    [Fact]
    public async Task La_eleccion_la_resuelve_el_servidor_no_el_navegador()
    {
        // El corazón del arreglo: la clase del cuerpo viene en el HTML, no la
        // pone un script. Si alguien vuelve a mover esto a JavaScript, el
        // registro de asistencia vuelve a romperse como en 2026-09.
        var html = await ClienteDelPortal().GetStringAsync("/Reuniones/Index");

        var cuerpo = html.IndexOf("<body", StringComparison.Ordinal);
        cuerpo.Should().BeGreaterThan(0);
        html[cuerpo..(cuerpo + 40)].Should().Contain("tema-claro",
            "la clase tiene que estar en el marcado servido, no aplicarse después");
    }

    [Fact]
    public async Task Con_el_oscuro_oculto_el_portal_no_muestra_el_boton()
    {
        var html = await ClienteDelPortal().GetStringAsync("/Reuniones/Index");

        html.Should().NotContain("id=\"temaBtn\"",
            "un botón que ofrece un tema oculto mentiría");
        html.Should().NotContain("Cambiar a tema oscuro");
    }

    // ── El QR público: claro siempre, pase lo que pase ───────────────────

    [Fact]
    public async Task El_registro_de_asistencia_sale_claro_sin_cookie()
    {
        var html = await _portal.ClienteAnonimo()
            .GetStringAsync($"/Asistencia/Registro/{_tokenRegistro}");

        html.Should().NotContain("tema-oscuro");
        html.Should().Contain("content=\"light\"");
    }

    [Fact]
    public async Task El_registro_de_asistencia_ignora_la_cookie_oscura()
    {
        // ESTA es la prueba que justifica el archivo. En 2026-09 esta página
        // salía oscura y por eso se eliminó el modo oscuro entero. Declara su
        // propia BodyClass, así que no participa del tema: ni cookie, ni botón.
        var cliente = _portal.ClienteAnonimo();
        cliente.DefaultRequestHeaders.Add("Cookie", "diger_tema=oscuro");

        var html = await cliente.GetStringAsync($"/Asistencia/Registro/{_tokenRegistro}");

        html.Should().NotContain("tema-oscuro",
            "la abre gente de fuera desde su teléfono y tiene que salir clara siempre");
        html.Should().Contain("content=\"light\"");
    }

    // ── El realce de la tabla ────────────────────────────────────────────

    [Fact]
    public async Task Los_dos_temas_le_dan_plano_propio_a_la_tabla()
    {
        // Sobre un fondo liso la tabla no necesita caja, pero los dos temas
        // llevan trama, y sin plano propio el patrón corre por debajo de cada
        // celda. El claro se quedó sin esta regla hasta el 2026-10-19, cuando
        // ganó su trama: la tabla quedó con los nodos pasándole por detrás.
        var hoja = await ClienteDelPortal().GetStringAsync("/css/diger.css");

        hoja.Should().Contain(".tema-oscuro .seg-table");
        hoja.Should().Contain(".tema-claro .seg-table",
            "el claro también tiene trama, así que su tabla también necesita plano");

        // Medido en L*: el celeste al 5 % aclara el suelo oscuro +3.64 y el
        // Cada tema se aleja del suyo en la dirección que tiene libre: el oscuro
        // ACLARA con celeste al 5 % y el claro ACLARA TAMBIÉN, con blanco al
        // 55 %. No son simétricos a propósito — oscurecer el suelo claro, que ya
        // tira a gris, daba otro gris y la tabla se confundía con el fondo.
        hoja.Should().Contain("rgba(122, 203, 221, .05)");
        hoja.Should().Contain("rgba(255, 255, 255, .55)");
    }

    [Fact]
    public async Task El_tema_oscuro_pinta_el_desplegable_del_select()
    {
        // El desplegable de un <select> lo dibuja el sistema operativo, no esta
        // hoja. Como el portal le pone background y color al select cerrado,
        // Chrome deja de aplicarle el esquema oscuro al panel: las <option>
        // heredan el texto claro y el fondo sale blanco. Texto blanco sobre
        // blanco, en los 168 selects del portal.
        //
        // Se prueba la regla y no el render porque el panel es del sistema: no
        // aparece en el DOM ni lo captura un navegador headless.
        var hoja = await ClienteDelPortal().GetStringAsync("/css/diger.css");

        hoja.Should().Contain(".tema-oscuro option",
            "sin pintar las opciones, el desplegable sale blanco con texto claro");
        hoja.Should().Contain(".tema-oscuro optgroup");

        var i = hoja.IndexOf(".tema-oscuro option", StringComparison.Ordinal);
        var regla = hoja[i..(i + 220)];
        regla.Should().Contain("--diger-panel",
            "el desplegable flota sobre la página, así que va sobre la superficie " +
            "opaca y no sobre --diger-surface, que es translúcida");
    }

    [Fact]
    public async Task En_papel_la_tabla_pierde_el_realce_en_los_dos_temas()
    {
        // El realce existe para separar la tabla de la trama. En papel no hay
        // trama: gastaría tinta sin separar nada.
        var hoja = await ClienteDelPortal().GetStringAsync("/css/diger.css");

        var print = hoja[hoja.LastIndexOf("@media print", StringComparison.Ordinal)..];
        print.Should().Contain(".tema-oscuro .seg-table");
        print.Should().Contain(".tema-claro .seg-table");
    }

    [Fact]
    public async Task El_registro_de_asistencia_no_muestra_el_boton()
    {
        var html = await _portal.ClienteAnonimo()
            .GetStringAsync($"/Asistencia/Registro/{_tokenRegistro}");

        html.Should().NotContain("id=\"temaBtn\"",
            "un botón que no puede cambiar el tema de esa página mentiría");
    }
}
