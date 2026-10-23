using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>El Panel de control de notificaciones servido de verdad: permisos, que la contraseña
/// nunca vuelva al navegador y que el menú solo ofrezca el grupo a quien puede verlo.</summary>
public sealed class PanelControlNotificacionesTests : IAsyncLifetime
{
    private const string Ruta = "/PanelControl/Notificaciones";
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("Consultor", "PanelControl.Notificaciones.Ver");
        await _portal.OtorgarAsync("JefeArea", "PanelControl.Notificaciones.Ver", "PanelControl.Notificaciones.Editar");
    }

    public Task DisposeAsync() { _portal.Dispose(); return Task.CompletedTask; }

    private static string Token(string html) =>
        Regex.Match(html, """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

    private static FormUrlEncodedContent Form(string token, params (string, string)[] campos) =>
        new([new("__RequestVerificationToken", token), .. campos.Select(c => new KeyValuePair<string, string>(c.Item1, c.Item2))]);

    [Fact]
    public async Task Sin_permiso_se_niega_y_el_menu_no_lo_ofrece()
    {
        (await _portal.ClienteComo("Empleado").GetAsync(Ruta)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var html = await _portal.ClienteComo("Empleado").GetStringAsync("/Notificaciones");
        html.Should().NotContain("href=\"/PanelControl");
    }

    [Fact]
    public async Task Con_Ver_se_ve_deshabilitado_y_no_guarda()
    {
        var cliente = _portal.ClienteComo("Consultor");
        var html = await cliente.GetStringAsync(Ruta);
        html.Should().Contain("Revisión automática").And.Contain("disabled");

        var post = await cliente.PostAsync(Ruta + "?handler=Revision", Form(Token(html), ("Horas", "0"), ("Minutos", "15")));
        post.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Con_Editar_se_guarda_la_revision()
    {
        var cliente = _portal.ClienteComo("JefeArea");
        var html = await cliente.GetStringAsync(Ruta);

        var post = await cliente.PostAsync(Ruta + "?handler=Revision", Form(Token(html), ("Horas", "0"), ("Minutos", "15")));
        post.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var despues = await cliente.GetStringAsync(Ruta);
        despues.Should().Contain("name=\"Minutos\"").And.Contain("value=\"15\"");
    }

    [Fact]
    public async Task La_contrasena_guardada_nunca_vuelve_al_navegador()
    {
        var cliente = _portal.ClienteComo("JefeArea");
        var html = await cliente.GetStringAsync(Ruta);

        var post = await cliente.PostAsync(Ruta + "?handler=Correo", Form(Token(html),
            ("Correo.Host", "smtp.office365.com"), ("Correo.Puerto", "587"), ("Correo.Ssl", "true"),
            ("Correo.Usuario", "no-reply@diger.gob.hn"), ("Correo.Password", "Secreta#123"),
            ("Correo.Remitente", "no-reply@diger.gob.hn")));
        post.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var despues = await cliente.GetStringAsync(Ruta);
        despues.Should().NotContain("Secreta#123");
        despues.Should().Contain("Hay una contraseña guardada");
    }

    [Fact]
    public async Task Un_rechazo_conserva_lo_escrito()
    {
        var cliente = _portal.ClienteComo("JefeArea");
        var html = await cliente.GetStringAsync(Ruta);

        var post = await cliente.PostAsync(Ruta + "?handler=Correo", Form(Token(html),
            ("Correo.Host", "smtp.office365.com"), ("Correo.Puerto", "587"),
            ("Correo.Usuario", "no-reply@diger.gob.hn"), ("Correo.Remitente", "no-reply@diger.gob.hn")));

        post.StatusCode.Should().Be(HttpStatusCode.OK);
        var cuerpo = await post.Content.ReadAsStringAsync();
        cuerpo.Should().Contain("smtp.office365.com").And.Contain("escriba la contrase");
    }
}
