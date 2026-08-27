using System.Net;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El instructivo del módulo de Proyectos en el Centro de Ayuda.
///
/// <para>La ayuda se sirve a cualquiera con sesión —documentar cómo se usa el portal no expone
/// datos— pero <b>cada tema se muestra según los módulos que la persona tiene</b>. Explicarle a
/// alguien una pantalla que no puede abrir es ruido, y peor: le hace pedir accesos que no
/// necesita.</para>
/// </summary>
public sealed class AyudaTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Proyectos.Ver");
        // Consultor queda deliberadamente sin Proyectos.Ver.
        await _portal.OtorgarAsync("Consultor", "Tickets.Ver");
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private Task<string> AyudaAsync(string rol) =>
        _portal.ClienteComo(rol).GetStringAsync("/Ayuda/Index");

    [Fact]
    public async Task El_centro_de_ayuda_se_abre_con_sesion()
    {
        var respuesta = await _portal.ClienteComo("Consultor").GetAsync("/Ayuda/Index");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Quien_tiene_proyectos_recibe_el_instructivo_del_modulo()
    {
        var html = await AyudaAsync("JefeArea");

        html.Should().Contain("id=\"ayuda-proyectos\"");
        html.Should().Contain("Proyectos y seguimiento", "el tema tiene que estar en el menú lateral");
    }

    [Fact]
    public async Task Quien_no_tiene_proyectos_no_lo_recibe()
    {
        var html = await AyudaAsync("Consultor");

        html.Should().NotContain("id=\"ayuda-proyectos\"");
        html.Should().NotContain("Proyectos y seguimiento");
    }

    [Fact]
    public async Task El_instructivo_cubre_lo_que_mas_confunde()
    {
        // No es decoración: son los cuatro puntos donde el módulo se entiende mal y que hoy hay
        // que explicar a mano. Si alguien recorta el instructivo, que al menos falle acá.
        var html = await AyudaAsync("JefeArea");

        html.Should().Contain("Entregable = QUÉ se entrega",
            "la distinción entregable/actividad es lo primero que hay que explicar");
        html.Should().Contain("solo puede ser alguien registrado como interesado",
            "el selector de responsable vacío es la duda número uno");
        html.Should().Contain("El único número que se teclea es el de la actividad",
            "el avance se calcula, no se declara");
        html.Should().Contain("Avisan, no bloquean",
            "las dependencias avisan y no impiden reportar");
    }

    [Fact]
    public async Task El_instructivo_apunta_al_proyecto_demostrativo()
    {
        // Durante la validación con usuarios, el demo es la referencia de «así se ve bien llevado».
        var html = await AyudaAsync("JefeArea");

        html.Should().Contain("PRY-2026-99");
    }

    [Theory]
    [InlineData("JefeArea")]
    [InlineData("Consultor")]
    public async Task El_menu_principal_ofrece_la_ayuda_a_cualquiera_con_sesion(string rol)
    {
        // Hasta ahora el enlace vivía solo dentro del panel de usuario: quien no supiera que
        // existía, no lo encontraba. Va sin condición de permiso porque documentar el portal no
        // expone datos — la propia página lleva [PermisoNoRequerido] por la misma razón.
        var html = await _portal.ClienteComo(rol).GetStringAsync("/Proyectos/Index");

        html.Should().Contain("/Ayuda/Index", "el enlace tiene que estar en el menú de todas las páginas");
        html.Should().Contain("Manual de usuario del portal");
    }

    [Fact]
    public async Task La_ayuda_tambien_llega_a_quien_no_tiene_ningun_modulo()
    {
        // El usuario sin asignación es el caso límite: es justo quien más necesita el manual.
        var respuesta = await _portal.ClienteComo(null).GetAsync("/Ayuda/Index");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
