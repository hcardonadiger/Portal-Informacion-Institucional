using System.Text;
using Diger.TramitesEstado.Application.Calendario.Ics;

namespace Diger.TramitesEstado.Web.Pages.Calendario;

/// <summary>
/// Sirve la agenda personal en formato iCalendar para que Outlook, Google o Apple la mantengan
/// suscrita.
///
/// <para><b>Anónima a propósito</b>: un calendario suscrito lo pide el cliente en segundo plano, sin
/// sesión ni cookies. El token de la URL es la credencial —el mismo criterio con el que ya funciona
/// el auto-registro de asistencia— y por eso la persona puede regenerarlo desde su perfil.</para>
///
/// <para>Un token inexistente, revocado o de un usuario dado de baja responde <b>404</b> y no 401:
/// no hay ninguna pantalla de sesión que ofrecerle a un cliente de calendario, y distinguir
/// «token inválido» de «token válido sin reuniones» solo le serviría a quien esté probando tokens.</para>
/// </summary>
[AllowAnonymous]
public sealed class FeedModel(ISender sender) : PageModel
{
    public async Task<IActionResult> OnGetAsync(Guid token, CancellationToken ct)
    {
        var ics = await sender.Send(new GetIcsAgendaQuery(token), ct);
        if (ics is null) return NotFound();

        // Sin caché: el sentido de la suscripción es que el cliente vea los cambios. Algunos
        // intermediarios cachean agresivamente las respuestas anónimas si no se les dice que no.
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";

        return File(Encoding.UTF8.GetBytes(ics.Contenido), ArchivoIcs.TipoContenido, ics.NombreArchivo);
    }
}
