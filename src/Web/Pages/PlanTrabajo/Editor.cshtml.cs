using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.PlanTrabajo.Commands;
using Diger.TramitesEstado.Application.PlanTrabajo.Common;
using Diger.TramitesEstado.Application.PlanTrabajo.Queries;
using Diger.TramitesEstado.Application.Tickets.Common;
using Diger.TramitesEstado.Application.Tickets.Queries.GetUsuariosAsignables;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.PlanTrabajo;

[Authorize]
public sealed class EditorModel(ISender sender) : PageModel
{
    public PlanTrabajoDetailDto Plan { get; private set; } = default!;
    public IReadOnlyList<UsuarioAsignableDto> Usuarios { get; private set; } = [];
    public bool PuedeEditar => User.CanMutate() && Plan.Estado != EstadoPlanTrabajo.Cerrado;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        try   { Plan = await sender.Send(new GetPlanTrabajoByIdQuery(id), ct); }
        catch (NotFoundException) { return NotFound(); }

        if (User.CanMutate() && Plan.Estado != EstadoPlanTrabajo.Cerrado)
            Usuarios = await sender.Send(new GetUsuariosAsignablesQuery(), ct);

        return Page();
    }

    // ── Guardar / crear meta ────────────────────────────────────────────────
    public sealed record GuardarMetaRequest(
        int?       MetaId,
        string     NombreTramite,
        string?    FechaEstimadaInicio,
        string?    FechaEstimadaFin,
        string?    FechaRealFin,
        Guid?      ResponsableId,
        int        Estado,
        string?    Observaciones,
        int?       ExpedienteId,
        int?       ExpedienteTramiteIndex);

    public async Task<IActionResult> OnPostGuardarMetaAsync(int id, [FromBody] GuardarMetaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.NombreTramite))
            return BadRequest(new { ok = false, error = "El nombre del trámite es requerido." });

        try
        {
            var metaId = await sender.Send(new GuardarMetaCommand(
                id, req.MetaId,
                req.NombreTramite,
                ParseDate(req.FechaEstimadaInicio),
                ParseDate(req.FechaEstimadaFin),
                ParseDate(req.FechaRealFin),
                req.ResponsableId,
                (EstadoMeta)req.Estado,
                req.Observaciones,
                req.ExpedienteId,
                req.ExpedienteTramiteIndex), ct);

            return new JsonResult(new { ok = true, metaId });
        }
        catch (Exception ex)
        {
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

    // ── Eliminar meta ───────────────────────────────────────────────────────
    public sealed record EliminarMetaRequest(int MetaId);

    public async Task<IActionResult> OnPostEliminarMetaAsync(int id, [FromBody] EliminarMetaRequest req, CancellationToken ct)
    {
        try
        {
            await sender.Send(new EliminarMetaCommand(req.MetaId, id), ct);
            return new JsonResult(new { ok = true });
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    // ── Cambiar estado del plan ─────────────────────────────────────────────
    public sealed record CambiarEstadoRequest(int NuevoEstado);

    public async Task<IActionResult> OnPostCambiarEstadoAsync(int id, [FromBody] CambiarEstadoRequest req, CancellationToken ct)
    {
        try
        {
            await sender.Send(new CambiarEstadoPlanCommand(id, (EstadoPlanTrabajo)req.NuevoEstado), ct);
            return new JsonResult(new { ok = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { ok = false, error = ex.Message });
        }
    }

    // ── Buscar expedientes (para el picker de la meta) ──────────────────────
    public async Task<IActionResult> OnGetBuscarExpedientesAsync(
        int id, string? q, string? institucionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(institucionId))
            return BadRequest();

        var resultados = await sender.Send(
            new BuscarExpedientesParaPlanQuery(institucionId.Trim(), q?.Trim()), ct);

        return new JsonResult(resultados.Select(r => new { r.Id, r.Codigo, r.EstadoExp, r.Analista }));
    }

    // ── Trámites de un expediente (paso 2 del picker) ───────────────────────
    public async Task<IActionResult> OnGetTramitesExpedienteAsync(int id, int expedienteId, CancellationToken ct)
    {
        var tramites = await sender.Send(new GetTramitesDeExpedienteQuery(expedienteId), ct);
        return new JsonResult(tramites.Select(t => new { t.TramiteIndex, t.Nombre, t.AreaResponsable }));
    }

    // ── Actualizar observaciones del plan ───────────────────────────────────
    public sealed record ActualizarObsRequest(string? Observaciones);

    public async Task<IActionResult> OnPostActualizarObsAsync(int id, [FromBody] ActualizarObsRequest req, CancellationToken ct)
    {
        await sender.Send(new ActualizarPlanCommand(id, req.Observaciones), ct);
        return new JsonResult(new { ok = true });
    }

    private static DateOnly? ParseDate(string? s) =>
        DateOnly.TryParse(s, out var d) ? d : null;
}
