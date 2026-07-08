using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.Cuenta;

[Authorize]
public sealed class CambiarFiltroJerarquiaModel : PageModel
{
    public async Task<IActionResult> OnPostAsync(string? areaId, string? unidadId, string? returnUrl)
    {
        var identity = User.Identity as ClaimsIdentity;
        if (identity == null)
            return RedirectToPage("/Tableros/Index");

        var rol = identity.FindFirst(AppClaims.ActiveRol)?.Value;
        if (rol != "JefeInstitucion" && rol != "JefeArea")
            return RedirectToPage("/Tableros/Index");

        string? finalAreaId = areaId;
        if (rol == "JefeArea")
        {
            finalAreaId = identity.FindFirst(AppClaims.ActiveArea)?.Value;
        }

        // Remover claims de filtro anteriores
        var claimTypesToRemove = new[] { AppClaims.ActiveArea, AppClaims.ActiveUnidad };
        var claimsToRemove = identity.Claims.Where(c => claimTypesToRemove.Contains(c.Type)).ToList();
        foreach (var c in claimsToRemove)
            identity.RemoveClaim(c);

        // Añadir nuevos filtros
        if (!string.IsNullOrWhiteSpace(finalAreaId))
            identity.AddClaim(new Claim(AppClaims.ActiveArea, finalAreaId.Trim().ToUpper()));
        
        if (!string.IsNullOrWhiteSpace(unidadId))
            identity.AddClaim(new Claim(AppClaims.ActiveUnidad, unidadId.Trim().ToUpper()));

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
