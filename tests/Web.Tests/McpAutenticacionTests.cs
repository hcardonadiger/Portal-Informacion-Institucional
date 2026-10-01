using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// La puerta del endpoint /mcp.
///
/// <para>Lo que más se cuida acá es que un 401 sea un 401. El esquema por defecto del portal es
/// la cookie, y su respuesta a una petición sin credencial es un 302 al HTML de
/// <c>/Cuenta/Login</c>. Para un navegador eso está bien; para un cliente MCP es una respuesta
/// corrupta en lugar de «su credencial no sirve», y el usuario termina viendo un error de
/// protocolo en vez de uno de autenticación.</para>
/// </summary>
public sealed class McpAutenticacionTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private readonly TokenMcpHasher _hasher = new();

    public async Task InitializeAsync() => await _portal.PrepararAsync();

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private static HttpRequestMessage Peticion(string? token)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""",
                Encoding.UTF8, "application/json")
        };

        msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        if (token is not null)
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return msg;
    }

    /// <summary>Siembra un token y devuelve su valor completo.</summary>
    private async Task<string> SembrarTokenAsync(
        bool revocado = false, DateTime? expira = null, bool usuarioActivo = true)
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var usuario = Usuario.Crear("Dueño del token", $"{Guid.NewGuid():N}@diger.gob.hn", "hash");
        if (!usuarioActivo) usuario.Desactivar();
        db.Usuarios.Add(usuario);

        var asignacion = AsignacionUsuario.Crear(usuario.Id, "DIGER", null, null, "Empleado");
        db.AsignacionesUsuario.Add(asignacion);

        var (token, prefijo, hash) = _hasher.Generar();
        var fila = TokenAccesoMcp.Crear(usuario.Id, asignacion.Id, "Prueba", prefijo, hash, expira);
        if (revocado) fila.Revocar();

        db.TokensAccesoMcp.Add(fila);
        await db.SaveChangesAsync();

        return token;
    }

    [Fact]
    public async Task Sin_cabecera_de_autorizacion_responde_401_y_no_un_redirect_al_login()
    {
        var respuesta = await _portal.ClienteAnonimo().SendAsync(Peticion(null));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        respuesta.StatusCode.Should().NotBe(HttpStatusCode.Redirect,
            "el challenge de la cookie mandaría el HTML del login y el cliente MCP no sabe leerlo");
        respuesta.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task Un_token_inexistente_no_entra()
    {
        var (inventado, _, _) = _hasher.Generar();

        var respuesta = await _portal.ClienteAnonimo().SendAsync(Peticion(inventado));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("no-tiene-forma-de-token")]
    [InlineData("pdt.solodospartes")]
    [InlineData("otro.prefijo.secreto")]
    public async Task Un_token_con_forma_invalida_no_entra(string token)
    {
        var respuesta = await _portal.ClienteAnonimo().SendAsync(Peticion(token));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Un_token_revocado_no_entra()
    {
        var token = await SembrarTokenAsync(revocado: true);

        var respuesta = await _portal.ClienteAnonimo().SendAsync(Peticion(token));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Un_token_de_una_cuenta_desactivada_no_entra()
    {
        var token = await SembrarTokenAsync(usuarioActivo: false);

        var respuesta = await _portal.ClienteAnonimo().SendAsync(Peticion(token));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "dar de baja a alguien tiene que cortar sus tokens sin que nadie los revoque a mano");
    }

    /// <summary>El secreto correcto es lo único que abre: el prefijo está en claro en la base
    /// y en la pantalla, así que presentarlo no puede alcanzar.</summary>
    [Fact]
    public async Task El_prefijo_por_si_solo_no_abre_nada()
    {
        var token   = await SembrarTokenAsync();
        var prefijo = _hasher.LeerPrefijo(token)!;

        var respuesta = await _portal.ClienteAnonimo().SendAsync(Peticion($"pdt.{prefijo}.loquesea"));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Un_token_valido_pasa_la_autenticacion()
    {
        var token = await SembrarTokenAsync();

        var respuesta = await _portal.ClienteAnonimo().SendAsync(Peticion(token));

        respuesta.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        respuesta.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    /// <summary>Entrar deja rastro: sirve para detectar tokens muertos y para saber cuál se
    /// está usando cuando hay que revocar uno.</summary>
    [Fact]
    public async Task Usar_un_token_registra_su_ultimo_uso()
    {
        var token = await SembrarTokenAsync();

        using (var scope = _portal.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.TokensAccesoMcp.AsNoTracking().SingleAsync()).UltimoUsoUtc.Should().BeNull();
        }

        await _portal.ClienteAnonimo().SendAsync(Peticion(token));

        using (var scope = _portal.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.TokensAccesoMcp.AsNoTracking().SingleAsync()).UltimoUsoUtc.Should().NotBeNull();
        }
    }

    /// <summary>El portal web no se entera de que existe el esquema nuevo.</summary>
    [Fact]
    public async Task El_login_web_sigue_respondiendo_con_su_pagina()
    {
        var respuesta = await _portal.ClienteAnonimo().GetAsync("/Cuenta/Login");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
