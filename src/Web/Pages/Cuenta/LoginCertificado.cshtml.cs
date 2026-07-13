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

namespace Diger.TramitesEstado.Web.Pages.Cuenta;

[AllowAnonymous]
public sealed class LoginCertificadoModel(ISender sender) : PageModel
{
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(string? returnUrl, CancellationToken ct)
    {
        var clientCert = await HttpContext.Connection.GetClientCertificateAsync();
        if (clientCert is null)
        {
            Error = "No se detectó un certificado digital válido o el navegador no lo envió. Por favor intenta de nuevo o ingresa con correo y contraseña.";
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
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(AppClaims.UserId,          usuario.Id.ToString()),
            new(ClaimTypes.Name,           usuario.Nombre),
            new(ClaimTypes.Email,          usuario.Correo)
        };
        
        if (usuario.Asignaciones.Count > 0)
        {
            var asignacionesJson = JsonSerializer.Serialize(usuario.Asignaciones);
            claims.Add(new Claim(AppClaims.AsignacionesJson, asignacionesJson));

            var active = usuario.Asignaciones[0];
            claims.Add(new Claim(ClaimTypes.Role,           active.Rol));
            claims.Add(new Claim(AppClaims.ActiveRol,       active.Rol));
            claims.Add(new Claim(AppClaims.ActiveInstitucion, active.InstitucionId));
            if (active.AreaId != null) claims.Add(new Claim(AppClaims.ActiveArea, active.AreaId));
            if (active.UnidadId != null) claims.Add(new Claim(AppClaims.ActiveUnidad, active.UnidadId));
        }
        else
        {
            claims.Add(new Claim(ClaimTypes.Role,           usuario.RolGlobal));
            claims.Add(new Claim(AppClaims.ActiveRol,       usuario.RolGlobal));
        }

        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });

        // Redirigir siempre de vuelta al puerto original o al returnUrl relativo al dominio.
        // Como la cookie está asociada al localhost en general, funcionará perfectamente en el puerto 49175.
        var finalUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) 
            ? returnUrl 
            : "/Tableros/Index";

        // Si estamos en localhost y acabamos en el puerto 49176, lo mejor es forzar la vuelta al puerto normal
        var host = Request.Host.Host;
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        
        if (environment == "Development" && host == "localhost")
        {
            return Redirect($"https://localhost:49175{finalUrl}");
        }

        // Si es prod y usamos subdominio, deberíamos forzar la vuelta al dominio principal. 
        // Para simplificar, si estamos en cert.dominio.com volvemos a dominio.com
        if (host.StartsWith("cert."))
        {
            var mainDomain = host.Substring(5);
            return Redirect($"https://{mainDomain}{finalUrl}");
        }

        return LocalRedirect(finalUrl);
    }
}
