using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Diger.TramitesEstado.Infrastructure.Security;
using Diger.TramitesEstado.Application.Usuarios.Queries.AutenticarUsuario;

namespace Diger.TramitesEstado.Web.Pages.Cuenta;

[Authorize]
public sealed class CambiarContextoModel : PageModel
{
    public async Task<IActionResult> OnPostAsync(int indiceSeleccionado, string? returnUrl)
    {
        var identity = User.Identity as ClaimsIdentity;
        if (identity == null)
            return RedirectToPage("/Tableros/Index");

        var asignacionesJson = User.FindFirstValue(AppClaims.AsignacionesJson);
        if (string.IsNullOrWhiteSpace(asignacionesJson))
            return RedirectToPage("/Tableros/Index");

        List<AsignacionAuthDto>? asignaciones = null;
        try
        {
            asignaciones = JsonSerializer.Deserialize<List<AsignacionAuthDto>>(asignacionesJson);
        }
        catch { }

        if (asignaciones == null || indiceSeleccionado < 0 || indiceSeleccionado >= asignaciones.Count)
            return RedirectToPage("/Tableros/Index");

        var active = asignaciones[indiceSeleccionado];

        // Remover claims de contexto activo anteriores
        var claimTypesToRemove = new[] 
        { 
            ClaimTypes.Role, AppClaims.ActiveRol, AppClaims.ActiveInstitucion, 
            AppClaims.ActiveArea, AppClaims.ActiveUnidad 
        };

        var claimsToRemove = identity.Claims.Where(c => claimTypesToRemove.Contains(c.Type)).ToList();
        foreach (var c in claimsToRemove)
            identity.RemoveClaim(c);

        // Añadir los nuevos claims
        identity.AddClaim(new Claim(ClaimTypes.Role, active.Rol));
        identity.AddClaim(new Claim(AppClaims.ActiveRol, active.Rol));
        identity.AddClaim(new Claim(AppClaims.ActiveInstitucion, active.InstitucionId));
        if (active.AreaId != null) identity.AddClaim(new Claim(AppClaims.ActiveArea, active.AreaId));
        if (active.UnidadId != null) identity.AddClaim(new Claim(AppClaims.ActiveUnidad, active.UnidadId));

        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToPage("/Tableros/Index");
    }
}
