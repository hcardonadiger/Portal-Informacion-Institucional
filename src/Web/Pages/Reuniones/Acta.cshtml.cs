namespace Diger.TramitesEstado.Web.Pages.Reuniones;

[Authorize]
public sealed class ActaModel(ISender sender, IActaPdfService actaPdf) : PageModel
{
    public bool EsAdmin => User.IsInRole(nameof(RolUsuario.Administrador));
    public int ReunionId { get; private set; }
    public ReunionFormDto      Datos      { get; private set; } = new();
    public List<AsistenteInput> Asistentes { get; private set; } = [];
    public List<AcuerdoInput>   Acuerdos   { get; private set; } = [];
    public string? InstitucionNombre { get; private set; }
    public IReadOnlyList<InstitucionRefDto> InstitucionesParticipantes { get; private set; } = [];

    /// <summary>Desglose de asistentes por institución (para la sección de desglose del acta).
    /// Cada institución convocada aparece aunque tenga cero asistentes; el texto libre de
    /// asistencia que no coincide con ninguna convocada se agrupa aparte.</summary>
    public IReadOnlyList<(string Institucion, int Cantidad)> DesgloseAsistenciaPorInstitucion { get; private set; } = [];

    // ── Hilo de reuniones enlazadas ───────────────────────────────
    public HiloDeReunionDto Hilo { get; private set; } = new(null, []);
    public IReadOnlyList<HiloMiembroRefDto> Enlazables { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        try
        {
            var d = await sender.Send(new GetReunionByIdQuery(id), ct);
            ReunionId  = d.Id;
            Datos      = d.Datos;
            Asistentes = d.Asistentes;
            Acuerdos   = d.Acuerdos;
            InstitucionNombre = d.InstitucionNombre;  // snapshot (sirve aunque la institución no esté catalogada)
            InstitucionesParticipantes = d.InstitucionesParticipantes;

            Hilo       = await sender.Send(new GetHiloDeReunionQuery(id), ct);
            Enlazables = await sender.Send(new GetReunionesEnlazablesQuery(id), ct);

            var porNombre = Asistentes
                .GroupBy(a => string.IsNullOrWhiteSpace(a.Institucion) ? "(sin institución)" : a.Institucion!.Trim())
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            var desglose = new List<(string, int)>();
            foreach (var inst in InstitucionesParticipantes)
            {
                desglose.Add((inst.Nombre, porNombre.GetValueOrDefault(inst.Nombre)));
                porNombre.Remove(inst.Nombre);
            }
            desglose.AddRange(porNombre.Select(kv => (kv.Key, kv.Value)).OrderByDescending(x => x.Value));
            DesgloseAsistenciaPorInstitucion = desglose;

            return Page();
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnGetPdfAsync(int id, CancellationToken ct)
    {
        try
        {
            var d = await sender.Send(new GetReunionByIdQuery(id), ct);
            var dto = new ActaPdfDto(d.Datos, d.Asistentes, d.Acuerdos, d.InstitucionNombre);
            var bytes = actaPdf.Generar(dto);

            var slug = string.Concat((d.Datos.Titulo ?? "reunion")
                .Where(ch => char.IsLetterOrDigit(ch) || ch is ' ' or '-' or '_'))
                .Trim().Replace(' ', '_');
            if (string.IsNullOrWhiteSpace(slug)) slug = "reunion";
            var fecha = d.Datos.Fecha?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd");
            var nombre = $"Registro_{slug}_{fecha}.pdf";

            return File(bytes, "application/pdf", nombre);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnPostEnlazarAsync(int id, int otraReunionId, CancellationToken ct)
    {
        if (!EsAdmin) return Forbid();
        if (otraReunionId <= 0)
        {
            TempData["ErrorMessage"] = "Seleccione una reunión para enlazar.";
            return RedirectToPage(new { id });
        }
        try
        {
            await sender.Send(new EnlazarReunionesCommand(id, otraReunionId), ct);
            TempData["SuccessMessage"] = "Reunión enlazada al hilo.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDesenlazarAsync(int id, CancellationToken ct)
    {
        if (!EsAdmin) return Forbid();
        try
        {
            await sender.Send(new DesenlazarReunionCommand(id), ct);
            TempData["SuccessMessage"] = "Reunión retirada del hilo.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostEnviarRecordatorioAsync(int id, string? mensaje, CancellationToken ct)
    {
        try
        {
            await sender.Send(new Diger.TramitesEstado.Application.Notificaciones.Commands.EnviarRecordatorioManual.EnviarRecordatorioReunionCommand(id, mensaje), ct);
            TempData["SuccessMessage"] = "Notificaciones de recordatorio enviadas a los asistentes con correo.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToPage(new { id });
    }
}
