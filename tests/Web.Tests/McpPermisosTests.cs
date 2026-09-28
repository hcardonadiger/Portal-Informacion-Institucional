using System.Net;
using System.Security.Claims;
using Diger.TramitesEstado.Infrastructure.Security;
using Diger.TramitesEstado.Web.Mcp;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// La guarda de permisos de las herramientas MCP y el gateo de la pantalla de tokens.
///
/// <para>Las herramientas no pasan por <c>PermissionPageFilter</c> —ese es un filtro de Razor
/// Pages y ellas no son páginas—, así que sin <c>McpPermisos</c> un token con acceso a
/// proyectos podría llamar a las herramientas de reuniones, expedientes o tickets sin tener
/// sus claves. Esto verifica que cada herramienta exige la suya.</para>
/// </summary>
public sealed class McpPermisosTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();

        // El Empleado ve proyectos, pero nada más. El Consultor es de solo lectura.
        await _portal.OtorgarAsync("Empleado", "Proyectos.Ver");
        await _portal.OtorgarAsync("Consultor", "Proyectos.Ver", "Proyectos.Editar");
        await _portal.OtorgarAsync("JefeArea", "Proyectos.Ver", "Proyectos.Editar");
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Arma un McpPermisos que habla por el rol indicado, con la misma forma de
    /// identidad que emite <c>SesionClaimsFactory</c> para un token: los mismos claims que
    /// el login, que es de donde CurrentUserService y la matriz de permisos leen todo.</summary>
    private async Task<T> ComoRolAsync<T>(string rol, Func<McpPermisos, Task<T>> accion)
    {
        using var scope = _portal.Services.CreateScope();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(AppClaims.UserId,          Guid.NewGuid().ToString()),
            new(ClaimTypes.Name,           "Usuario de prueba"),
            new(ClaimTypes.Role,           rol),
            new(AppClaims.ActiveRol,       rol),
            new(AppClaims.ActiveInstitucion, "DIGER")
        };

        var contexto = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        contexto.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "McpBearer"));

        // McpPermisos y CurrentUserService leen del accessor del ámbito.
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = contexto;

        return await accion(scope.ServiceProvider.GetRequiredService<McpPermisos>());
    }

    [Fact]
    public async Task Una_herramienta_de_proyectos_pasa_con_Proyectos_Ver()
    {
        var accion = async () => await ComoRolAsync("Empleado", async p =>
        {
            await p.ExigirAsync("Proyectos.Ver");
            return true;
        });

        await accion.Should().NotThrowAsync();
    }

    /// <summary>Tener proyectos no abre reuniones: cada herramienta pide la clave de SU módulo.</summary>
    [Fact]
    public async Task Ver_proyectos_no_habilita_las_herramientas_de_reuniones()
    {
        var accion = async () => await ComoRolAsync("Empleado", async p =>
        {
            await p.ExigirAsync("Reuniones.Ver");
            return true;
        });

        (await accion.Should().ThrowAsync<McpPermisoDenegadoException>())
            .WithMessage("*Reuniones.Ver*", "el mensaje tiene que nombrar la clave que falta");
    }

    [Fact]
    public async Task Ver_proyectos_no_habilita_escribir_en_proyectos()
    {
        var accion = async () => await ComoRolAsync("Empleado", async p =>
        {
            await p.ExigirEscrituraAsync("Proyectos.Editar");
            return true;
        });

        await accion.Should().ThrowAsync<McpPermisoDenegadoException>();
    }

    /// <summary>El rol de solo lectura tiene la clave de edición otorgada y aun así no escribe:
    /// la capacidad del rol manda sobre la matriz.</summary>
    [Fact]
    public async Task Un_rol_de_solo_lectura_no_escribe_aunque_tenga_la_clave()
    {
        var puedeLeer = await ComoRolAsync("Consultor", async p =>
        {
            await p.ExigirAsync("Proyectos.Editar");   // la clave la tiene
            return true;
        });
        puedeLeer.Should().BeTrue();

        var accion = async () => await ComoRolAsync("Consultor", async p =>
        {
            await p.ExigirEscrituraAsync("Proyectos.Editar");
            return true;
        });

        (await accion.Should().ThrowAsync<McpPermisoDenegadoException>())
            .WithMessage("*solo lectura*");
    }

    [Fact]
    public async Task Un_rol_con_la_clave_de_edicion_si_escribe()
    {
        var accion = async () => await ComoRolAsync("JefeArea", async p =>
        {
            await p.ExigirEscrituraAsync("Proyectos.Editar");
            return true;
        });

        await accion.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PuedeAsync_responde_sin_lanzar_para_armar_el_catalogo()
    {
        (await ComoRolAsync("Empleado", p => p.PuedeAsync("Proyectos.Ver"))).Should().BeTrue();
        (await ComoRolAsync("Empleado", p => p.PuedeAsync("Tickets.Ver"))).Should().BeFalse();
    }

    // ── Gateo de la pantalla de tokens ────────────────────────────────────────

    /// <summary>Emitir tokens no es autoservicio: sin la clave, la pantalla no se abre.</summary>
    [Fact]
    public async Task Sin_TokensMcp_Ver_la_pantalla_de_tokens_no_se_abre()
    {
        var respuesta = await _portal.ClienteComo("Empleado").GetAsync("/Cuenta/TokensMcp");

        respuesta.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect);
        respuesta.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Con_TokensMcp_Ver_la_pantalla_se_abre()
    {
        await _portal.OtorgarAsync("Empleado", "TokensMcp.Ver");

        var respuesta = await _portal.ClienteComo("Empleado").GetAsync("/Cuenta/TokensMcp");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Ver los propios no es ver los de todos.</summary>
    [Fact]
    public async Task Ver_los_tokens_propios_no_abre_la_vista_de_administracion()
    {
        await _portal.OtorgarAsync("Empleado", "TokensMcp.Ver", "TokensMcp.Crear");

        var respuesta = await _portal.ClienteComo("Empleado").GetAsync("/Accesos/TokensMcp");

        respuesta.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task La_vista_de_administracion_se_abre_con_su_propia_clave()
    {
        await _portal.OtorgarAsync("JefeArea", "TokensMcp.Admin.Ver");

        var respuesta = await _portal.ClienteComo("JefeArea").GetAsync("/Accesos/TokensMcp");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Las claves nuevas tienen que quedar declaradas donde PermissionCatalogSyncService las
    /// busca: atributos <c>[Permission]</c> sobre los PageModel y sus handlers. Él las
    /// descubre por reflexión al arrancar y con eso puebla la matriz de Accesos, así que si
    /// se declararan en otro lado —o se olvidara una en un handler— nunca aparecerían para
    /// otorgarlas.
    ///
    /// <para>Se mira la reflexión y no la tabla a propósito: en el host de pruebas el esquema
    /// se crea después de que arrancan los hosted services, así que el catálogo persistido
    /// está vacío y no dice nada sobre este invariante.</para>
    /// </summary>
    [Theory]
    [InlineData(typeof(Pages.Cuenta.TokensMcpModel),  "TokensMcp.Ver", "TokensMcp.Crear", "TokensMcp.Eliminar")]
    [InlineData(typeof(Pages.Accesos.TokensMcpAdminModel), "TokensMcp.Admin.Ver", "TokensMcp.Admin.Eliminar")]
    public void Las_claves_de_tokens_estan_declaradas_donde_el_catalogo_las_busca(
        Type pagina, params string[] esperadas)
    {
        var declaradas = pagina
            .GetCustomAttributes(typeof(PermissionAttribute), inherit: false)
            .Concat(pagina.GetMethods()
                .SelectMany(m => m.GetCustomAttributes(typeof(PermissionAttribute), inherit: false)))
            .Cast<PermissionAttribute>()
            .Select(a => a.Clave)
            .ToList();

        declaradas.Should().Contain(esperadas);
    }

    /// <summary>Un handler de mutación sin <c>[Permission]</c> queda abierto y el sync solo
    /// lo reporta como advertencia al arrancar, que nadie lee.</summary>
    [Theory]
    [InlineData(typeof(Pages.Cuenta.TokensMcpModel))]
    [InlineData(typeof(Pages.Accesos.TokensMcpAdminModel))]
    public void Todo_handler_de_post_declara_su_permiso(Type pagina)
    {
        var sinPermiso = pagina.GetMethods()
            .Where(m => m.Name.StartsWith("OnPost"))
            .Where(m => !m.GetCustomAttributes(typeof(PermissionAttribute), false).Any())
            .Select(m => m.Name)
            .ToList();

        sinPermiso.Should().BeEmpty();
    }
}
