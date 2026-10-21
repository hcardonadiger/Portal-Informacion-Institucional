using System.Net;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Prueba el gateo real de extremo a extremo: PermissionPageFilter, PermissionPolicyProvider,
/// PermissionAuthorizationHandler y la caché por rol, sobre el catálogo que
/// PermissionCatalogSyncService descubre por reflexión al arrancar.
///
/// Es la cobertura que faltaba: los tests de Application prueban los handlers por separado,
/// pero nada verificaba que el cableado que decide el acceso funcione junto. El bug de la
/// guardia anti-bloqueo pasó 116 tests en verde y solo apareció al usar la pantalla.
/// </summary>
public sealed class GateoDePermisosTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();

        // Un rol de consulta y otro de edición, para contrastar la misma ruta.
        await _portal.OtorgarAsync("Consultor", "Expedientes.Ver", "Tickets.Ver");
        await _portal.OtorgarAsync("JefeArea",
            "Expedientes.Ver", "Expedientes.Editar", "Tickets.Ver", "Tickets.Crear");
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    // ── Sin sesión ────────────────────────────────────────────────────────
    [Theory]
    [InlineData("/")]
    [InlineData("/Accesos/Permisos")]
    [InlineData("/Usuarios")]
    public async Task Anonimo_EsRechazado(string ruta)
    {
        // 401 y no el 302 a /Cuenta/Login que devuelve la aplicación real: el redirect lo hace
        // el manejador de cookies, que en el host de pruebas está sustituido. Lo que importa
        // acá es que la petición sin sesión se rechaza, no la forma del rechazo.
        var respuesta = await _portal.ClienteAnonimo().GetAsync(ruta);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Con rol, sin la clave ─────────────────────────────────────────────
    [Theory]
    [InlineData("/Accesos/Permisos")]   // Accesos.Permisos.Ver
    [InlineData("/Accesos/Roles")]      // Accesos.Roles.Ver
    [InlineData("/Usuarios")]           // Usuarios.Ver
    [InlineData("/Instituciones")]      // Instituciones.Ver
    public async Task SinLaClave_DevuelveProhibido(string ruta)
    {
        var respuesta = await _portal.ClienteComo("Consultor").GetAsync(ruta);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ConLaClaveDeConsulta_Entra()
    {
        var respuesta = await _portal.ClienteComo("Consultor").GetAsync("/");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── La misma ruta, dos roles distintos ────────────────────────────────
    [Fact]
    public async Task EditorDeExpedientes_SoloParaQuienTieneEditar()
    {
        (await _portal.ClienteComo("Consultor").GetAsync("/Expedientes/Editor"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await _portal.ClienteComo("JefeArea").GetAsync("/Expedientes/Editor"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Capacidad de administrador: aprueba sin filas en la matriz ────────
    [Fact]
    public async Task RolAdministrador_EntraSinTenerNingunaClaveOtorgada()
    {
        // A Administrador no se le otorgó nada en InitializeAsync a propósito: si igual entra,
        // es porque PermissionAuthorizationHandler lo aprueba por capacidad, no por la matriz.
        //
        // En serie y no con Task.WhenAll: toda la fixture comparte una única conexión SQLite
        // en memoria, que no admite acceso concurrente. Con peticiones en paralelo el test
        // falla de forma intermitente por la base, no por el permiso que quiere probar.
        var cliente = _portal.ClienteComo("Administrador");

        foreach (var ruta in new[] { "/Accesos/Permisos", "/Usuarios", "/Expedientes/Editor" })
        {
            var respuesta = await cliente.GetAsync(ruta);
            respuesta.StatusCode.Should().Be(HttpStatusCode.OK, "el rol administrador aprueba {0} por capacidad", ruta);
        }
    }

    // ── Cuenta sin asignación: sin rol, sin nada ──────────────────────────
    [Theory]
    [InlineData("/")]
    [InlineData("/Usuarios")]
    [InlineData("/Accesos/Permisos")]
    public async Task SinRolAsignado_NoAccedeANingunModulo(string ruta)
    {
        // Es el caso que el fallback `?? "Empleado"` tapaba: la cuenta entraba con 32 claves.
        var respuesta = await _portal.ClienteComo(rolId: null).GetAsync(ruta);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SinRolAsignado_ConservaElAutoservicio()
    {
        // Perfil y contraseña llevan [PermisoNoRequerido]: tienen que seguir alcanzables, o
        // una cuenta sin configurar no podría ni cambiar su propia contraseña.
        var cliente = _portal.ClienteComo(rolId: null);

        (await cliente.GetAsync("/Cuenta/Perfil")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await cliente.GetAsync("/Cuenta/Contrasena")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Granularidad por handler ──────────────────────────────────────────
    [Fact]
    public async Task DiagnosticoYBitacora_CompartenLaClaveDeConsultaDePermisos()
    {
        var cliente = _portal.ClienteComo("Consultor");

        (await cliente.GetAsync("/Accesos/Auditoria")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await cliente.GetAsync("/Accesos/Diagnostico")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await _portal.OtorgarAsync("Consultor", "Accesos.Permisos.Ver");

        (await cliente.GetAsync("/Accesos/Auditoria")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await cliente.GetAsync("/Accesos/Diagnostico")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
