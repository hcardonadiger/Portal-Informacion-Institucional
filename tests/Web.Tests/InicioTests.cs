using System.Net;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Adónde va cada quien después de entrar.
///
/// El login mandaba a todos a Tableros. Un rol que solo trabaja en Honduras Fácil —que es
/// exactamente lo que se le da a quien prueba el portal— no tiene Tableros.Ver, así que lo primero
/// que veía al entrar era «Acceso denegado». Y el botón «Volver al inicio» de esa pantalla lo
/// llevaba a Expedientes: otro acceso denegado. /Inicio elige la primera pantalla del menú que el
/// rol sí puede abrir.
/// </summary>
public sealed class InicioTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Tableros.Ver", "Siger.Ver");
        await _portal.OtorgarAsync("Empleado", "Siger.Ver", "HondurasSimple.Ver", "HondurasSimple.Editar");
        await _portal.OtorgarAsync("Consultor", "HondurasSimple.Publicacion.Ver");
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Quien_tiene_Tableros_sigue_entrando_a_Tableros()
    {
        (await DestinoAsync("JefeArea")).Should().Be("/Tableros");
    }

    [Fact]
    public async Task Quien_solo_trabaja_en_Honduras_Facil_entra_al_inventario()
    {
        (await DestinoAsync("Empleado")).Should().Be("/Siger",
            "el inventario es la primera pantalla del menú que ese rol puede abrir");
    }

    [Fact]
    public async Task Quien_solo_puede_ver_lo_publicado_entra_a_Publicado()
    {
        (await DestinoAsync("Consultor")).Should().Be("/HondurasSimple/Publicacion");
    }

    [Fact]
    public async Task Una_cuenta_sin_asignacion_va_a_su_perfil()
    {
        (await DestinoAsync(null)).Should().StartWith("/Cuenta/Perfil",
            "sin rol no puede ver ningún módulo, y el perfil le dice qué le falta");
    }

    [Fact]
    public async Task El_boton_de_acceso_denegado_lleva_a_Inicio()
    {
        var respuesta = await _portal.ClienteComo("Empleado").GetAsync("/Cuenta/Denegado");
        var html = await respuesta.Content.ReadAsStringAsync();

        html.Should().Contain("href=\"/Inicio\"",
            "«Volver al inicio» llevaba a Expedientes, que a este rol también le está vedado");
    }

    private async Task<string> DestinoAsync(string? rol)
    {
        var respuesta = await _portal.ClienteComo(rol).GetAsync("/Inicio");
        respuesta.StatusCode.Should().Be(HttpStatusCode.Redirect);
        return respuesta.Headers.Location!.OriginalString;
    }
}

/// <summary>Un rol con asignación pero sin ningún permiso de módulo: no hay pantalla a la que
/// mandarlo, así que va a su perfil, que siempre puede ver.</summary>
public sealed class InicioSinPermisosTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public Task InitializeAsync() => _portal.PrepararAsync();

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Un_rol_sin_permisos_va_a_su_perfil()
    {
        var respuesta = await _portal.ClienteComo("Consultor").GetAsync("/Inicio");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Redirect);
        respuesta.Headers.Location!.OriginalString.Should().StartWith("/Cuenta/Perfil");
    }
}
