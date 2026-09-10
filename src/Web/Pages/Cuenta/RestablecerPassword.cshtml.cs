using Diger.TramitesEstado.Application.Usuarios.Commands.RestablecerPassword;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Diger.TramitesEstado.Web.Pages.Cuenta;

[AllowAnonymous]
public sealed class RestablecerPasswordModel(ISender sender) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Correo { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string Token { get; set; } = "";

    [BindProperty]
    public string NuevaPassword { get; set; } = "";

    [BindProperty]
    public string ConfirmarPassword { get; set; } = "";

    public string? Error   { get; private set; }
    public bool    Exitoso { get; private set; }

    public IActionResult OnGet()
    {
        if (string.IsNullOrWhiteSpace(Correo) || string.IsNullOrWhiteSpace(Token))
        {
            Error = "El enlace de restablecimiento es incompleto o inválido. Por favor solicite uno nuevo.";
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Correo) || string.IsNullOrWhiteSpace(Token))
        {
            Error = "Datos de recuperación no válidos. Por favor solicite un nuevo enlace.";
            return Page();
        }

        try
        {
            await sender.Send(new RestablecerPasswordCommand(Correo, Token, NuevaPassword, ConfirmarPassword), ct);
            Exitoso = true;
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
        }
        catch (ValidationException ex)
        {
            Error = string.Join(" ", ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception)
        {
            Error = "No se pudo actualizar la contraseña. Verifique que el enlace no haya expirado e intente nuevamente.";
        }

        return Page();
    }
}
