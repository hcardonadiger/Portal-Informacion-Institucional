namespace Diger.TramitesEstado.Web.Pages.Reuniones;

[Authorize(Policy = "PuedeGestionarReuniones")]
public sealed class EditorModel(
    ISender sender, IInstitucionRepository institucionRepo,
    ICurrentUserService currentUser, IWebHostEnvironment env) : PageModel
{
    public int? ReunionId { get; private set; }
    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];

    private async Task<IReadOnlyList<Institucion>> InstitucionesEnAlcanceAsync(CancellationToken ct)
    {
        var insts = await institucionRepo.GetAllActivasAsync(ct);
        return currentUser.EsGlobal ? insts
            : insts.Where(i => currentUser.InstitucionesAsignadas.Contains(i.Id)).ToList();
    }

    [BindProperty] public ReunionFormDto      Datos      { get; set; } = new();
    [BindProperty] public List<AsistenteInput> Asistentes { get; set; } = [];
    [BindProperty] public List<AcuerdoInput>   Acuerdos   { get; set; } = [];
    [BindProperty] public IFormFile?          Foto1File  { get; set; }
    [BindProperty] public IFormFile?          Foto2File  { get; set; }

    private static readonly string[] ExtPermitidas = [".jpg", ".jpeg", ".png", ".webp", ".gif"];

    /// <summary>Guarda una imagen subida en wwwroot/uploads/reuniones y devuelve su ruta relativa.</summary>
    private async Task<string?> GuardarFotoAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return null;
        if (file.Length > 5 * 1024 * 1024) throw new DomainException("La imagen supera el límite de 5 MB.");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!ExtPermitidas.Contains(ext)) throw new DomainException("Formato de imagen no permitido (use JPG, PNG, WEBP o GIF).");

        var dir = Path.Combine(env.WebRootPath, "uploads", "reuniones");
        Directory.CreateDirectory(dir);
        var nombre = $"{Guid.NewGuid():N}{ext}";
        await using (var fs = System.IO.File.Create(Path.Combine(dir, nombre)))
            await file.CopyToAsync(fs, ct);
        return $"/uploads/reuniones/{nombre}";
    }

    public string? Error { get; set; }

    public string[] Modalidades => ["Presencial", "Virtual", "Híbrida", "Otro"];
    public string[] Tipos =>
        ["Taller", "Seminario", "Charla", "Curso", "Reunión técnica", "Capacitación", "Inducción", "Conferencia", "Otro"];

    public async Task<IActionResult> OnGetAsync(int? id, DateOnly? fecha, CancellationToken ct)
    {
        Instituciones = await InstitucionesEnAlcanceAsync(ct);
        if (id is null)
        {
            if (fecha is { } f) Datos.Fecha = f;   // pre-llenado desde el calendario
            return Page();
        }

        try
        {
            var d = await sender.Send(new GetReunionByIdQuery(id.Value), ct);
            ReunionId  = d.Id;
            Datos      = d.Datos;
            Asistentes = d.Asistentes;
            Acuerdos   = d.Acuerdos;
            return Page();
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnPostAsync(int? id, CancellationToken ct)
    {
        ReunionId = id;
        Instituciones = await InstitucionesEnAlcanceAsync(ct);

        if (string.IsNullOrWhiteSpace(Datos.Titulo))
        {
            Error = "El título de la reunión es obligatorio.";
            return Page();
        }

        try
        {
            var f1 = await GuardarFotoAsync(Foto1File, ct); if (f1 is not null) Datos.Foto1Url = f1;
            var f2 = await GuardarFotoAsync(Foto2File, ct); if (f2 is not null) Datos.Foto2Url = f2;

            if (id is null)
                await sender.Send(new CrearReunionCommand(Datos, Asistentes, Acuerdos), ct);
            else
                await sender.Send(new ActualizarReunionCommand(id.Value, Datos, Asistentes, Acuerdos), ct);

            TempData["SuccessMsg"] = id is null ? "Reunión creada." : "Reunión actualizada.";
            return RedirectToPage("/Reuniones/Index");
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
            return Page();
        }
    }
}
