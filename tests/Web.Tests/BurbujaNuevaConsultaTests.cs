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
/// Con un chat abierto la burbuja solo mostraba ese chat: no había cómo empezar otra consulta
/// hasta que el técnico lo resolviera. Ahora el usuario cierra el suyo y la burbuja vuelve al
/// formulario. Cerrar es solo del dueño: el chat de otro no se toca aunque se arme el POST.
/// </summary>
public sealed class BurbujaNuevaConsultaTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _mio;
    private int _ajeno;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mio   = ChatSesion.Iniciar(_portal.UsuarioDe("Administrador"), "Usuario administrador");
        var ajeno = ChatSesion.Iniciar(Guid.NewGuid(), "Otra persona");
        db.ChatSesiones.AddRange(mio, ajeno);
        await db.SaveChangesAsync();
        (_mio, _ajeno) = (mio.Id, ajeno.Id);
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private async Task<HttpResponseMessage> CerrarAsync(int sesionId)
    {
        var cliente = _portal.ClienteComo("Administrador");
        var html    = await cliente.GetStringAsync("/Tickets");
        var token   = Regex.Match(html, """<meta name="csrf-token" content="([^"]+)""").Groups[1].Value;

        var req = new HttpRequestMessage(HttpMethod.Post, $"/Chat?handler=NuevaConsulta&sesionId={sesionId}");
        req.Headers.Add("RequestVerificationToken", token);
        return await cliente.SendAsync(req);
    }

    private async Task<ChatSesion> SesionAsync(int id)
    {
        using var scope = _portal.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .ChatSesiones.AsNoTracking().Include(s => s.Mensajes).SingleAsync(s => s.Id == id);
    }

    [Fact]
    public async Task Con_un_chat_abierto_la_burbuja_ofrece_una_nueva_consulta()
    {
        var html = await _portal.ClienteComo("Administrador").GetStringAsync("/Tickets");

        html.Should().Contain("onclick=\"cwpNuevaConsulta()\"");
    }

    [Fact]
    public async Task El_usuario_cierra_su_chat_y_queda_libre_para_otra_consulta()
    {
        var r = await CerrarAsync(_mio);

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var s = await SesionAsync(_mio);
        s.Estado.Should().Be(ChatEstado.Abandonado);
        s.Mensajes.Should().Contain(m => m.EsSistema && m.Texto.Contains("cerró la conversación"),
            "el técnico que lo atendía tiene que saber por qué se cerró");

        var html = await _portal.ClienteComo("Administrador").GetStringAsync("/Tickets");
        html.Should().NotContain($"data-sesion=\"{_mio}\"",
            "el chat cerrado ya no ocupa la burbuja: vuelve el formulario de inicio");
    }

    [Fact]
    public async Task No_se_cierra_el_chat_de_otro()
    {
        var r = await CerrarAsync(_ajeno);

        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SesionAsync(_ajeno)).Estado.Should().Be(ChatEstado.EnCola);
    }
}
