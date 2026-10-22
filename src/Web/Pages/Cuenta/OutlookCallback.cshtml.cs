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
// [Authorize] declara la intención, pero no basta: AllowAnonymousToFolder("/Cuenta") gana en el
// pipeline de autorización y la página corre igual sin sesión. Por eso el handler se guarda solo,
// como Perfil.
[Authorize]
[PermisoNoRequerido("Autoservicio: cada usuario conecta su propia cuenta Microsoft.")]
public sealed class OutlookCallbackModel(ISender sender) : PageModel
{
    private const string NoCorresponde =
        "La respuesta de Microsoft no corresponde a esta sesión. Vuelva a pulsar «Conectar mi Outlook».";

    public async Task<IActionResult> OnGetAsync(string? code, string? state, string? error, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToPage("/Cuenta/Login");

        // Peek y no lectura: un enlace falsificado que llegue antes que la respuesta real no debe
        // consumir el estado y el verificador de una conexión en curso.
        var esperado    = TempData.Peek("OutlookEstado") as string;
        var verificador = TempData.Peek("OutlookVerificador") as string;

        // El estado se valida antes que nada, también en las respuestas de error (Microsoft
        // devuelve el state en ellas): lo que no venga de esta sesión no toca nada.
        if (esperado is null || verificador is null || !Iguales(state, esperado))
            return Volver(NoCorresponde);

        // Desde aquí la respuesta es la de esta sesión: el estado se gasta, no se repite.
        TempData.Remove("OutlookEstado");
        TempData.Remove("OutlookVerificador");

        // Mensajes fijos: lo que trae la URL nunca llega al aviso.
        if (!string.IsNullOrEmpty(error))
            return Volver(error == "access_denied"
                ? "No se conectó: la solicitud se canceló en Microsoft."
                : "Microsoft no completó la conexión. Vuelva a intentarlo; si se repite, avise a TI.");

        if (string.IsNullOrEmpty(code))
            return Volver(NoCorresponde);

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
