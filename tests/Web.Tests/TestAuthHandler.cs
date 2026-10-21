using System.Security.Claims;
using System.Text.Encodings.Web;
using Diger.TramitesEstado.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Firma al usuario de la prueba a partir de cabeceras del request.
///
/// **No es un bypass de autenticación de la aplicación**: este esquema se registra solo en el
/// host de pruebas (ver <see cref="PortalFactory"/>) y no existe en el binario que se despliega.
/// Es deliberadamente distinto de agregar un endpoint de desarrollo tipo "entrar como rol X",
/// que sí viviría en la aplicación y terminaría desplegado con el ambiente mal configurado.
///
/// Las cabeceras se leen por request para que una sola instancia del host sirva a todos los
/// roles: cada prueba dice con qué rol habla sin levantar la aplicación de nuevo.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Esquema = "Pruebas";

    public const string CabeceraRol         = "X-Test-Rol";
    public const string CabeceraUsuarioId   = "X-Test-UsuarioId";
    public const string CabeceraInstitucion = "X-Test-Institucion";
    public const string CabeceraArea        = "X-Test-Area";
    public const string CabeceraUnidad      = "X-Test-Unidad";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Sin cabecera de usuario la petición es anónima: así se puede probar también el
        // comportamiento sin sesión con el mismo host.
        if (!Request.Headers.TryGetValue(CabeceraUsuarioId, out var uid))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, uid.ToString()),
            new(AppClaims.UserId,          uid.ToString()),
            new(ClaimTypes.Name,           "Usuario de prueba"),
            new(ClaimTypes.Email,          "pruebas@diger.gob.hn"),
        };

        // El rol es opcional a propósito: su ausencia es justamente el caso de la cuenta sin
        // asignación, que debe quedar sin capacidades en vez de caer en un rol por defecto.
        if (Request.Headers.TryGetValue(CabeceraRol, out var rol) && !string.IsNullOrWhiteSpace(rol))
        {
            claims.Add(new Claim(ClaimTypes.Role, rol!));
            claims.Add(new Claim(AppClaims.ActiveRol, rol!));
        }

        Agregar(claims, CabeceraInstitucion, AppClaims.ActiveInstitucion);
        Agregar(claims, CabeceraArea,        AppClaims.ActiveArea);
        Agregar(claims, CabeceraUnidad,      AppClaims.ActiveUnidad);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Esquema));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Esquema)));
    }

    private void Agregar(List<Claim> claims, string cabecera, string claim)
    {
        if (Request.Headers.TryGetValue(cabecera, out var v) && !string.IsNullOrWhiteSpace(v))
            claims.Add(new Claim(claim, v!));
    }
}
