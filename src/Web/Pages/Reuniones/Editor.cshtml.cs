namespace Diger.TramitesEstado.Web.Pages.Reuniones;

[Authorize]
public sealed class EditorModel(
    ISender sender, IInstitucionRepository institucionRepo,
    ICurrentUserService currentUser, IWebHostEnvironment env) : PageModel
{
    public bool EsAdmin => User.IsInRole(nameof(RolUsuario.Administrador));
    public int? ReunionId { get; private set; }
    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];
    public IReadOnlyList<ContactoDto> ContactosDirectorio { get; private set; } = [];

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

        var dir = Path.Combine(env.ContentRootPath, "App_Data", "uploads", "reuniones");
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
        if (!EsAdmin)
            return Forbid();

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

            var instNombres = Datos.InstitucionesIds
                .Select(iid => Instituciones.FirstOrDefault(i => i.Id == iid)?.Nombre)
                .OfType<string>()
                .ToList();
            if (instNombres.Count > 0)
                ContactosDirectorio = await sender.Send(new GetContactosQuery(Instituciones: instNombres), ct);

            return Page();
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnPostAsync(int? id, CancellationToken ct)
    {
        if (!EsAdmin)
            return Forbid();
        ReunionId = id;
        Instituciones = await InstitucionesEnAlcanceAsync(ct);

        if (string.IsNullOrWhiteSpace(Datos.Titulo))
        {
            Error = "El título de la reunión es obligatorio.";
            return Page();
        }

        try
        {
            await GuardarAsync(id, ct);
            TempData["SuccessMsg"] = id is null ? "Reunión creada." : "Reunión actualizada.";
            return RedirectToPage("/Reuniones/Index");
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
            return Page();
        }
    }

    /// <summary>Autoguardado del paso "Generales": crea o actualiza la reunión con lo capturado
    /// hasta ahora y recarga la misma página, donde ya queda disponible el enlace/QR de
    /// auto-registro (requiere que la reunión exista para tener un token).</summary>
    public async Task<IActionResult> OnPostGuardarContinuarAsync(int? id, [FromForm] int paso, CancellationToken ct)
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
            var reunionId = await GuardarAsync(id, ct);
            TempData["SuccessMsg"] = "Cambios guardados.";
            return RedirectToPage(new { id = reunionId, paso = paso });
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
            return Page();
        }
    }

    /// <summary>Guarda las fotos adjuntas y crea/actualiza la reunión. Devuelve el Id resultante.</summary>
    private async Task<int> GuardarAsync(int? id, CancellationToken ct)
    {
        var f1 = await GuardarFotoAsync(Foto1File, ct); if (f1 is not null) Datos.Foto1Url = f1;
        var f2 = await GuardarFotoAsync(Foto2File, ct); if (f2 is not null) Datos.Foto2Url = f2;

        if (id is null)
            return await sender.Send(new CrearReunionCommand(Datos, Asistentes, Acuerdos), ct);

        await sender.Send(new ActualizarReunionCommand(id.Value, Datos, Asistentes, Acuerdos), ct);
        return id.Value;
    }
}
