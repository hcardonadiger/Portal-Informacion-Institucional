using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.Expedientes;

[Authorize]
public sealed class CronogramaModel(ISender sender) : PageModel
{
    public CronogramaExpedienteDto Data { get; private set; } = default!;
    public bool PuedeGestionar => User.CanMutate();

    public async Task<IActionResult> OnGetAsync(int id, int? t, CancellationToken ct)
    {
        try { Data = await sender.Send(new GetCronogramaExpedienteQuery(id, t), ct); return Page(); }
        catch (NotFoundException) { return NotFound(); }
    }

    public sealed record GuardarRequest(
        string    EtapaNum,
        string?   FechaInicio,
        string?   FechaFin,
        string?   FechaRealFin,
        string?   Responsable,
        string?   Observacion);

    public async Task<IActionResult> OnPostGuardarAsync(int id, int tramite, [FromBody] GuardarRequest req, CancellationToken ct)
    {
        if (!PuedeGestionar) return Forbid();
        try
        {
            await sender.Send(new GuardarEtapaCronogramaCommand(
                id, tramite, req.EtapaNum,
                ParseDate(req.FechaInicio),
                ParseDate(req.FechaFin),
                ParseDate(req.FechaRealFin),
                req.Responsable,
                req.Observacion), ct);
            return new JsonResult(new { ok = true });
        }
        catch (Exception ex) when (ex is DomainException or NotFoundException)
        {
            return BadRequest(new { ok = false, msg = ex.Message });
        }
    }

    /// <summary>Guardado centralizado: aplica la etapa a todos los trámites del expediente.</summary>
    public async Task<IActionResult> OnPostGuardarTodosAsync(int id, [FromBody] GuardarRequest req, CancellationToken ct)
    {
        if (!PuedeGestionar) return Forbid();
        try
        {
            await sender.Send(new GuardarEtapaCronogramaTodosCommand(
                id, req.EtapaNum,
                ParseDate(req.FechaInicio),
                ParseDate(req.FechaFin),
                ParseDate(req.FechaRealFin),
                req.Responsable,
                req.Observacion), ct);
            return new JsonResult(new { ok = true });
        }
        catch (Exception ex) when (ex is DomainException or NotFoundException)
        {
            return BadRequest(new { ok = false, msg = ex.Message });
        }
    }

    private static DateOnly? ParseDate(string? s) =>
        !string.IsNullOrWhiteSpace(s) && DateOnly.TryParse(s, out var d) ? d : null;
}
