using System.Text.Json;
using Diger.TramitesEstado.Application.Tickets.Common;
using Diger.TramitesEstado.Application.Tickets.Queries.GetUsuariosAsignables;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.Expedientes;

[Authorize]
public sealed class EditorModel(ISender sender, IInstitucionRepository institucionRepo, IWebHostEnvironment env) : PageModel
{
    public int?    ExpId   { get; private set; }
    public string  Codigo  { get; private set; } = "";
    public string? ExpJson { get; private set; }   // OriginalExpedienteDto serializado (edición)
    public List<string> Plantillas { get; private set; } = [];
    public IReadOnlyList<UsuarioAsignableDto> Usuarios { get; private set; } = [];

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken ct)
    {
        Plantillas = await sender.Send(new Diger.TramitesEstado.Application.Expedientes.Plantillas.GetNombresPlantillasActivasQuery(), ct);
        Usuarios   = await sender.Send(new GetUsuariosAsignablesQuery(), ct);
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

    /// <summary>Busca una plantilla de Marco Legal/Requisitos por nombre exacto de trámite (copiado automático en el wizard).</summary>
    public async Task<IActionResult> OnGetPlantillaAsync(string? nombre, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return new JsonResult(null);
        var plantilla = await sender.Send(new GetPlantillaPorNombreQuery(nombre), ct);
        return new JsonResult(plantilla);
    }

    /// <summary>Sube un documento de "Documentación solicitada" y devuelve su URL (consumido por expediente.js).</summary>
    public async Task<IActionResult> OnPostSubirDocumentoAsync(IFormFile archivo, CancellationToken ct)
    {
        if (!User.CanMutate())
            return Forbid();

        var guardados = await AdjuntoStorage.GuardarAsync([archivo], env, ct, carpeta: "expedientes");
        return new JsonResult(new { url = guardados.FirstOrDefault()?.Url });
    }

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
