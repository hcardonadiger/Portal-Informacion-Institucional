using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MediatR;
using Diger.TramitesEstado.Application.Usuarios.Queries.AutenticarUsuario;
using Diger.TramitesEstado.Infrastructure.Security;
using Diger.TramitesEstado.Web.Security;

namespace Diger.TramitesEstado.Web.Pages.Cuenta;

[AllowAnonymous]
public sealed class LoginCertificadoModel(ISender sender, IConfiguration config) : PageModel
{
    public string? Error { get; set; }

    public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync(string? returnUrl, CancellationToken ct)
    {
        ReturnUrl = returnUrl;
        var clientCert = await HttpContext.Connection.GetClientCertificateAsync();
        if (clientCert is null)
        {
            Error = "No se detectó un certificado digital válido o el navegador no lo envió. Por favor intenta de nuevo o ingresa con correo y contraseña.";
            return Page();
        }

        // Si existe el certificado, mostramos la interfaz y dejamos que el POST haga la validación asíncrona
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl, CancellationToken ct)
    {
        ReturnUrl = returnUrl;
        var clientCert = await HttpContext.Connection.GetClientCertificateAsync();
        if (clientCert is null)
        {
            Error = "No se detectó un certificado digital válido en la petición. Por favor intenta de nuevo.";
            return Page();
        }

        if (DateTime.Now < clientCert.NotBefore || DateTime.Now > clientCert.NotAfter)
        {
            Error = "El certificado digital ha expirado o aún no es válido.";
            return Page();
        }

        var usuario = await sender.Send(new AutenticarUsuarioCertificadoQuery(clientCert.Thumbprint), ct);
        if (usuario is null)
        {
            Error = "El certificado digital proporcionado no está vinculado a ninguna cuenta activa. Inicia sesión con correo y contraseña, y vincúlalo en la sección Mi Perfil.";
            return Page();
        }

        return await SignInUsuarioAsync(usuario, returnUrl);
    }

    private async Task<IActionResult> SignInUsuarioAsync(UsuarioAuthDto usuario, string? returnUrl)
    {
        // Mismos claims que el login por contraseña, del mismo sitio — ver SesionClaimsFactory.
        var identity  = SesionClaimsFactory.Crear(usuario, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });

        // Redirigir siempre de vuelta al puerto original o al returnUrl relativo al dominio.
        // Como la cookie está asociada al localhost en general, funcionará perfectamente en el puerto 49175.
        var finalUrl = usuario.Asignaciones.Count == 0
            ? "/Cuenta/Perfil?sinAsignacion=true"
            : !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl != "/"
                ? returnUrl
                : "/Tableros/Index";

        // Si estamos en localhost y acabamos en el puerto 49176, lo mejor es forzar la vuelta al puerto normal
        var host = Request.Host.Host;
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        
        if (environment == "Development" && host == "localhost")
        {
            return Redirect($"https://localhost:{config.GetValue<int>("Ports:DevMain", 49175)}{finalUrl}");
        }

        // Forzamos el regreso al puerto principal en la misma IP/Host
        var mainPort = config.GetValue<int>("Ports:Main", 8443);
        var portSuffix = mainPort == 443 ? "" : $":{mainPort}";
        
        // Si aún entran por subdominio (híbrido) quitamos el cert.
        var mainDomain = host.StartsWith("cert.") ? host.Substring(5) : host;
        
        return Redirect($"https://{mainDomain}{portSuffix}{finalUrl}");
    }
}
