using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MediatR;
using Diger.TramitesEstado.Application.Usuarios.Commands.VincularMiCertificado;
using Diger.TramitesEstado.Application.Usuarios.Queries.GetUsuarioById;
using Diger.TramitesEstado.Application.Common.Interfaces;

namespace Diger.TramitesEstado.Web.Pages.Cuenta;

public sealed class CertificadoModel(ISender sender, ICurrentUserService currentUser, IConfiguration config) : PageModel
{
    public bool TieneCertificadoVinculado { get; private set; }
    public string CertUrl { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (currentUser.UserId == null) return RedirectToPage("/Cuenta/Login");
        await CargarAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostDesvincularAsync(CancellationToken ct)
    {
        if (currentUser.UserId == null) return RedirectToPage("/Cuenta/Login");

        try
        {
            await sender.Send(new VincularMiCertificadoCommand(currentUser.UserId.Value, null), ct);
            TempData["SuccessMessage"] = "Certificado desvinculado correctamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage();
    }

    private async Task CargarAsync(CancellationToken ct)
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
