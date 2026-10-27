using System.Net;
using System.Net.Http.Json;
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
/// La cola del chat es una sola y la ve toda la bandeja. Antes, al iniciar un chat se elegía a
/// un técnico del tema y solo él recibía el aviso; ahora no se avisa a nadie en particular: el
/// menú cuenta lo que espera, cada quien toma, y jefaturas, PMO y Global reparten.
/// </summary>
public sealed class ColaCompartidaChatTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _enCola;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = ChatSesion.Iniciar(Guid.NewGuid(), "Lucía EnCola");
        db.ChatSesiones.Add(s);
        await db.SaveChangesAsync();
        _enCola = s.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private static string Token(string html) =>
        Regex.Match(html, """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

    private static FormUrlEncodedContent Form(string token, params (string, string)[] campos)
    {
        var datos = new Dictionary<string, string> { ["__RequestVerificationToken"] = token };
        foreach (var (k, v) in campos) datos[k] = v;
        return new FormUrlEncodedContent(datos);
    }

    private async Task<ChatSesion> SesionAsync()
    {
        using var scope = _portal.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .ChatSesiones.AsNoTracking().SingleAsync(x => x.Id == _enCola);
    }

    [Fact]
    public async Task El_menu_dice_cuantos_chats_esperan_en_la_cola()
    {
        var html = await _portal.ClienteComo("Administrador").GetStringAsync("/Tickets");

        Regex.IsMatch(html, """id="navColaChat"[^>]*>1</span>""").Should().BeTrue(
            "hay un chat en cola y el menú lo cuenta junto a «Chat de Soporte»");
    }

    [Fact]
    public async Task Las_jefaturas_ven_el_asignar_y_un_tecnico_comun_no()
    {
        await _portal.OtorgarAsync("JefeArea", "Chat.Ver");
        await _portal.OtorgarAsync("Empleado", "Chat.Ver");

        var jefe     = await _portal.ClienteComo("JefeArea").GetStringAsync("/Chat");
        var empleado = await _portal.ClienteComo("Empleado").GetStringAsync("/Chat");

        jefe.Should().Contain($"data-asignar=\"{_enCola}\"");
        jefe.Should().Contain("Usuario empleado", "el selector ofrece a los técnicos activos");
        empleado.Should().Contain("EnCola<", "la cola es la misma para todos");
        empleado.Should().NotContain("data-asignar=", "un técnico común toma, no reparte");
    }

    /// <summary>La fábrica no siembra PMO: se crea el rol y un usuario con esa asignación.</summary>
    private async Task<HttpClient> ClientePmoAsync()
    {
        Guid pmoId;
        using (var scope = _portal.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Roles.Add(Rol.Crear("PMO", "PMO", NivelAlcance.Area,
                esSupervisor: true, esTecnicoSoporte: true, esPmo: true));
            var u = Usuario.Crear("Usuario pmo", "pmo@pruebas.gob.hn", "hash");
            db.Usuarios.Add(u);
            db.AsignacionesUsuario.Add(AsignacionUsuario.Crear(u.Id, "DIGER", null, null, "PMO"));
            await db.SaveChangesAsync();
            pmoId = u.Id;
            await scope.ServiceProvider
                .GetRequiredService<Diger.TramitesEstado.Application.Common.Interfaces.IRolCatalogo>().RecargarAsync();
        }
        await _portal.OtorgarAsync("PMO", "Chat.Ver", "Chat.Editar");

        var cliente = _portal.ClienteComo("JefeArea");
        cliente.DefaultRequestHeaders.Remove(TestAuthHandler.CabeceraUsuarioId);
        cliente.DefaultRequestHeaders.Remove(TestAuthHandler.CabeceraRol);
        cliente.DefaultRequestHeaders.Add(TestAuthHandler.CabeceraUsuarioId, pmoId.ToString());
        cliente.DefaultRequestHeaders.Add(TestAuthHandler.CabeceraRol, "PMO");
        return cliente;
    }

    [Fact]
    public async Task La_PMO_no_reparte_chats_aunque_su_rol_sea_de_supervision()
    {
        var cliente = await ClientePmoAsync();
        var html    = await cliente.GetStringAsync("/Chat");

        html.Should().Contain("EnCola<", "la PMO sí ve la cola");
        html.Should().NotContain("data-asignar=", "repartir chats no le corresponde a la PMO");

        var r = await cliente.PostAsync("/Chat?handler=Asignar",
            Form(Token(html), ("sesionId", _enCola.ToString()), ("tecnicoId", _portal.UsuarioDe("Empleado").ToString())));

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SesionAsync()).Estado.Should().Be(ChatEstado.EnCola);
    }

    [Fact]
    public async Task Un_tecnico_comun_no_puede_asignar_aunque_arme_el_POST()
    {
        await _portal.OtorgarAsync("Empleado", "Chat.Ver", "Chat.Editar");
        var cliente = _portal.ClienteComo("Empleado");
        var token   = Token(await cliente.GetStringAsync("/Chat"));

        var r = await cliente.PostAsync("/Chat?handler=Asignar",
            Form(token, ("sesionId", _enCola.ToString()), ("tecnicoId", _portal.UsuarioDe("Empleado").ToString())));

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SesionAsync()).Estado.Should().Be(ChatEstado.EnCola);
    }

    [Fact]
    public async Task Una_jefatura_asigna_y_el_tecnico_recibe_el_aviso()
    {
        await _portal.OtorgarAsync("JefeArea", "Chat.Ver", "Chat.Editar");
        var cliente  = _portal.ClienteComo("JefeArea");
        var token    = Token(await cliente.GetStringAsync("/Chat"));
        var empleado = _portal.UsuarioDe("Empleado");

        var r = await cliente.PostAsync("/Chat?handler=Asignar",
            Form(token, ("sesionId", _enCola.ToString()), ("tecnicoId", empleado.ToString())));

        r.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var sesion = await SesionAsync();
        sesion.Estado.Should().Be(ChatEstado.Activo);
        sesion.TecnicoId.Should().Be(empleado);

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Notificaciones.AsNoTracking()
            .AnyAsync(n => n.DestinatarioId == empleado && n.Tipo == TipoNotificacion.ChatRecibido))
            .Should().BeTrue("el chat ya es suyo y tiene que saberlo");
        (await db.ChatMensajes.AsNoTracking()
            .AnyAsync(m => m.SesionId == _enCola && m.EsSistema && m.Texto.Contains("asignó el chat a Usuario empleado")))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Iniciar_un_chat_ya_no_le_avisa_a_ningun_tecnico()
    {
        using (var scope = _portal.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tema = TemaTicket.Crear("Accesos", 0);
            db.TemasTicket.Add(tema);
            await db.SaveChangesAsync();
            db.UsuarioTemas.Add(UsuarioTema.Crear(_portal.UsuarioDe("Empleado"), tema.Id));
            await db.SaveChangesAsync();
        }

        var cliente = _portal.ClienteComo("Administrador");
        var token   = Token(await cliente.GetStringAsync("/Chat"));
        int temaId;
        using (var scope = _portal.Services.CreateScope())
            temaId = (await scope.ServiceProvider.GetRequiredService<AppDbContext>()
                .TemasTicket.AsNoTracking().SingleAsync(t => t.Nombre == "Accesos")).Id;

        var req = new HttpRequestMessage(HttpMethod.Post, "/Chat?handler=IniciarSesion")
        {
            Content = JsonContent.Create(new { temaId, mensaje = "No puedo entrar" }),
        };
        req.Headers.Add("RequestVerificationToken", token);
        var r = await cliente.SendAsync(req);

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        using var check = _portal.Services.CreateScope();
        (await check.ServiceProvider.GetRequiredService<AppDbContext>().Notificaciones.AsNoTracking()
            .AnyAsync(n => n.Tipo == TipoNotificacion.ChatRecibido))
            .Should().BeFalse("el chat entra a la cola común; nadie recibe un aviso individual");
    }
}
