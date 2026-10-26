using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;

namespace Diger.TramitesEstado.Web.Security;

/// <summary>
/// Ata la cookie de sesión al entorno que la emitió, para que una sesión de QA o de desarrollo no
/// valga en producción.
///
/// <para>Los tres entornos conviven en el mismo servidor con el mismo nombre de aplicación de Data
/// Protection (<c>PortalDigital</c>) y, salvo que alguien la cambie, la misma carpeta de llaves.
/// Cualquiera descifraba la cookie de otro, y como el rol va dentro del ticket, un administrador de
/// QA era administrador en producción sin conocer su contraseña. El navegador, además, no separa
/// cookies por puerto: con el mismo nombre, la de QA viajaba sola a producción.</para>
///
/// <para>No se cambia el nombre de aplicación de Data Protection, que sería lo obvio: con esas
/// llaves están cifrados también la contraseña SMTP y la caché de tokens de Outlook guardadas en la
/// base, y cambiarlo los dejaría ilegibles. Se aísla solo lo que hace falta, el ticket de sesión:
/// su propósito de cifrado y el nombre de la cookie llevan el entorno.</para>
///
/// <para>El entorno es la base de datos a la que apunta el portal, no <c>ASPNETCORE_ENVIRONMENT</c>:
/// el despliegue fija <c>Production</c> en todos los sitios, y es la base la que define qué usuarios
/// y qué contraseñas valen. Así el aislamiento no depende de que nadie configure nada.</para>
/// </summary>
public static class AislamientoDeSesion
{
    // El propósito con el que el framework protege el ticket de cookie; el entorno va a continuación.
    private const string PropositoDelFramework =
        "Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationMiddleware";

    /// <summary>Identificador corto del entorno, derivado del servidor y la base de la cadena de
    /// conexión. Mayúsculas, orden de las claves u opciones como el timeout no lo cambian.</summary>
    public static string EntornoDe(string? cadenaDeConexion)
    {
        string identidad;
        try
        {
            var cs = new SqlConnectionStringBuilder(cadenaDeConexion ?? string.Empty);
            identidad = $"{cs.DataSource}|{cs.InitialCatalog}".ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            // Una cadena que no es de SQL Server (las pruebas usan SQLite): sirve tal cual.
            identidad = (cadenaDeConexion ?? string.Empty).ToLowerInvariant();
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identidad));
        return Convert.ToHexString(hash, 0, 6).ToLowerInvariant();
    }

    public static string NombreDeCookie(string entorno) => $".GestionDigital.Sesion.{entorno}";

    /// <summary>Da a la cookie de sesión un nombre y un propósito de cifrado propios del entorno.
    /// Se registra como <c>Configure</c> para correr antes del <c>PostConfigure</c> del framework,
    /// que solo arma su <c>TicketDataFormat</c> si nadie lo hizo.</summary>
    public static IServiceCollection AislarCookieDeSesion(this IServiceCollection services, string entorno)
    {
        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<IDataProtectionProvider>((opts, proveedor) =>
            {
                opts.Cookie.Name = NombreDeCookie(entorno);
                opts.TicketDataFormat = new TicketDataFormat(proveedor.CreateProtector(
                    PropositoDelFramework, CookieAuthenticationDefaults.AuthenticationScheme, "v2",
                    "Entorno", entorno));
            });

        return services;
    }
}
