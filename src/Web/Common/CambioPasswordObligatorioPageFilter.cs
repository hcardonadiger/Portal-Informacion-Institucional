using Diger.TramitesEstado.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Web.Common;

/// <summary>
/// El portón del cambio de contraseña por única vez. A partir de la fecha de corte
/// (<c>Auth:CambioPasswordObligatorioDesde</c>), una sesión cuya contraseña no haya elegido su
/// propio dueño después de ese corte no puede ir a ninguna parte salvo a cambiarla.
///
/// No consulta la base en ninguna petición: la fecha viaja en el claim <c>diger:pwdfecha</c> que
/// arma <see cref="Web.Security.SesionClaimsFactory"/> —punto único que comparten el login por
/// contraseña, el de certificado y el MCP— así que el certificado queda retenido por este mismo
/// código, sin una segunda regla que mantener. Una cookie emitida antes de este despliegue no
/// trae el claim y por eso cuenta como pendiente: es lo que queremos, no un agujero.
///
/// Falla abierto en un solo caso, a propósito: sin fecha configurada no retiene a nadie. Una
/// clave mal escrita no puede dejar al portal entero girando en la pantalla de cambio.
///
/// Queda fuera de su alcance el servidor MCP, que no pasa por filtros de página: es por token y
/// no interactivo, y un portón de pantalla no tiene dónde retener a un agente.
/// </summary>
public sealed class CambioPasswordObligatorioPageFilter(IOptions<AuthOptions> opciones) : IAsyncPageFilter
{
    /// <summary>
    /// Lo mínimo que tiene que seguir abierto para que la retención no sea una trampa: la
    /// pantalla del cambio (si no, se muerde la cola), las de entrar y salir, las de acceso
    /// denegado y la ayuda del certificado. Todo lo demás se retiene, el perfil incluido.
    /// </summary>
    private static readonly HashSet<string> PaginasExentas = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Cuenta/CambioObligatorio",
        "/Cuenta/Login",
        "/Cuenta/LoginCertificado",
        "/Cuenta/Logout",
        "/Cuenta/Denegado",
        "/Cuenta/AyudaCertificado",
        "/Error"
    };

    public async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (!DebeRetener(context.HttpContext, PaginaDe(context.ActionDescriptor)))
        {
            await next();
            return;
        }

        context.Result = new RedirectToPageResult("/Cuenta/CambioObligatorio");
    }

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    private static string PaginaDe(Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor descriptor) =>
        (descriptor as PageActionDescriptor)?.ViewEnginePath ?? string.Empty;

    /// <summary>Separado del handler, y público, para poder probar la decisión sin levantar un
    /// pipeline de Razor Pages: es la parte que tiene reglas y la que puede equivocarse.</summary>
    public bool DebeRetener(HttpContext http, string pagina)
    {
        if (PaginasExentas.Contains(pagina)) return false;

        // El auto-registro público por QR es anónimo por convención: retener ahí no protege nada
        // y le rompería el ingreso a quien pasa por esa pantalla con su sesión abierta.
        if (pagina.StartsWith("/Asistencia/", StringComparison.OrdinalIgnoreCase)) return false;

        return CambioPasswordObligatorio.Pendiente(http.User, opciones.Value.CorteCambioPasswordUtc);
    }
}
