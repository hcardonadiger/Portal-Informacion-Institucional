using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MediatR;
using Diger.TramitesEstado.Application.Usuarios.Commands.VincularMiCertificado;
using Diger.TramitesEstado.Application.Common.Interfaces;

namespace Diger.TramitesEstado.Web.Pages.Cuenta;

[Authorize]
public sealed class VincularCertificadoModel(ISender sender, ICurrentUserService currentUser) : PageModel
{
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (currentUser.UserId == null) return RedirectToPage("/Cuenta/Login");

        var clientCert = await HttpContext.Connection.GetClientCertificateAsync();
        if (clientCert is null)
        {
            Error = "No se detectó un certificado digital válido o cancelaste la selección del navegador. Por favor intenta de nuevo.";
            return Page();
        }

        try
        {
            await sender.Send(new VincularMiCertificadoCommand(currentUser.UserId.Value, clientCert.Thumbprint), ct);
            TempData["SuccessMessage"] = "¡Certificado digital vinculado con éxito! Ahora puedes iniciar sesión con él.";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            return Page();
        }

        var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        var host = Request.Host.Host;
        
        var backUrl = "/Cuenta/Perfil";
        
        if (isDev && host == "localhost")
        {
            return Redirect($"https://localhost:49175{backUrl}");
        }

        if (host.StartsWith("cert."))
        {
            var mainDomain = host.Substring(5);
            return Redirect($"https://{mainDomain}{backUrl}");
        }

        return LocalRedirect(backUrl);
    }
}
