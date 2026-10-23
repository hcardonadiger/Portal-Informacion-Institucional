using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Application.Notificaciones.Queries;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.Notificaciones;

[Authorize]
[PermisoNoRequerido("Autoservicio: cada usuario ve el detalle de sus propias notificaciones.")]
public sealed class DetalleModel(ISender sender, INotificacionService notifSvc, ICurrentUserService usuario) : PageModel
{
    public NotificacionDetalleDto Aviso { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        try { Aviso = await sender.Send(new GetNotificacionDetalleQuery(id), ct); }
        catch (NotFoundException) { return NotFound(); }

        // Abrirlo es leerlo: lo nuevo que llegue del mismo tipo empieza otro aviso.
        if (!Aviso.Leida && usuario.UserId is { } uid)
            await notifSvc.MarcarLeidaAsync(id, uid, ct);

        return Page();
    }
}
