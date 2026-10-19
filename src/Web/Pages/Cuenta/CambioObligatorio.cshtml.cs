using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Common.Models;
using Diger.TramitesEstado.Application.Usuarios.Commands.EstablecerPasswordObligatoria;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Infrastructure.Security;
using Diger.TramitesEstado.Web.Common;

namespace Diger.TramitesEstado.Web.Pages.Cuenta;

/// <summary>
/// La única salida del portón de cambio obligatorio. No pide la contraseña actual: a esta
/// pantalla se llega ya autenticado, y volver a pedirla trabaría a quien entró con una temporal
/// que un administrador le entregó y que quizá no recuerda bien.
/// </summary>
// [Authorize] explícito porque /Cuenta está exento por convención (lo necesita el login).
[Authorize]
[PermisoNoRequerido("Autoservicio: gatear el cambio obligatorio con una clave otorgable dejaría al usuario encerrado sin poder cumplirlo.")]
public sealed class CambioObligatorioModel(
    ISender sender,
    ICurrentUserService currentUser,
    IOptions<AuthOptions> opciones) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "La nueva contraseña es requerida.")]
    [MinLength(8, ErrorMessage = "La nueva contraseña debe tener al menos 8 caracteres.")]
    [DataType(DataType.Password)]
    public string PasswordNueva { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "La confirmación es requerida.")]
    [Compare(nameof(PasswordNueva), ErrorMessage = "La nueva contraseña y su confirmación no coinciden.")]
    [DataType(DataType.Password)]
    public string PasswordConfirmar { get; set; } = string.Empty;

    public string? Error { get; private set; }

    public IActionResult OnGet()
    {
        if (currentUser.UserId is null) return RedirectToPage("/Cuenta/Login");

        // Quien ya cumplió no tiene nada que hacer acá. Sin esta guarda, la pantalla quedaría
        // accesible por URL como un cambio de contraseña sin pedir la actual.
        if (!Pendiente()) return RedirectToPage("/Tableros/Index");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (currentUser.UserId is null) return RedirectToPage("/Cuenta/Login");
        if (!Pendiente()) return RedirectToPage("/Tableros/Index");
        if (!ModelState.IsValid) return Page();

        try
        {
            var estampa = await sender.Send(
                new EstablecerPasswordObligatoriaCommand(currentUser.UserId.Value, PasswordNueva), ct);

            await RefrescarClaimAsync(estampa);

            TempData["SuccessMessage"] =
                "Tu contraseña quedó actualizada. No volveremos a pedirte este cambio.";
            return RedirectToPage("/Tableros/Index");
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
            return Page();
        }
        catch (FluentValidation.ValidationException ex)
        {
            Error = string.Join(" ", ex.Errors.Select(e => e.ErrorMessage));
            return Page();
        }
    }

    private bool Pendiente() =>
        CambioPasswordObligatorio.Pendiente(User, opciones.Value.CorteCambioPasswordUtc);

    /// <summary>
    /// Vuelve a firmar la sesión con la fecha nueva. Se editan los claims que ya tiene en vez de
    /// rearmar la identidad desde cero: así se conserva cuál de sus asignaciones es la activa,
    /// que el usuario pudo haber cambiado a mano. Sin este refresco, el portón lo seguiría
    /// reteniendo con la cookie vieja aunque la contraseña ya esté cambiada.
    /// </summary>
    private async Task RefrescarClaimAsync(DateTime cambiadoElUtc)
    {
        if (User.Identity is not ClaimsIdentity identity) return;

        foreach (var viejo in identity.FindAll(AppClaims.PasswordCambiadoEl).ToList())
            identity.RemoveClaim(viejo);

        identity.AddClaim(new Claim(
            AppClaims.PasswordCambiadoEl,
            cambiadoElUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }
}
