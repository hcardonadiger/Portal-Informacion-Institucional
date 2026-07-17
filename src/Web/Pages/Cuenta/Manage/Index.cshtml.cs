using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MediatR;
using Diger.TramitesEstado.Application.Usuarios.Commands.ActualizarMiPerfil;
using Diger.TramitesEstado.Application.Usuarios.Queries.GetUsuarioById;
using Diger.TramitesEstado.Application.Common.Interfaces;

namespace Diger.TramitesEstado.Web.Pages.Cuenta.Manage;

public sealed class IndexModel(ISender sender, ICurrentUserService currentUser) : PageModel
{
    [BindProperty] public string Nombre { get; set; } = string.Empty;
    [BindProperty] public string Correo { get; set; } = string.Empty;
    [BindProperty] public string? PasswordActual { get; set; }
    [BindProperty] public string? PasswordNuevo { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (currentUser.UserId == null) return RedirectToPage("/Cuenta/Login");

        var dto = await sender.Send(new GetUsuarioByIdQuery(currentUser.UserId.Value), ct);
        
        Nombre = dto.Nombre;
        Correo = dto.Correo;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await sender.Send(new ActualizarMiPerfilCommand(
                currentUser.UserId!.Value, Nombre, Correo, PasswordActual, PasswordNuevo), ct);
                
            TempData["SuccessMessage"] = "Tu perfil ha sido actualizado correctamente.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return Page();
        }
    }
}
