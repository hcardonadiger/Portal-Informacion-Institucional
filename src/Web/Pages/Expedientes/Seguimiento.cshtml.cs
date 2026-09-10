using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.Expedientes;

[Authorize]
[Permission("Expedientes", AccionModulo.Ver, "Ver expedientes")]
public sealed class SeguimientoModel(ISender sender, AccesoModulosService acceso) : PageModel
{
    public SeguimientoExpedienteDto Data { get; private set; } = default!;

    public bool EsAdmin { get; private set; }
    public bool PuedeGestionar => EsAdmin;

    public async Task<IActionResult> OnGetAsync(int id, int? t, CancellationToken ct)
    {
        try { Data = await sender.Send(new GetSeguimientoExpedienteQuery(id, t), ct); }
        catch (NotFoundException) { return NotFound(); }

        EsAdmin = await acceso.PuedeEditarAsync("Expedientes", ct);
        return Page();
    }

    [Permission("Expedientes", AccionModulo.Editar, "Crear y editar expedientes")]
    public async Task<IActionResult> OnPostSubAsync(int id, int tramite, string subId, int estado, CancellationToken ct)
    {
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

    [Permission("Expedientes", AccionModulo.Editar, "Crear y editar expedientes")]
    public async Task<IActionResult> OnPostAplicaAsync(int id, int tramite, string etapa, bool aplica, CancellationToken ct)
    {
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
