using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.Expedientes;

[Authorize]
public sealed class SeguimientoModel(ISender sender) : PageModel
{
    public SeguimientoExpedienteDto Data { get; private set; } = default!;

    public bool PuedeGestionar => User.CanMutate();

    public async Task<IActionResult> OnGetAsync(int id, int? t, CancellationToken ct)
    {
        try { Data = await sender.Send(new GetSeguimientoExpedienteQuery(id, t), ct); return Page(); }
        catch (NotFoundException) { return NotFound(); }
    }

    public async Task<IActionResult> OnPostSubAsync(int id, int tramite, string subId, int estado, CancellationToken ct)
    {
        if (!PuedeGestionar) return Forbid();
        try
        {
            await sender.Send(new ActualizarSubEtapaCommand(id, tramite, subId, estado), ct);
            return new JsonResult(new { ok = true });
        }
        catch (Exception ex) when (ex is DomainException or NotFoundException)
        {
            return BadRequest(new { ok = false, msg = ex.Message });
        }
    }

    public async Task<IActionResult> OnPostAplicaAsync(int id, int tramite, string etapa, bool aplica, CancellationToken ct)
    {
        if (!PuedeGestionar) return Forbid();
        try
        {
            await sender.Send(new CambiarAplicaEtapaCommand(id, tramite, etapa, aplica), ct);
            return new JsonResult(new { ok = true });
        }
        catch (Exception ex) when (ex is DomainException or NotFoundException)
        {
            return BadRequest(new { ok = false, msg = ex.Message });
        }
    }
}
