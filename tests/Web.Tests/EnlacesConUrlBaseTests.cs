using System.Text.RegularExpressions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>Captura los correos en vez de enviarlos.</summary>
public sealed class CorreosCapturados : IEmailService
{
    public List<(string Para, string Cuerpo, TipoCorreo Tipo)> Enviados { get; } = [];

    public Task SendEmailAsync(string to, string subject, string bodyHtml, TipoCorreo tipo, CancellationToken ct = default)
    {
        lock (Enviados) Enviados.Add((to, bodyHtml, tipo));
        return Task.CompletedTask;
    }
}

/// <summary>
/// S5 (2026-10-22): el enlace de recuperación de contraseña se armaba con la dirección de la
/// petición (<c>Request.Host</c>), que la manda quien hace la petición. Bastaba con pedir la
/// recuperación de otra persona con un host propio para que su correo llevara un enlace a ese
/// host, con su token. Ahora el enlace sale solo de <c>Correo:UrlBase</c>; si falta, no se envía.
/// </summary>
public abstract class RecuperacionBase : IAsyncLifetime
{
    protected const string HostManipulado = "malicioso.example";
    protected const string Correo = "jefearea@pruebas.gob.hn";

    protected readonly PortalFactory Portal = new();
    protected readonly CorreosCapturados Correos = new();

    protected RecuperacionBase(string? urlBase)
    {
        if (urlBase is not null) Portal.Configuracion["Correo:UrlBase"] = urlBase;
        Portal.ServiciosDePrueba = s =>
        {
            s.RemoveAll<IEmailService>();
            s.AddSingleton<IEmailService>(Correos);
        };
    }

    public Task InitializeAsync() => Portal.PrepararAsync();

    public Task DisposeAsync()
    {
        Portal.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Pide la recuperación como lo haría cualquiera desde afuera, pero mandando un host
    /// propio en la petición.</summary>
    protected async Task<string> PedirRecuperacionAsync()
    {
        var cliente = Portal.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri($"http://{HostManipulado}/"),
        });

        var html  = await cliente.GetStringAsync("/Cuenta/OlvidarPassword");
        var token = Regex.Match(html, """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;
        token.Should().NotBeEmpty();

        var r = await cliente.PostAsync("/Cuenta/OlvidarPassword", new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", token),
            new("Correo", Correo),
        ]));
        return System.Net.WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());
    }
}

public sealed class RecuperacionConUrlBaseTests() : RecuperacionBase("https://portal.diger.prueba/")
{
    [Fact]
    public async Task El_enlace_sale_de_la_url_base_y_no_del_host_de_la_peticion()
    {
        await PedirRecuperacionAsync();

        var (para, cuerpo, tipo) = Correos.Enviados.Should().ContainSingle().Subject;
        para.Should().Be(Correo);
        tipo.Should().Be(TipoCorreo.RecuperacionContrasena);
        cuerpo.Should().NotContain(HostManipulado);
        cuerpo.Should().Contain("https://portal.diger.prueba/Cuenta/RestablecerPassword?token=");
    }
}

public sealed class RecuperacionSinUrlBaseTests() : RecuperacionBase(urlBase: null)
{
    [Fact]
    public async Task Sin_url_base_no_se_envia_y_la_pantalla_mantiene_su_mensaje_generico()
    {
        var html = await PedirRecuperacionAsync();

        Correos.Enviados.Should().BeEmpty("sin una dirección confiable no hay enlace que mandar");
        html.Should().Contain("Si el correo electrónico está registrado en el sistema",
            "la pantalla no revela si el correo existe ni si la instalación está bien configurada");
    }
}

/// <summary>S5, el QR de asistencia: usa <c>Correo:UrlBase</c> si existe; si no, sigue como hoy,
/// con la dirección de la petición.</summary>
public abstract class QrAsistenciaBase : IAsyncLifetime
{
    protected readonly PortalFactory Portal = new();
    private int _reunionId;

    protected QrAsistenciaBase(string? urlBase)
    {
        if (urlBase is not null) Portal.Configuracion["Correo:UrlBase"] = urlBase;
    }

    public async Task InitializeAsync()
    {
        await Portal.PrepararAsync();
        await Portal.OtorgarAsync("JefeArea", "Reuniones.Ver");

        using var scope = Portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var r = Reunion.Crear("Reunión del QR");
        r.InstitucionId      = "DIGER";
        r.InstitucionDuenaId = "DIGER";
        r.AreaId   = PortalFactory.AreaDeCasa;
        r.UnidadId = PortalFactory.UnidadDeCasa;
        db.Reuniones.Add(r);
        await db.SaveChangesAsync();
        _reunionId = r.Id;
    }

    public Task DisposeAsync()
    {
        Portal.Dispose();
        return Task.CompletedTask;
    }

    protected async Task<string> EnlacePublicoAsync()
    {
        var html = await Portal.ClienteComo("JefeArea", area: PortalFactory.AreaDeCasa, unidad: PortalFactory.UnidadDeCasa)
            .GetStringAsync($"/Reuniones/Asistencia/{_reunionId}");
        var enlace = Regex.Match(html, """id="urlBox" value="([^"]+)""").Groups[1].Value;
        enlace.Should().NotBeEmpty();
        return System.Net.WebUtility.HtmlDecode(enlace);
    }
}

public sealed class QrAsistenciaConUrlBaseTests() : QrAsistenciaBase("https://portal.diger.prueba")
{
    [Fact]
    public async Task El_enlace_del_QR_usa_la_url_base()
    {
        (await EnlacePublicoAsync()).Should().StartWith("https://portal.diger.prueba/Asistencia/Registro/");
    }
}

public sealed class QrAsistenciaSinUrlBaseTests() : QrAsistenciaBase(urlBase: null)
{
    [Fact]
    public async Task Sin_url_base_el_enlace_del_QR_sigue_como_hoy()
    {
        (await EnlacePublicoAsync()).Should().StartWith("http://localhost/Asistencia/Registro/");
    }
}
