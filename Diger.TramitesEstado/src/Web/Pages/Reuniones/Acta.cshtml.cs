namespace Diger.TramitesEstado.Web.Pages.Reuniones;

[Authorize]
public sealed class ActaModel(ISender sender) : PageModel
{
    public int ReunionId { get; private set; }
    public ReunionFormDto      Datos      { get; private set; } = new();
    public List<AsistenteInput> Asistentes { get; private set; } = [];
    public List<AcuerdoInput>   Acuerdos   { get; private set; } = [];
    public string? InstitucionNombre { get; private set; }

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

            return Page();
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}
