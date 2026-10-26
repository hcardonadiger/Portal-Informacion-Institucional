using System.Security.Claims;
using Diger.TramitesEstado.Web.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Fija una vulnerabilidad: con una sesión de QA o de desarrollo se entraba a producción.
///
/// <para>Los tres entornos conviven en el mismo servidor y comparten el nombre de aplicación de
/// Data Protection (<c>PortalDigital</c>) y, por omisión, la carpeta de llaves
/// (<c>C:\PortalDigital_Keys</c>). Así, cualquiera de ellos descifraba la cookie emitida por otro.
/// Como además se llamaban igual (<c>.AspNetCore.Cookies</c>) y el navegador no separa cookies por
/// puerto, la cookie de QA viajaba sola a producción. El rol va dentro del ticket, así que un
/// administrador de QA quedaba administrador en producción sin conocer su contraseña.</para>
/// </summary>
public sealed class AislamientoDeSesionTests : IDisposable
{
    private const string Produccion = "Server=SRV-DIGER;Database=GestionGD;Integrated Security=true";
    private const string Qa         = "Server=SRV-DIGER;Database=GestionGD_QA;Integrated Security=true";

    // Las mismas llaves para los dos entornos: es lo que pasa hoy en el servidor.
    private readonly DirectoryInfo _llaves =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "llaves-" + Guid.NewGuid().ToString("N")));

    private ServiceProvider Entorno(string cadenaDeConexion)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection()
            .SetApplicationName("PortalDigital")
            .PersistKeysToFileSystem(_llaves);
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        services.AislarCookieDeSesion(AislamientoDeSesion.EntornoDe(cadenaDeConexion));
        return services.BuildServiceProvider();
    }

    private static CookieAuthenticationOptions Opciones(IServiceProvider sp) =>
        sp.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
          .Get(CookieAuthenticationDefaults.AuthenticationScheme);

    private static AuthenticationTicket TicketDeAdministrador() =>
        new(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "admin@diger.gob.hn"), new Claim(ClaimTypes.Role, "Administrador")],
                CookieAuthenticationDefaults.AuthenticationScheme)),
            CookieAuthenticationDefaults.AuthenticationScheme);

    [Fact]
    public void Una_sesion_emitida_por_QA_no_se_abre_en_produccion()
    {
        using var qa   = Entorno(Qa);
        using var prod = Entorno(Produccion);

        var cookieDeQa = Opciones(qa).TicketDataFormat.Protect(TicketDeAdministrador());

        Opciones(prod).TicketDataFormat.Unprotect(cookieDeQa)
            .Should().BeNull("una cookie de otro entorno no puede valer como sesión de producción");
    }

    [Fact]
    public void Cada_entorno_sigue_abriendo_sus_propias_sesiones()
    {
        // Las dos instancias de producción (o un reinicio del grupo) comparten sesión, como siempre.
        using var prod1 = Entorno(Produccion);
        using var prod2 = Entorno(Produccion);

        var cookie = Opciones(prod1).TicketDataFormat.Protect(TicketDeAdministrador());

        Opciones(prod2).TicketDataFormat.Unprotect(cookie)!.Principal.IsInRole("Administrador")
            .Should().BeTrue();
    }

    [Fact]
    public void Cada_entorno_usa_su_propio_nombre_de_cookie()
    {
        // El navegador no separa cookies por puerto: con el mismo nombre, entrar a QA pisaba la
        // sesión de producción en el mismo navegador, y la de QA viajaba a producción.
        using var qa   = Entorno(Qa);
        using var prod = Entorno(Produccion);

        Opciones(qa).Cookie.Name.Should().NotBe(Opciones(prod).Cookie.Name);
        Opciones(prod).Cookie.Name.Should().NotBe(".AspNetCore.Cookies");
    }

    [Fact]
    public void El_entorno_lo_define_la_base_no_como_se_escriba_la_cadena()
    {
        // Mayúsculas, orden de las claves o el timeout no cambian de base: no deben cerrar sesiones.
        AislamientoDeSesion.EntornoDe("Server=SRV-DIGER;Database=GestionGD;Integrated Security=true")
            .Should().Be(AislamientoDeSesion.EntornoDe(
                "Initial Catalog=gestiongd;Data Source=srv-diger;Connect Timeout=60;Integrated Security=true"));

        AislamientoDeSesion.EntornoDe(Produccion).Should().NotBe(AislamientoDeSesion.EntornoDe(Qa));
    }

    [Fact]
    public void El_portal_arranca_con_la_cookie_aislada()
    {
        // Que Program.cs efectivamente lo registre: sin esto las pruebas de arriba no protegen nada.
        using var portal = new PortalFactory();

        Opciones(portal.Services).Cookie.Name.Should().StartWith(".GestionDigital.Sesion.");
    }

    public void Dispose()
    {
        try { _llaves.Delete(recursive: true); } catch (IOException) { }
    }
}
