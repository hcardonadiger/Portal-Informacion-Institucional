using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MediatR;
using Diger.TramitesEstado.Application.Usuarios.Commands.VincularMiCertificado;
using Diger.TramitesEstado.Application.Usuarios.Queries.GetUsuarioById;
using Diger.TramitesEstado.Application.Common.Interfaces;

namespace Diger.TramitesEstado.Web.Pages.Cuenta.Manage;

public sealed class CertificateModel(ISender sender, ICurrentUserService currentUser, IConfiguration config) : PageModel
{
    public bool TieneCertificadoVinculado { get; set; }
    public string CertUrl { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (currentUser.UserId == null) return RedirectToPage("/Cuenta/Login");

        await LoadDataAsync(ct);

        if (TieneCertificadoVinculado)
        {
            if (TempData["CertVerificado"] as bool? != true)
            {
                var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
                var certPort = config.GetValue<int>("Ports:Cert", 444);
                var host = Request.Host.Host;
                
                var verifyUrl = isDev 
                    ? "https://localhost:49176/Cuenta/VerificarCertificado" 
                    : $"https://{host}:{certPort}/Cuenta/VerificarCertificado";

                return Redirect(verifyUrl);
            }
            TempData.Keep("CertVerificado");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDesvincularAsync(CancellationToken ct)
    {
        try
        {
            await sender.Send(new VincularMiCertificadoCommand(currentUser.UserId!.Value, null), ct);
            TempData["SuccessMessage"] = "Certificado desvinculado correctamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        
        return RedirectToPage();
    }

    private async Task LoadDataAsync(CancellationToken ct)
    {
        var dto = await sender.Send(new GetUsuarioByIdQuery(currentUser.UserId!.Value), ct);
        TieneCertificadoVinculado = !string.IsNullOrWhiteSpace(dto.CertificadoThumbprint);
        
        var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        var certPort = config.GetValue<int>("Ports:Cert", 444);
        var host = Request.Host.Host;
        
        CertUrl = isDev 
            ? "https://localhost:49176/Cuenta/VincularCertificado" 
            : $"https://{host}:{certPort}/Cuenta/VincularCertificado";
    }
}
