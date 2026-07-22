using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MediatR;
using Diger.TramitesEstado.Application.Usuarios.Commands.VincularMiCertificado;
using Diger.TramitesEstado.Application.Common.Interfaces;

namespace Diger.TramitesEstado.Web.Pages.Cuenta;

[Authorize]
public sealed class VincularCertificadoModel(ISender sender, ICurrentUserService currentUser, IConfiguration config) : PageModel
{
    public string? Error { get; set; }
    public string? Thumbprint { get; set; }
    public string? Subject { get; set; }
    public string? Issuer { get; set; }
    public string? ValidTo { get; set; }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (currentUser.UserId == null) return RedirectToPage("/Cuenta/Login");

        var clientCert = await HttpContext.Connection.GetClientCertificateAsync();
        if (clientCert is null)
        {
            Error = "No se detectó un certificado digital válido o cancelaste la selección del navegador. Por favor intenta de nuevo.";
            return Page();
        }

        Thumbprint = clientCert.Thumbprint;
        Subject = clientCert.Subject;
        Issuer = clientCert.Issuer;
        ValidTo = clientCert.NotAfter.ToString("dd/MM/yyyy");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string thumbprint, CancellationToken ct)
    {
        if (currentUser.UserId == null) return RedirectToPage("/Cuenta/Login");

        try
        {
            await sender.Send(new VincularMiCertificadoCommand(currentUser.UserId.Value, thumbprint), ct);
            TempData["SuccessMessage"] = "¡Certificado digital vinculado con éxito! Ahora puedes iniciar sesión con él.";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            return Page();
        }

        var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        var host = Request.Host.Host;
        
        var backUrl = "/Cuenta/Certificado";
        
        if (isDev && host == "localhost")
        {
            return Redirect($"https://localhost:49175{backUrl}");
        }

        var mainPort = config.GetValue<int>("Ports:Main", 443);
        var portSuffix = mainPort == 443 ? "" : $":{mainPort}";
        var mainDomain = host.StartsWith("cert.") ? host.Substring(5) : host;
        
        return Redirect($"https://{mainDomain}{portSuffix}{backUrl}");
    }
}
