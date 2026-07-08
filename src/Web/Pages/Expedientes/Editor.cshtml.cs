using System.Text.Json;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.Expedientes;

[Authorize]
public sealed class EditorModel(ISender sender, IInstitucionRepository institucionRepo) : PageModel
{
    public int?    ExpId   { get; private set; }
    public string  Codigo  { get; private set; } = "";
    public string? ExpJson { get; private set; }   // OriginalExpedienteDto serializado (edición)

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken ct)
    {
        if (id is null) return Page();

        try
        {
            var detalle = await sender.Send(new GetExpedienteByIdQuery(id.Value), ct);
            ExpId   = detalle.Id;
            Codigo  = detalle.Codigo;
            var original = OriginalShapeMapper.FromInput(detalle.Datos);
            ExpJson = JsonSerializer.Serialize(original, JsonOpts);
            return Page();
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Autocompletado de contactos por institución (consumido por expediente.js).</summary>
    public async Task<IActionResult> OnGetContactosAsync(string? institucion, CancellationToken ct)
        => new JsonResult(await sender.Send(new GetContactosPorInstitucionQuery(institucion ?? ""), ct));

    public async Task<IActionResult> OnPostAsync(int? id, [FromBody] OriginalExpedienteDto datos, CancellationToken ct)
    {
        // Autorización por rol (Razor Pages no aplica [Authorize] a nivel de handler).
        // Admin, Jefes y Empleado pueden mutar; el alcance institucional (filtro + validación
        // en CrearExpediente) limita sobre qué instituciones pueden actuar. Consultor es solo lectura.
        if (!User.CanMutate())
            return Forbid();

        // Resolver la institución (el editor envía el nombre)
        var instituciones = await institucionRepo.GetAllActivasAsync(ct);
        var inst = instituciones.FirstOrDefault(i =>
            string.Equals(i.Nombre, datos.Inst?.Trim(), StringComparison.OrdinalIgnoreCase));
        var institucionId = inst?.Id ?? string.Empty;

        var input = OriginalShapeMapper.ToInput(datos, institucionId);

        int expedienteId;
        if (id is null)
            expedienteId = await sender.Send(new CrearExpedienteCommand(input), ct);
        else
        {
            await sender.Send(new ActualizarExpedienteCommand(id.Value, input), ct);
            expedienteId = id.Value;
        }

        return new JsonResult(new { id = expedienteId });
    }
}
