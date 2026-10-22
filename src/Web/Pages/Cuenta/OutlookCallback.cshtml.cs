using System.Security.Cryptography;
using System.Text;
using Diger.TramitesEstado.Application.CuentaMicrosoft;
using Diger.TramitesEstado.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Diger.TramitesEstado.Web.Pages.Cuenta;

/// <summary>
/// Adonde Microsoft devuelve al usuario tras aceptar. Solo canjea si el <c>state</c> es el que
/// esta misma sesión emitió en Perfil: sin eso, un enlace preparado por otro podría conectar la
/// cuenta Microsoft de un tercero al usuario que lo abra.
/// </summary>
// [Authorize] explícito: la carpeta /Cuenta está exenta por convención.
[Authorize]
[PermisoNoRequerido("Autoservicio: cada usuario conecta su propia cuenta Microsoft.")]
public sealed class OutlookCallbackModel(ISender sender) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string? code, string? state, string? error, string? error_description, CancellationToken ct)
    {
        var esperado    = TempData["OutlookEstado"] as string;
        var verificador = TempData["OutlookVerificador"] as string;

        if (!string.IsNullOrEmpty(error))
            return Volver(error == "access_denied"
                ? "No se conectó: la solicitud se canceló en Microsoft."
                : $"Microsoft no completó la conexión ({error}).");

        if (string.IsNullOrEmpty(code) || esperado is null || verificador is null || !Iguales(state, esperado))
            return Volver("La respuesta de Microsoft no corresponde a esta sesión. Vuelva a pulsar «Conectar mi Outlook».");

        try
        {
            await sender.Send(new CompletarConexionOutlookCommand(code, verificador, Url.PageLink("/Cuenta/OutlookCallback")!), ct);
        }
        catch (DomainException ex)
        {
            return Volver(ex.Message);
        }

        // "SuccessMessage" es la clave que pinta _CuentaLayout; "SuccessMsg" no se mostraría en Perfil.
        TempData["SuccessMessage"] = "Outlook conectado. Ya puede enviar reuniones a su calendario.";
        return RedirectToPage("/Cuenta/Perfil");
    }

    private RedirectToPageResult Volver(string aviso)
    {
        TempData["OutlookAviso"] = aviso;
        return RedirectToPage("/Cuenta/Perfil");
    }

    private static bool Iguales(string? a, string b) =>
        a is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
