using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Secciones contraíbles del menú lateral (2026-10-22). Lo que decide el servidor: qué
/// secciones aparecen, en qué orden, y cuál nace abierta —la que contiene la página, y
/// ninguna más—. Lo que decide el navegador (un clic, la última sección elegida) se
/// comprobó a mano: no hay pruebas de JavaScript en esta solución.
/// </summary>
public sealed class MenuLateralTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public Task InitializeAsync() => _portal.PrepararAsync();

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private sealed record Seccion(string Id, bool Abierta, string Controla, bool MenuExiste);

    private static List<Seccion> Secciones(string html) =>
        Regex.Matches(html,
                """<div class="nav-group[^"]*" data-seccion="(?<id>[^"]+)">\s*<button[^>]*aria-expanded="(?<exp>true|false)" aria-controls="(?<ctl>[^"]+)">""")
            .Select(m => new Seccion(
                m.Groups["id"].Value,
                m.Groups["exp"].Value == "true",
                m.Groups["ctl"].Value,
                html.Contains($"""<div class="nav-group-menu" id="{m.Groups["ctl"].Value}">""")))
            .ToList();

    private async Task<List<Seccion>> SeccionesEnAsync(string ruta, string rol = "Administrador") =>
        Secciones(await _portal.ClienteComo(rol).GetStringAsync(ruta));

    [Fact]
    public async Task Las_secciones_salen_en_el_orden_de_siempre_con_la_ayuda_al_final()
    {
        var html = await _portal.ClienteComo("Administrador").GetStringAsync("/Ayuda");

        Secciones(html).Select(s => s.Id).Should().Equal(
            "expedientes", "siger", "honduras-simple", "proyectos",
            "tableros", "agenda", "soporte", "panel-control");
        html.IndexOf("data-seccion=\"panel-control\"", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf(">Ayuda</a>", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Panel_de_control_absorbe_Administracion_en_una_sola_seccion()
    {
        // 2026-10-26: Panel de control y Administración se fusionaron; el título que queda es
        // Panel de control, con Notificaciones y lo que antes colgaba de Administración.
        var html = await _portal.ClienteComo("Administrador").GetStringAsync("/Ayuda");

        html.Should().NotContain(">Administración<svg");
        Regex.Matches(html, ">Panel de control<").Should().ContainSingle();
        var menu = html[html.IndexOf("id=\"nav-sec-panel-control\"", StringComparison.Ordinal)..];
        menu = menu[..menu.IndexOf("</div>", StringComparison.Ordinal)];
        menu.Should().Contain("Notificaciones").And.Contain("Catálogos").And.Contain("Usuarios")
            .And.Contain("Roles").And.Contain("Permisos").And.Contain("Alcance de módulos");
    }

    [Theory]
    [InlineData("/Proyectos", "proyectos")]
    [InlineData("/Catalogos/Insumos", "panel-control")]
    [InlineData("/PanelControl/Notificaciones", "panel-control")]
    [InlineData("/Reuniones/Compromisos", "agenda")]
    [InlineData("/", "expedientes")]
    public async Task Al_cargar_una_pagina_solo_esta_abierta_su_seccion(string ruta, string seccion)
    {
        var secciones = await SeccionesEnAsync(ruta);

        // aria-expanded ES el estado: el CSS contrae por él, así que lo que se anuncia y lo
        // que se ve no pueden separarse.
        secciones.Should().ContainSingle(s => s.Abierta).Which.Id.Should().Be(seccion);
    }

    [Fact]
    public async Task En_una_pagina_sin_seccion_todo_nace_contraido()
    {
        // La última sección elegida la repone el navegador; el servidor no la conoce.
        var secciones = await SeccionesEnAsync("/Ayuda");

        secciones.Should().NotBeEmpty();
        secciones.Should().OnlyContain(s => !s.Abierta);
    }

    [Fact]
    public async Task Cada_titulo_es_un_boton_que_controla_su_propio_submenu()
    {
        var secciones = await SeccionesEnAsync("/Proyectos");

        secciones.Should().OnlyContain(s => s.MenuExiste && s.Controla == "nav-sec-" + s.Id);
    }

    [Fact]
    public async Task Una_seccion_sin_elementos_visibles_no_aparece()
    {
        // El Consultor no administra nada: la sección Panel de control no se pinta, ni vacía
        // ni contraída.
        var secciones = await SeccionesEnAsync("/Ayuda", "Consultor");

        secciones.Select(s => s.Id).Should().NotContain("panel-control");
    }
}
