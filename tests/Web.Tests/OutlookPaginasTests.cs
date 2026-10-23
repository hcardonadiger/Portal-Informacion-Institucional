using System.Net;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Application.Reuniones.Outlook;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>El retorno de Microsoft solo se acepta con el estado que la propia sesión emitió
/// (anti-CSRF); el perfil apaga la tarjeta cuando el servidor no tiene la app registrada.
/// En las reuniones: el editor envía la cita a Outlook, y borrar no se bloquea por Microsoft.</summary>
public sealed class OutlookPaginasTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private readonly IConexionMicrosoft _conexion = Substitute.For<IConexionMicrosoft>();
    private readonly IOutlookCalendario _calendario = Substitute.For<IOutlookCalendario>();

    public async Task InitializeAsync()
    {
        _portal.ServiciosDePrueba = s =>
        {
            s.RemoveAll<IConexionMicrosoft>();
            s.AddScoped(_ => _conexion);
            s.RemoveAll<IOutlookCalendario>();
            s.AddScoped(_ => _calendario);
        };
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Reuniones.Ver", "Reuniones.Editar", "Reuniones.Eliminar");
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

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        resp.Headers.Location!.ToString().Should().Contain("/Cuenta/Login");
        await _conexion.DidNotReceiveWithAnyArgs().CompletarAsync(default, default!, default!, default!, default);
    }

    /// <summary>AllowAnonymousToFolder("/Cuenta") gana sobre el [Authorize] de Perfil, así que sin
    /// la guarda del handler un POST anónimo llegaba a él. Con un token antiforgery anónimo válido
    /// —el del login, que cualquiera obtiene— el antiforgery no lo frena: lo frena la guarda.</summary>
    [Theory]
    [InlineData("DesconectarOutlook")]
    [InlineData("ConectarOutlook")]
    public async Task Sin_sesion_los_handlers_de_Outlook_del_perfil_mandan_al_login(string handler)
    {
        _conexion.EstaConfigurada.Returns(true);
        _conexion.IniciarAsync(default!, default).ReturnsForAnyArgs(
            new InicioConexionDto("https://login.microsoftonline.com/x/authorize", "E", "V"));
        var anonimo = _portal.ClienteAnonimo();
        var login = await anonimo.GetStringAsync("/Cuenta/Login");
        var token = Regex.Match(login, """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;
        token.Should().NotBeEmpty();

        var resp = await anonimo.PostAsync($"/Cuenta/Perfil?handler={handler}",
            new FormUrlEncodedContent([new KeyValuePair<string, string>("__RequestVerificationToken", token)]));

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        resp.Headers.Location!.ToString().Should().Contain("/Cuenta/Login");
        await _conexion.DidNotReceiveWithAnyArgs().IniciarAsync(default!, default);
    }

    [Fact]
    public async Task Un_estado_distinto_no_canjea_y_no_rompe_la_conexion_en_curso()
    {
        var cliente = await ConexionIniciadaAsync();

        var falsa = await cliente.GetAsync("/Cuenta/OutlookCallback?code=abc&state=OTRO");

        falsa.StatusCode.Should().Be(HttpStatusCode.Redirect);
        falsa.Headers.Location!.ToString().Should().Contain("/Cuenta/Perfil");
        await _conexion.DidNotReceiveWithAnyArgs().CompletarAsync(default, default!, default!, default!, default);

        // La respuesta real, que llega después del enlace falsificado, todavía canjea.
        var real = await cliente.GetAsync("/Cuenta/OutlookCallback?code=abc&state=ESTADO-OK");

        real.StatusCode.Should().Be(HttpStatusCode.Redirect);
        await _conexion.Received(1).CompletarAsync(
            _portal.UsuarioDe("Empleado"), "abc", "VERIF-OK", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task El_estado_correcto_canjea_y_vuelve_al_perfil()
    {
        var cliente = await ConexionIniciadaAsync();

        var resp = await cliente.GetAsync("/Cuenta/OutlookCallback?code=abc&state=ESTADO-OK");

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        resp.Headers.Location!.ToString().Should().Contain("/Cuenta/Perfil");
        await _conexion.Received(1).CompletarAsync(
            _portal.UsuarioDe("Empleado"), "abc", "VERIF-OK", Arg.Any<string>(), Arg.Any<CancellationToken>());

        // El estado se gastó: repetir la misma URL no vuelve a canjear.
        var repetida = await cliente.GetAsync("/Cuenta/OutlookCallback?code=abc&state=ESTADO-OK");

        repetida.StatusCode.Should().Be(HttpStatusCode.Redirect);
        repetida.Headers.Location!.ToString().Should().Contain("/Cuenta/Perfil");
        await _conexion.ReceivedWithAnyArgs(1).CompletarAsync(default, default!, default!, default!, default);
    }

    [Fact]
    public async Task Un_error_de_Microsoft_no_se_refleja()
    {
        var cliente = await ConexionIniciadaAsync();

        var resp = await cliente.GetAsync("/Cuenta/OutlookCallback?error=%3Cb%3Einventado%3C%2Fb%3E&state=ESTADO-OK");

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        resp.Headers.Location!.ToString().Should().Contain("/Cuenta/Perfil");

        // El aviso sale por una expresión Razor, que codifica los acentos: se decodifica para leerlo.
        var html = WebUtility.HtmlDecode(await cliente.GetStringAsync("/Cuenta/Perfil"));

        html.Should().NotContain("inventado");
        html.Should().Contain("Microsoft no completó la conexión");
        await _conexion.DidNotReceiveWithAnyArgs().CompletarAsync(default, default!, default!, default!, default);
    }

    // ── Reuniones: editor, listado ────────────────────────────────

    private async Task<int> SembrarReunionAsync(bool conectada)
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Diger.TramitesEstado.Infrastructure.Persistence.AppDbContext>();
        var r = Diger.TramitesEstado.Domain.Entities.Reunion.Crear("Mesa técnica");
        r.InstitucionId = "DIGER"; r.InstitucionDuenaId = "DIGER";
        r.AreaId = PortalFactory.AreaDeCasa; r.UnidadId = PortalFactory.UnidadDeCasa;
        r.Fecha = new DateOnly(2026, 10, 23); r.Hora = "09:00";
        db.Reuniones.Add(r);
        if (conectada)
            db.ConexionesMicrosoft.Add(Diger.TramitesEstado.Domain.Entities.ConexionMicrosoft.Crear(
                _portal.UsuarioDe("JefeArea"), "jefe@diger.gob.hn", "h", [1], DateTime.UtcNow));
        await db.SaveChangesAsync();
        return r.Id;
    }

    private HttpClient Jefe() => _portal.ClienteComo("JefeArea", "DIGER", PortalFactory.AreaDeCasa, PortalFactory.UnidadDeCasa);

    private static async Task<string> TokenAsync(HttpClient c, string url)
    {
        var html = await c.GetStringAsync(url);
        var m = System.Text.RegularExpressions.Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        return m.Groups[1].Value;
    }

    [Fact]
    public async Task El_editor_ofrece_enviar_con_Teams_marcado_si_el_usuario_esta_conectado()
    {
        _conexion.EstaConfigurada.Returns(true);
        var id = await SembrarReunionAsync(conectada: true);

        var html = await Jefe().GetStringAsync($"/Reuniones/Editor?id={id}");

        html.Should().Contain("Enviar a Outlook");
        html.Should().MatchRegex("name=\"conTeams\"[^>]*checked");
    }

    [Fact]
    public async Task Enviar_desde_el_editor_deja_la_reunion_en_Outlook()
    {
        _conexion.EstaConfigurada.Returns(true);
        _calendario.CrearAsync(default, default!, default, default).ReturnsForAnyArgs(new ResultadoOutlook("AAMk-9", "https://teams/z"));
        var id = await SembrarReunionAsync(conectada: true);
        var c = Jefe();
        var token = await TokenAsync(c, $"/Reuniones/Editor?id={id}");

        var resp = await c.PostAsync($"/Reuniones/Editor?id={id}&handler=EnviarOutlook", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["conTeams"] = "true"
        }));

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Diger.TramitesEstado.Infrastructure.Persistence.AppDbContext>();
        // IgnoreQueryFilters: este scope no tiene sesión, y el filtro de alcance lo dejaría sin filas.
        (await db.Reuniones.IgnoreQueryFilters().SingleAsync(r => r.Id == id)).OutlookEventId.Should().Be("AAMk-9");
    }

    [Fact]
    public async Task Un_fallo_inesperado_al_enviar_muestra_aviso_y_no_500()
    {
        _conexion.EstaConfigurada.Returns(true);
        _calendario.CrearAsync(default, default!, default, default)
            .ThrowsAsyncForAnyArgs(new InvalidOperationException("x"));
        var id = await SembrarReunionAsync(conectada: true);
        var c = Jefe();
        var token = await TokenAsync(c, $"/Reuniones/Editor?id={id}");

        var resp = await c.PostAsync($"/Reuniones/Editor?id={id}&handler=EnviarOutlook", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["conTeams"] = "true"
        }));

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        // WebExceptionHandler también responde 302, pero hacia /Error?code=500: el código solo no basta.
        resp.Headers.Location!.ToString().Should().NotContain("/Error");
        // El aviso sale por una expresión Razor, que codifica los acentos: se decodifica para leerlo.
        WebUtility.HtmlDecode(await c.GetStringAsync($"/Reuniones/Editor?id={id}"))
            .Should().Contain("No se pudo enviar a Outlook");
    }

    [Fact]
    public async Task Borrar_una_reunion_cuya_cita_no_se_pudo_cancelar_la_borra_igual()
    {
        _conexion.EstaConfigurada.Returns(true);
        _calendario.CrearAsync(default, default!, default, default).ReturnsForAnyArgs(new ResultadoOutlook("AAMk-9", null));
        _calendario.CancelarAsync(default, default!, default!, default)
            .ThrowsAsyncForAnyArgs(new OutlookNoDisponibleException("503"));
        var id = await SembrarReunionAsync(conectada: true);
        var c = Jefe();
        var token = await TokenAsync(c, $"/Reuniones/Editor?id={id}");
        await c.PostAsync($"/Reuniones/Editor?id={id}&handler=EnviarOutlook", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["__RequestVerificationToken"] = token, ["conTeams"] = "true" }));

        var tokenIndex = await TokenAsync(c, "/Reuniones");
        var resp = await c.PostAsync($"/Reuniones?handler=Eliminar&id={id}", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["__RequestVerificationToken"] = tokenIndex }));

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        await _calendario.ReceivedWithAnyArgs(1).CancelarAsync(default, default!, default!, default);
        var lista = WebUtility.HtmlDecode(await c.GetStringAsync("/Reuniones"));
        lista.Should().Contain("Reunión eliminada.").And.Contain("a mano");
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Diger.TramitesEstado.Infrastructure.Persistence.AppDbContext>();
        (await db.Reuniones.IgnoreQueryFilters().SingleAsync(r => r.Id == id)).IsDeleted
            .Should().BeTrue("el portal no bloquea un borrado por Microsoft");
    }

    [Fact]
    public async Task Desmarcar_Teams_envia_sin_Teams()
    {
        _conexion.EstaConfigurada.Returns(true);
        _calendario.CrearAsync(default, default!, default, default).ReturnsForAnyArgs(new ResultadoOutlook("AAMk-9", null));
        var id = await SembrarReunionAsync(conectada: true);
        var c = Jefe();
        var token = await TokenAsync(c, $"/Reuniones/Editor?id={id}");

        // Casilla desmarcada: el navegador no manda el campo conTeams.
        var resp = await c.PostAsync($"/Reuniones/Editor?id={id}&handler=EnviarOutlook", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["__RequestVerificationToken"] = token }));

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        await _calendario.Received(1).CrearAsync(
            Arg.Any<Guid>(), Arg.Is<EventoOutlook>(e => !e.EsTeams), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Si_la_sincronizacion_revienta_tras_guardar_el_guardado_sigue_siendo_exitoso()
    {
        _conexion.EstaConfigurada.Returns(true);
        _calendario.CrearAsync(default, default!, default, default).ReturnsForAnyArgs(new ResultadoOutlook("AAMk-9", null));
        var id = await SembrarReunionAsync(conectada: true);
        var c = Jefe();
        var token = await TokenAsync(c, $"/Reuniones/Editor?id={id}");
        await c.PostAsync($"/Reuniones/Editor?id={id}&handler=EnviarOutlook", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["__RequestVerificationToken"] = token, ["conTeams"] = "true" }));

        // La reunión la creó otra persona. Al volverla privada, el guardado la saca del alcance de
        // quien edita: ya confirmado el cambio, la sincronización no la encuentra (NotFoundException).
        using (var scope = _portal.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Diger.TramitesEstado.Infrastructure.Persistence.AppDbContext>();
            var r = await db.Reuniones.IgnoreQueryFilters().SingleAsync(x => x.Id == id);
            r.OutlookEventId.Should().Be("AAMk-9", "la reunión tiene que estar ya en Outlook");
            r.CreadoPorId = _portal.UsuarioDe("Empleado");
            await db.SaveChangesAsync();
        }

        token = await TokenAsync(c, $"/Reuniones/Editor?id={id}");
        var resp = await c.PostAsync($"/Reuniones/Editor?id={id}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Datos.Titulo"] = "Mesa técnica (editada)",
            ["Datos.Visibilidad"] = "Privada",
            ["Datos.Fecha"] = "2026-10-23",
            ["Datos.Hora"] = "09:00"
        }));

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        resp.Headers.Location!.ToString().Should().NotContain("/Error");
        using var verif = _portal.Services.CreateScope();
        var dbv = verif.ServiceProvider.GetRequiredService<Diger.TramitesEstado.Infrastructure.Persistence.AppDbContext>();
        (await dbv.Reuniones.IgnoreQueryFilters().SingleAsync(x => x.Id == id)).Titulo
            .Should().Be("Mesa técnica (editada)", "el guardado se confirmó antes de que Outlook fallara");
        WebUtility.HtmlDecode(await c.GetStringAsync("/Reuniones"))
            .Should().Contain("Reunión actualizada.").And.Contain("no se pudo actualizar en Outlook");
    }

    /// <summary>Pulsa «Conectar mi Outlook» como Empleado: deja el estado y el verificador en el
    /// TempData de ese cliente (el cliente de la fábrica conserva las cookies).</summary>
    private async Task<HttpClient> ConexionIniciadaAsync()
    {
        _conexion.EstaConfigurada.Returns(true);
        _conexion.IniciarAsync(default!, default).ReturnsForAnyArgs(
            new InicioConexionDto("https://login.microsoftonline.com/x/authorize", "ESTADO-OK", "VERIF-OK"));

        var cliente = _portal.ClienteComo("Empleado");
        var perfil  = await cliente.GetStringAsync("/Cuenta/Perfil");
        var token   = Regex.Match(perfil,
            """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;
        token.Should().NotBeEmpty();

        var resp = await cliente.PostAsync("/Cuenta/Perfil?handler=ConectarOutlook",
            new FormUrlEncodedContent([new KeyValuePair<string, string>("__RequestVerificationToken", token)]));

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        resp.Headers.Location!.ToString().Should().Be("https://login.microsoftonline.com/x/authorize");
        return cliente;
    }
}
