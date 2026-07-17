using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MediatR;
using Diger.TramitesEstado.Application.Usuarios.Queries.GetUsuarioById;
using Diger.TramitesEstado.Application.Common.Interfaces;

namespace Diger.TramitesEstado.Web.Pages.Cuenta;

[Authorize]
public sealed class VerificarCertificadoModel(ISender sender, ICurrentUserService currentUser, IConfiguration config) : PageModel
{
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (currentUser.UserId == null) return RedirectToPage("/Cuenta/Login");

        var user = await sender.Send(new GetUsuarioByIdQuery(currentUser.UserId.Value), ct);
        
        // Si no tiene certificado vinculado, no hay nada que verificar
        if (string.IsNullOrEmpty(user.CertificadoThumbprint))
        {
            TempData["CertVerificado"] = true;
            return RedirectToManageCertificate();
        }

        var clientCert = await HttpContext.Connection.GetClientCertificateAsync();
        if (clientCert is null)
        {
            Error = "No se detectó el certificado digital. Asegúrate de conectar tu Token/Smart Card e intentarlo nuevamente.";
            return Page();
        }

        if (clientCert.Thumbprint.Equals(user.CertificadoThumbprint, StringComparison.OrdinalIgnoreCase))
        {
            TempData["CertVerificado"] = true;
            return RedirectToManageCertificate();
        }
        else
        {
            Error = "El certificado presentado no coincide con el que tienes vinculado a tu cuenta. Se leyó la huella: " + clientCert.Thumbprint;
            return Page();
        }
    }

    private IActionResult RedirectToManageCertificate()
    {
        var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        var host = Request.Host.Host;
        var backUrl = "/Cuenta/Manage/Certificate";
        
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
