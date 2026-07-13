using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MediatR;
using Diger.TramitesEstado.Application.Usuarios.Commands.ActualizarMiPerfil;
using Diger.TramitesEstado.Application.Usuarios.Commands.VincularMiCertificado;
using Diger.TramitesEstado.Application.Usuarios.Queries.GetUsuarioById;
using Diger.TramitesEstado.Application.Common.Interfaces;

namespace Diger.TramitesEstado.Web.Pages.Cuenta;

public sealed class PerfilModel(ISender sender, ICurrentUserService currentUser) : PageModel
{
    [BindProperty] public string Nombre { get; set; } = string.Empty;
    [BindProperty] public string Correo { get; set; } = string.Empty;
    [BindProperty] public string? PasswordActual { get; set; }
    [BindProperty] public string? PasswordNuevo { get; set; }

    public bool TieneCertificadoVinculado { get; set; }
    public string CertUrl { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (currentUser.UserId == null) return RedirectToPage("/Cuenta/Login");

        var dto = await sender.Send(new GetUsuarioByIdQuery(currentUser.UserId.Value), ct);
        
        Nombre = dto.Nombre;
        Correo = dto.Correo;
        TieneCertificadoVinculado = !string.IsNullOrWhiteSpace(dto.CertificadoThumbprint);

        var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        CertUrl = isDev 
            ? "https://localhost:49176/Cuenta/VincularCertificado" 
            : $"https://cert.{Request.Host.Host.Replace("cert.", "")}/Cuenta/VincularCertificado";

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await LoadDataAsync(ct);
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
            await LoadDataAsync(ct);
            return Page();
        }
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
        CertUrl = isDev 
            ? "https://localhost:49176/Cuenta/VincularCertificado" 
            : $"https://cert.{Request.Host.Host.Replace("cert.", "")}/Cuenta/VincularCertificado";
    }
}
