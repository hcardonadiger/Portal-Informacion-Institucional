namespace Diger.TramitesEstado.Web.Pages.Admin;

[Authorize(Roles = nameof(RolUsuario.Administrador))]
public sealed class PlantillaTramiteEditorModel(ISender sender) : PageModel
{
    public sealed class LegalRowVm
    {
        public string  Instrumento { get; set; } = string.Empty;
        public string? Articulos   { get; set; }
        public string? Obs         { get; set; }
    }

    public sealed class RequisitoRowVm
    {
        public string  Requisito { get; set; } = string.Empty;
        public string? Obs       { get; set; }
    }

    public int? PlantillaId { get; private set; }
    public string? Error { get; set; }

    [BindProperty] public string Nombre { get; set; } = string.Empty;
    [BindProperty] public bool   Activa { get; set; } = true;
    [BindProperty] public List<LegalRowVm>      Legal      { get; set; } = [];
    [BindProperty] public List<RequisitoRowVm>  Requisitos { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken ct)
    {
        if (id is null)
        {
            Legal = [new LegalRowVm()];
            Requisitos = [new RequisitoRowVm()];
            return Page();
        }

        try
        {
            var d = await sender.Send(new GetPlantillaByIdQuery(id.Value), ct);
            PlantillaId = d.Id;
            Nombre = d.Nombre;
            Activa = d.Activa;
            Legal = d.Legal.Select(l => new LegalRowVm { Instrumento = l.Instrumento, Articulos = l.Articulos, Obs = l.Obs }).ToList();
            Requisitos = d.Requisitos.Select(r => new RequisitoRowVm { Requisito = r.Requisito, Obs = r.Obs }).ToList();
            if (Legal.Count == 0) Legal = [new LegalRowVm()];
            if (Requisitos.Count == 0) Requisitos = [new RequisitoRowVm()];
            return Page();
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnPostAsync(int? id, CancellationToken ct)
    {
        PlantillaId = id;
        if (string.IsNullOrWhiteSpace(Nombre))
        {
            Error = "El nombre de la plantilla es obligatorio.";
            return Page();
        }

        var legalInputs = Legal
            .Where(l => !string.IsNullOrWhiteSpace(l.Instrumento))
            .Select(l => new PlantillaLegalInput(l.Instrumento, l.Articulos, l.Obs))
            .ToList();
        var reqInputs = Requisitos
            .Where(r => !string.IsNullOrWhiteSpace(r.Requisito))
            .Select(r => new PlantillaRequisitoInput(r.Requisito, r.Obs))
            .ToList();

        try
        {
            if (id is null)
                await sender.Send(new CrearPlantillaCommand(Nombre, legalInputs, reqInputs), ct);
            else
                await sender.Send(new ActualizarPlantillaCommand(id.Value, Nombre, Activa, legalInputs, reqInputs), ct);

            TempData["SuccessMsg"] = "Plantilla guardada.";
            return RedirectToPage("PlantillasTramite");
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
            return Page();
        }
    }
}
