using System.Net;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Gateo de eliminar y restaurar usuarios en <c>/Usuarios/Index</c>.
///
/// <para>Las guardas de fondo viven en <c>EliminarUsuarioCommandHandler</c> y están probadas en
/// Application.Tests. Lo que se fija acá es lo que solo se ve desde la página: que un rol con
/// <c>Usuarios.Ver</c> pero sin capacidad de administrador reciba <b>403 y no un 409</b> —o sea,
/// que se le niegue el paso antes de llegar al comando— y que la casilla «Mostrar eliminados» no
/// se le pinte siquiera.</para>
///
/// <para>La guarda se consulta dentro del propio handler POST y no en una propiedad de la página:
/// en un POST, ASP.NET construye un PageModel nuevo y ejecuta solo el handler, así que una bandera
/// calculada en <c>OnGetAsync</c> llegaría en false. Ese fallo ya ocurrió en cinco páginas del
/// portal — ver <c>GuardasDePostTests</c>.</para>
/// </summary>
public sealed class BorrarUsuarioTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Usuarios.Ver");
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>El usuario sembrado para el rol Empleado: se elimina a ese y no a un administrador,
    /// para que el invariante de «no quedarse sin administradores» no sea lo que corte.</summary>
    private async Task<Guid> EmpleadoAsync()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Usuarios.Where(u => u.Correo == "empleado@pruebas.gob.hn")
            .Select(u => u.Id).FirstAsync();
    }

    private async Task<(HttpClient Cliente, string Token, string Html)> ListaAsync(string rol)
    {
        var cliente = _portal.ClienteComo(rol);
        var html = await cliente.GetStringAsync("/Usuarios/Index");
        var token = Regex.Match(html,
            """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

        token.Should().NotBeEmpty();
        return (cliente, token, html);
    }

    private static FormUrlEncodedContent Form(string token, Guid id) =>
        new([
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("id", id.ToString())
        ]);

    [Fact]
    public async Task Un_administrador_elimina_y_el_usuario_desaparece_de_la_lista()
    {
        var id = await EmpleadoAsync();
        var (cliente, token, html) = await ListaAsync("Administrador");
        html.Should().Contain("empleado@pruebas.gob.hn");

        var r = await cliente.PostAsync("/Usuarios/Index?handler=Eliminar", Form(token, id));
        r.StatusCode.Should().Be(HttpStatusCode.Redirect);

        (await cliente.GetStringAsync("/Usuarios/Index"))
            .Should().NotContain("empleado@pruebas.gob.hn");
    }

    [Fact]
    public async Task Con_mostrar_eliminados_vuelve_a_verse_y_se_puede_restaurar()
    {
        var id = await EmpleadoAsync();
        var (cliente, token, _) = await ListaAsync("Administrador");
        await cliente.PostAsync("/Usuarios/Index?handler=Eliminar", Form(token, id));

        (await cliente.GetStringAsync("/Usuarios/Index?eliminados=true"))
            .Should().Contain("empleado@pruebas.gob.hn");

        var r = await cliente.PostAsync("/Usuarios/Index?handler=Restaurar", Form(token, id));
        r.StatusCode.Should().Be(HttpStatusCode.Redirect);

        (await cliente.GetStringAsync("/Usuarios/Index"))
            .Should().Contain("empleado@pruebas.gob.hn");
    }

    [Fact]
    public async Task A_quien_no_es_administrador_se_le_niega_el_paso_con_403()
    {
        var id = await EmpleadoAsync();
        var (cliente, token, _) = await ListaAsync("JefeArea");

        var r = await cliente.PostAsync("/Usuarios/Index?handler=Eliminar", Form(token, id));

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "tiene Usuarios.Ver, pero eliminar lo decide la capacidad de administrador");

        (await cliente.GetStringAsync("/Usuarios/Index"))
            .Should().Contain("empleado@pruebas.gob.hn", "no se pudo haber eliminado");
    }

    [Fact]
    public async Task A_quien_no_es_administrador_ni_se_le_pinta_la_casilla()
    {
        var (_, _, htmlJefe) = await ListaAsync("JefeArea");
        htmlJefe.Should().NotContain("Mostrar eliminados");

        var (_, _, htmlAdmin) = await ListaAsync("Administrador");
        htmlAdmin.Should().Contain("Mostrar eliminados");
    }

    [Fact]
    public async Task El_administrador_no_puede_eliminarse_a_si_mismo()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var yo = await db.Usuarios.Where(u => u.Correo == "administrador@pruebas.gob.hn")
            .Select(u => u.Id).FirstAsync();

        var (cliente, token, _) = await ListaAsync("Administrador");

        var r = await cliente.PostAsync("/Usuarios/Index?handler=Eliminar", Form(token, yo));

        // El comando lanza DomainException; WebExceptionHandler la traduce a 409 y redirige a
        // /Error con el motivo legible.
        r.Headers.Location!.OriginalString.Should().Contain("/Error");
        (await cliente.GetStringAsync("/Usuarios/Index"))
            .Should().Contain("administrador@pruebas.gob.hn");
    }
}
