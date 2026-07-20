using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MediatR;
using Diger.TramitesEstado.Application.Usuarios.Commands.CambiarMiPassword;
using Diger.TramitesEstado.Application.Common.Interfaces;

namespace Diger.TramitesEstado.Web.Pages.Cuenta;

public sealed class ContrasenaModel(ISender sender, ICurrentUserService currentUser) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "La contraseña actual es requerida.")]
    [DataType(DataType.Password)]
    public string PasswordActual { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "La nueva contraseña es requerida.")]
    [MinLength(6, ErrorMessage = "La nueva contraseña debe tener al menos 6 caracteres.")]
    [DataType(DataType.Password)]
    public string PasswordNuevo { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "La confirmación de contraseña es requerida.")]
    [Compare(nameof(PasswordNuevo), ErrorMessage = "La nueva contraseña y su confirmación no coinciden.")]
    [DataType(DataType.Password)]
    public string PasswordConfirmar { get; set; } = string.Empty;

    public IActionResult OnGet()
    {
        if (currentUser.UserId == null) return RedirectToPage("/Cuenta/Login");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (currentUser.UserId == null) return RedirectToPage("/Cuenta/Login");
        if (!ModelState.IsValid) return Page();

        try
        {
            await sender.Send(new CambiarMiPasswordCommand(
                currentUser.UserId.Value, PasswordActual, PasswordNuevo), ct);

            TempData["SuccessMessage"] = "Tu contraseña ha sido cambiada correctamente.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return Page();
        }
    }
}
