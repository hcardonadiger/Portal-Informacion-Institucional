using Diger.TramitesEstado.Application.Usuarios.Commands.SolicitarRecuperacionPassword;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Diger.TramitesEstado.Web.Pages.Cuenta;

[AllowAnonymous]
public sealed class OlvidarPasswordModel(ISender sender) : PageModel
{
    [BindProperty]
    public string Correo { get; set; } = "";

    public string? Mensaje { get; private set; }
    public string? Error   { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Correo))
        {
            Error = "Por favor ingrese su correo electrónico institucional.";
            return Page();
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        try
        {
            await sender.Send(new SolicitarRecuperacionPasswordCommand(Correo, baseUrl), ct);
            Mensaje = "Si el correo electrónico está registrado en el sistema, se ha enviado un enlace con las instrucciones para restablecer su contraseña. Por favor revise su bandeja de entrada (y carpeta de correo no deseado).";
        }
        catch (Exception)
        {
            Error = "Ocurrió un inconveniente al procesar su solicitud. Por favor intente nuevamente.";
        }

        return Page();
    }
}
