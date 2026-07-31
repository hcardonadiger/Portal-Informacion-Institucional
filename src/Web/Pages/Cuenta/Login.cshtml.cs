using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.Cuenta;

[AllowAnonymous]
// NoStore evita que el navegador reviva esta página desde su caché: al volver
// con el botón «Atrás» servía el HTML renderizado durante la sesión anterior.
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LoginModel(ISender sender, IConfiguration config) : PageModel
{
    [BindProperty] public string Correo   { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
    public string? Error     { get; set; }

    /// <summary>Un usuario con sesión activa no debe ver el formulario de acceso:
    /// _Layout dibuja el header de la aplicación en cuanto hay autenticación, y
    /// quedaba el nombre del usuario sobre la pantalla de login.</summary>
    public IActionResult OnGet(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl != "/")
                return LocalRedirect(returnUrl);

            return RedirectToPage("/Tableros/Index");
        }

        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl, CancellationToken ct)
    {
        ReturnUrl = returnUrl;

        var usuario = await sender.Send(new AutenticarUsuarioQuery(Correo, Password), ct);
        if (usuario is null)
        {
            Error = "Correo o contraseña incorrectos.";
            return Page();
        }

        return await SignInUsuarioAsync(usuario, returnUrl);
    }

    public IActionResult OnPostCertificado(string? returnUrl)
    {
        var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        var host = Request.Host.Host;
        var certPort = config.GetValue<int>("Ports:Cert", 444);
        
        var targetUrl = isDev 
            ? $"https://localhost:49176/Cuenta/LoginCertificado" 
            : $"https://{host}:{certPort}/Cuenta/LoginCertificado";

        if (!string.IsNullOrEmpty(returnUrl))
        {
            targetUrl += $"?returnUrl={Uri.EscapeDataString(returnUrl)}";
        }

        return Redirect(targetUrl);
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

            // Set the first assignment as the active context by default
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

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl != "/")
            return LocalRedirect(returnUrl);

        return RedirectToPage("/Tableros/Index");
    }
}
