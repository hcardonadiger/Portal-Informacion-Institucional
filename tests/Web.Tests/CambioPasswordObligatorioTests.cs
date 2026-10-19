using System.Globalization;
using System.Net;
using System.Security.Claims;
using Diger.TramitesEstado.Application.Common.Models;
using Diger.TramitesEstado.Infrastructure.Security;
using Diger.TramitesEstado.Web.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El portón del cambio de contraseña por única vez, probado en los dos planos que pueden
/// fallar por separado: la decisión (a quién retiene y a quién no) y el cableado (que el filtro
/// esté efectivamente registrado y mande a la pantalla de cambio).
/// </summary>
public sealed class CambioPasswordObligatorioDecisionTests
{
    private static CambioPasswordObligatorioPageFilter Filtro(string? corte) =>
        new(Options.Create(new AuthOptions
        {
            CambioPasswordObligatorioDesde = corte is null
                ? null
                : DateTime.Parse(corte, CultureInfo.InvariantCulture)
        }));

    private static HttpContext Sesion(DateTime? cambiadoEl, bool autenticada = true)
    {
        var claims = new List<Claim> { new(AppClaims.UserId, Guid.NewGuid().ToString()) };

        if (cambiadoEl is { } fecha)
            claims.Add(new Claim(
                AppClaims.PasswordCambiadoEl,
                fecha.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));

        var identity = autenticada
            ? new ClaimsIdentity(claims, "Pruebas")
            : new ClaimsIdentity(claims);           // sin authenticationType no está autenticada

        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    private static string Ayer => DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
    private static string Manana => DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");

    [Fact]
    public void SinCorteConfiguradoNoRetieneANadie()
    {
        // Falla abierto a propósito: una clave mal escrita no puede dejar al portal entero
        // girando en la pantalla de cambio.
        Filtro(null).DebeRetener(Sesion(cambiadoEl: null), "/Tableros/Index")
            .Should().BeFalse();
    }

    [Fact]
    public void AntesDelCorteNoRetieneAunqueFalteElCambio()
    {
        Filtro(Manana).DebeRetener(Sesion(cambiadoEl: null), "/Tableros/Index")
            .Should().BeFalse("mientras la fecha no llegue, nadie debe notar que esto existe");
    }

    [Fact]
    public void SinClaimDeFechaRetiene()
    {
        // Es el caso de la cookie emitida antes de este despliegue y el de la contraseña que
        // puso un administrador: en ambos el cambio falta.
        Filtro(Ayer).DebeRetener(Sesion(cambiadoEl: null), "/Tableros/Index")
            .Should().BeTrue();
    }

    [Fact]
    public void ConUnCambioAnteriorAlCorteRetiene()
    {
        Filtro(Ayer).DebeRetener(Sesion(DateTime.UtcNow.AddMonths(-6)), "/Tableros/Index")
            .Should().BeTrue("la tanda única exige que el cambio ocurra a partir del corte");
    }

    [Fact]
    public void ConElCambioYaHechoNoRetiene()
    {
        Filtro(Ayer).DebeRetener(Sesion(DateTime.UtcNow), "/Tableros/Index")
            .Should().BeFalse();
    }

    [Fact]
    public void UnaPeticionAnonimaNoSeRetiene()
    {
        Filtro(Ayer).DebeRetener(Sesion(cambiadoEl: null, autenticada: false), "/Tableros/Index")
            .Should().BeFalse("sin sesión no hay a quién exigirle nada; de eso se ocupa el login");
    }

    [Theory]
    [InlineData("/Cuenta/CambioObligatorio")]   // si no, se muerde la cola
    [InlineData("/Cuenta/Login")]
    [InlineData("/Cuenta/Logout")]
    [InlineData("/Cuenta/Denegado")]
    [InlineData("/Cuenta/AyudaCertificado")]
    [InlineData("/Error")]
    [InlineData("/Asistencia/Registro")]        // auto-registro público por QR
    public void LasPaginasExentasSiguenAbiertas(string pagina)
    {
        Filtro(Ayer).DebeRetener(Sesion(cambiadoEl: null), pagina).Should().BeFalse();
    }

    [Theory]
    [InlineData("/Cuenta/Perfil")]
    [InlineData("/Cuenta/Contrasena")]
    [InlineData("/Expedientes/Index")]
    public void TodoLoDemasSeRetiene(string pagina)
    {
        Filtro(Ayer).DebeRetener(Sesion(cambiadoEl: null), pagina)
            .Should().BeTrue("el perfil incluido: dejarlo abierto sería una exención sin motivo");
    }

    [Fact]
    public void UnClaimIlegibleCuentaComoPendiente()
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(AppClaims.PasswordCambiadoEl, "no-es-una-fecha")], "Pruebas"))
        };

        Filtro(Ayer).DebeRetener(http, "/Tableros/Index")
            .Should().BeTrue("ante un dato que no se entiende, se retiene: falla cerrado");
    }
}

/// <summary>
/// El cableado real: que el filtro esté registrado en el pipeline de Razor Pages y que la
/// retención termine en la pantalla de cambio.
/// </summary>
public sealed class CambioPasswordObligatorioPortalTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new()
    {
        CorteCambioPassword = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd")
    };

    public Task InitializeAsync() => _portal.PrepararAsync();

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private HttpClient ClientePendiente()
    {
        var cliente = _portal.ClienteComo("Administrador");
        cliente.DefaultRequestHeaders.Add(
            TestAuthHandler.CabeceraPasswordCambiadoEl, TestAuthHandler.SinCambio);
        return cliente;
    }

    [Fact]
    public async Task AlUsuarioPendienteLoMandanAlaPantallaDeCambio()
    {
        var respuesta = await ClientePendiente().GetAsync("/Cuenta/Perfil");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Redirect);
        respuesta.Headers.Location!.ToString()
            .Should().Contain("/Cuenta/CambioObligatorio");
    }

    [Fact]
    public async Task LaPantallaDeCambioNoSeRedirigeASiMisma()
    {
        var respuesta = await ClientePendiente().GetAsync("/Cuenta/CambioObligatorio");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task QuienYaCambioLaPasswordPasaDeLargo()
    {
        var respuesta = await _portal.ClienteComo("Administrador").GetAsync("/Cuenta/Perfil");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LaPantallaDeCambioRechazaAQuienYaCumplio()
    {
        // Sin esta guarda quedaría accesible por URL como un cambio de contraseña que no pide
        // la actual.
        var respuesta = await _portal.ClienteComo("Administrador").GetAsync("/Cuenta/CambioObligatorio");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Redirect);
        respuesta.Headers.Location!.ToString().Should().Contain("/Tableros");
    }
}

/// <summary>
/// Antes del corte el portal se comporta como si esto no existiera. Va en su propia clase
/// porque necesita un host con otra fecha configurada.
/// </summary>
public sealed class CambioPasswordObligatorioAntesDelCorteTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new()
    {
        CorteCambioPassword = DateTime.Now.AddDays(30).ToString("yyyy-MM-dd")
    };

    public Task InitializeAsync() => _portal.PrepararAsync();

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AntesDelCorteNadieEsRetenido()
    {
        var cliente = _portal.ClienteComo("Administrador");
        cliente.DefaultRequestHeaders.Add(
            TestAuthHandler.CabeceraPasswordCambiadoEl, TestAuthHandler.SinCambio);

        var respuesta = await cliente.GetAsync("/Cuenta/Perfil");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
