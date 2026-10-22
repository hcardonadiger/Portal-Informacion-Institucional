using System.Net;
using Diger.TramitesEstado.Application.Reuniones.Outlook;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>El retorno de Microsoft solo se acepta con el estado que la propia sesión emitió
/// (anti-CSRF); el perfil apaga la tarjeta cuando el servidor no tiene la app registrada.</summary>
public sealed class OutlookPaginasTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private readonly IConexionMicrosoft _conexion = Substitute.For<IConexionMicrosoft>();

    public async Task InitializeAsync()
    {
        _portal.ServiciosDePrueba = s =>
        {
            s.RemoveAll<IConexionMicrosoft>();
            s.AddScoped(_ => _conexion);
        };
        await _portal.PrepararAsync();
    }

    public Task DisposeAsync() { _portal.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task Sin_configuracion_el_perfil_lo_dice_y_no_ofrece_conectar()
    {
        _conexion.EstaConfigurada.Returns(false);

        var html = await _portal.ClienteComo("Empleado").GetStringAsync("/Cuenta/Perfil");

        html.Should().Contain("no está configurada");
        html.Should().NotContain("ConectarOutlook");
    }

    [Fact]
    public async Task Configurada_y_sin_conexion_ofrece_conectar()
    {
        _conexion.EstaConfigurada.Returns(true);

        var html = await _portal.ClienteComo("Empleado").GetStringAsync("/Cuenta/Perfil");

        html.Should().Contain("Conectar mi Outlook");
    }

    [Fact]
    public async Task Un_retorno_con_estado_ajeno_no_canjea_nada()
    {
        _conexion.EstaConfigurada.Returns(true);

        var resp = await _portal.ClienteComo("Empleado").GetAsync("/Cuenta/OutlookCallback?code=abc&state=inventado");

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        resp.Headers.Location!.ToString().Should().Contain("/Cuenta/Perfil");
        await _conexion.DidNotReceiveWithAnyArgs().CompletarAsync(default, default!, default!, default!, default);
    }

    [Fact]
    public async Task Sin_sesion_el_retorno_no_es_alcanzable()
    {
        var resp = await _portal.ClienteAnonimo().GetAsync("/Cuenta/OutlookCallback?code=abc&state=x");

        resp.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }
}
