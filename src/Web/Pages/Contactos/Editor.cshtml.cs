namespace Diger.TramitesEstado.Web.Pages.Contactos;

[Authorize(Policy = "PuedeGestionarContactos")]
public sealed class EditorModel(ISender sender, IInstitucionRepository institucionRepo, ICurrentUserService currentUser) : PageModel
{
    public int? ContactoId { get; private set; }
    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];

    private async Task<IReadOnlyList<Institucion>> InstitucionesEnAlcanceAsync(CancellationToken ct)
    {
        var insts = await institucionRepo.GetAllActivasAsync(ct);
        return currentUser.EsGlobal ? insts
            : insts.Where(i => currentUser.InstitucionesAsignadas.Contains(i.Id)).ToList();
    }

    [BindProperty] public string  Nombre        { get; set; } = string.Empty;
    [BindProperty] public int     InstitucionId { get; set; }
    [BindProperty] public string? Cargo         { get; set; }
    [BindProperty] public string? Correo      { get; set; }
    [BindProperty] public string? Telefono    { get; set; }
    [BindProperty] public string? Notas       { get; set; }

    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken ct)
    {
        Instituciones = await InstitucionesEnAlcanceAsync(ct);
        if (id is null) return Page();

        try
        {
            var c = await sender.Send(new GetContactoByIdQuery(id.Value), ct);
            ContactoId    = c.Id;
            Nombre        = c.Nombre;
            InstitucionId = c.InstitucionId;
            Cargo         = c.Cargo;
            Correo      = c.Correo;
            Telefono    = c.Telefono;
            Notas       = c.Notas;
            return Page();
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnPostAsync(int? id, CancellationToken ct)
    {
        ContactoId = id;
        Instituciones = await InstitucionesEnAlcanceAsync(ct);
        if (!ModelState.IsValid) return Page();

        try
        {
            if (id is null)
                await sender.Send(new CrearContactoCommand(Nombre, InstitucionId, Cargo, Correo, Telefono, Notas), ct);
            else
                await sender.Send(new ActualizarContactoCommand(id.Value, Nombre, InstitucionId, Cargo, Correo, Telefono, Notas), ct);

            TempData["SuccessMsg"] = id is null ? "Contacto creado." : "Contacto actualizado.";
            return RedirectToPage("/Contactos/Index");
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
            return Page();
        }
    }
}
