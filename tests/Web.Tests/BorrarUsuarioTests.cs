using System.Net;
using System.Linq;
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

        // OJO con este 403: en producción Forbid() NO devuelve 403, redirige a AccessDeniedPath
        // (/Cuenta/Denegado, ver Program.cs). Acá sale 403 porque TestAuthHandler no configura esa
        // ruta. Lo que la prueba garantiza es que se deniega y no se borra nada; el código HTTP
        // exacto depende del esquema de autenticación y no es el contrato.
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

    // ── El reenvío del confirm global ─────────────────────────────
    // diger.js intercepta el submit de cualquier botón con clase .hist-del, muestra su propio
    // modal y, al confirmar, reenvía con form.submit(). Ese método usa el ACTION DEL FORMULARIO e
    // ignora el formaction del botón. Con el handler puesto solo en el botón, el POST caía en
    // /Usuarios sin ?handler=, Razor Pages no encontraba handler y renderizaba la página sin
    // ejecutar nada: lista vacía y ningún usuario borrado. Estas dos pruebas fijan eso.

    [Fact]
    public async Task El_formulario_de_eliminar_lleva_el_handler_en_su_propio_action()
    {
        var (_, _, html) = await ListaAsync("Administrador");

        var forms = Regex.Matches(html, "<form[^>]*method=\"post\"[^>]*>")
            .Select(m => m.Value).ToList();

        forms.Should().Contain(f => f.Contains("handler=Eliminar"),
            "diger.js reenvía con form.submit(), que descarta el formaction del botón");
    }

    [Fact]
    public async Task Reenviar_por_el_action_del_formulario_borra_de_verdad()
    {
        // Reproduce literalmente lo que hace diger.js: postea al action del formulario y agrega
        // el name/value del botón como hidden.
        var id = await EmpleadoAsync();
        var (cliente, token, html) = await ListaAsync("Administrador");

        var accion = Regex.Matches(html, "<form[^>]*method=\"post\"[^>]*>")
            .Select(m => Regex.Match(m.Value, "action=\"([^\"]*handler=Eliminar[^\"]*)\"").Groups[1].Value)
            .FirstOrDefault(a => !string.IsNullOrEmpty(a));

        accion.Should().NotBeNullOrEmpty("sin action con handler, form.submit() no llega a ningún handler");

        var r = await cliente.PostAsync(System.Net.WebUtility.HtmlDecode(accion), Form(token, id));
        r.StatusCode.Should().Be(HttpStatusCode.Redirect);

        (await cliente.GetStringAsync("/Usuarios/Index"))
            .Should().NotContain("empleado@pruebas.gob.hn");
    }

    [Fact]
    public async Task El_boton_no_lleva_su_propio_confirm_para_no_preguntar_dos_veces()
    {
        // diger.js ya confirma por su cuenta al ver .hist-del. Con un onclick=confirm() encima,
        // al usuario le salían DOS diálogos seguidos para la misma acción.
        var (_, _, html) = await ListaAsync("Administrador");

        var boton = Regex.Match(html, "<button[^>]*hist-del[^>]*>").Value;

        boton.Should().NotBeEmpty();
        boton.Should().NotContain("onclick", "el confirm lo pone diger.js, no la vista");
        boton.Should().Contain("data-confirm", "así el modal del portal dice a quién se elimina");
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
