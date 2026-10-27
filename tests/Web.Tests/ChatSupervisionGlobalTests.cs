using System.Net;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// La bandeja /Chat para el alcance Global: todas las conversaciones —en cola, las que atienden
/// otros técnicos y las cerradas—, cada una con quién la atiende. Lo ajeno se abre en solo
/// lectura, y el servidor lo sostiene: resolver el chat de otro no lo cierra.
/// </summary>
public sealed class ChatSupervisionGlobalTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _deEmpleado, _cerrado;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var activo = ChatSesion.Iniciar(Guid.NewGuid(), "Marta Solicitante");
        activo.AsignarTecnico(_portal.UsuarioDe("Empleado"), "Técnica Empleada");
        activo.AgregarMensaje("No puedo entrar al portal", esDelTecnico: false, esSistema: false, "Marta Solicitante");

        var cerrado = ChatSesion.Iniciar(Guid.NewGuid(), "Pedro Resuelto");
        cerrado.AsignarTecnico(_portal.UsuarioDe("JefeArea"), "Jefe Atento");
        cerrado.Cerrar(ChatEstado.Resuelto);

        db.ChatSesiones.AddRange(activo, cerrado, ChatSesion.Iniciar(Guid.NewGuid(), "Lucía EnCola"));
        await db.SaveChangesAsync();
        _deEmpleado = activo.Id;
        _cerrado    = cerrado.Id;
    }

    // Razor codifica los acentos (&#xE9;): se compara contra el texto que lee la persona.
    private async Task<string> LeerAsync(string rol, string ruta) =>
        WebUtility.HtmlDecode(await _portal.ClienteComo(rol).GetStringAsync(ruta));

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task El_Global_ve_todas_las_conversaciones_con_quien_las_atiende()
    {
        var html = await LeerAsync("Administrador", "/Chat");

        html.Should().Contain("Lucía EnCola").And.Contain("Sin asignar");
        html.Should().Contain("Activos de otros técnicos")
            .And.Contain("Marta Solicitante").And.Contain("Atiende: Técnica Empleada");
        html.Should().Contain("Cerrados recientes")
            .And.Contain("Pedro Resuelto").And.Contain("Atendió: Jefe Atento");
    }

    [Fact]
    public async Task Un_tecnico_sin_alcance_Global_sigue_viendo_solo_la_cola_y_lo_suyo()
    {
        await _portal.OtorgarAsync("JefeArea", "Chat.Ver");
        var html = await LeerAsync("JefeArea", "/Chat");

        html.Should().NotContain("Activos de otros técnicos");
        html.Should().NotContain("Marta Solicitante");
        html.Should().NotContain("Pedro Resuelto");
    }

    [Fact]
    public async Task El_chat_de_otro_se_abre_en_solo_lectura_con_la_opcion_de_tomarlo()
    {
        var html = await LeerAsync("Administrador", $"/Chat?sesion={_deEmpleado}");

        html.Should().Contain("No puedo entrar al portal");
        html.Should().Contain("Lo atiende Técnica Empleada · solo lectura");
        html.Should().Contain("Tomar para responder");
        html.Should().NotContain("id=\"txtRespuesta\"");
        html.Should().NotContain("id=\"frmResolver\"");
    }

    [Fact]
    public async Task Un_chat_cerrado_se_lee_y_no_se_puede_tomar()
    {
        var html = await LeerAsync("Administrador", $"/Chat?sesion={_cerrado}");

        html.Should().Contain("solo lectura");
        html.Should().NotContain("Tomar para responder");
        html.Should().NotContain("id=\"txtRespuesta\"");
    }

    [Fact]
    public async Task Resolver_el_chat_de_otro_no_lo_cierra()
    {
        var cliente = _portal.ClienteComo("Administrador");
        var pagina  = await cliente.GetStringAsync($"/Chat?sesion={_deEmpleado}");
        var token   = Regex.Match(pagina, """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

        var r = await cliente.PostAsync("/Chat?handler=Cerrar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["sesionId"] = _deEmpleado.ToString(),
        }));

        r.StatusCode.Should().Be(HttpStatusCode.Redirect);
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ChatSesiones.AsNoTracking().SingleAsync(s => s.Id == _deEmpleado))
            .Estado.Should().Be(ChatEstado.Activo, "resolver es de quien atiende; el Global tiene que tomarlo antes");
    }

    [Fact]
    public async Task El_Global_toma_el_chat_de_otro_y_queda_dicho_en_la_conversacion()
    {
        var cliente = _portal.ClienteComo("Administrador");
        var pagina  = await cliente.GetStringAsync($"/Chat?sesion={_deEmpleado}");
        var token   = Regex.Match(pagina, """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

        var r = await cliente.PostAsync("/Chat?handler=Tomar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["sesionId"] = _deEmpleado.ToString(),
        }));
        r.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var despues = WebUtility.HtmlDecode(await cliente.GetStringAsync($"/Chat?sesion={_deEmpleado}"));
        despues.Should().Contain("(lo atendía Técnica Empleada)");
        despues.Should().Contain("id=\"txtRespuesta\"", "ahora lo atiende el Global y puede responder");
    }
}
