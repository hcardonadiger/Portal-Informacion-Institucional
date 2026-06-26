namespace Diger.TramitesEstado.Web.Pages.Reuniones;

[Authorize(Policy = "PuedeGestionarReuniones")]
public sealed class EditorModel(ISender sender, IInstitucionRepository institucionRepo, ICurrentUserService currentUser) : PageModel
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

    public string? Error { get; set; }

    public string[] Modalidades => ["Presencial", "Virtual", "Híbrida", "Otro"];
    public string[] Tipos =>
        ["Taller", "Seminario", "Charla", "Curso", "Reunión técnica", "Capacitación", "Inducción", "Conferencia", "Otro"];

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken ct)
    {
        Instituciones = await InstitucionesEnAlcanceAsync(ct);
        if (id is null) return Page();

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
