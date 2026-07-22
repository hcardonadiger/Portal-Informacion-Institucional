using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Infrastructure.Security;
using System.Security.Claims;

namespace Diger.TramitesEstado.Web.Pages.Notificaciones;

[Authorize]
public sealed class IndexModel(INotificacionService notifSvc) : PageModel
{
    public IReadOnlyList<NotificacionDto> Notificaciones { get; private set; } = [];
    public int TotalNoLeidas { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        var uid = GetUserId();
        if (uid is null) return;

        // Para la página completa cargamos también las leídas (últimas 60)
        Notificaciones = await notifSvc.GetNoLeidasAsync(uid.Value, max: 60, ct);
        TotalNoLeidas  = Notificaciones.Count;
    }

    public async Task<IActionResult> OnPostMarcarLeidaAsync(int id, CancellationToken ct)
    {
        var uid = GetUserId();
        if (uid is null) return Forbid();
        await notifSvc.MarcarLeidaAsync(id, uid.Value, ct);
        return new JsonResult(new { ok = true });
    }

    public async Task<IActionResult> OnPostMarcarTodasAsync(CancellationToken ct)
    {
        var uid = GetUserId();
        if (uid is null) return Forbid();
        await notifSvc.MarcarTodasLeidasAsync(uid.Value, ct);
        return new JsonResult(new { ok = true });
    }

    private Guid? GetUserId()
    {
        var val = User.FindFirstValue(AppClaims.UserId);
        return Guid.TryParse(val, out var id) ? id : null;
    }
}
