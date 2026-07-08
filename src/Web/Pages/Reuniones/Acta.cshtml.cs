namespace Diger.TramitesEstado.Web.Pages.Reuniones;

[Authorize]
public sealed class ActaModel(ISender sender) : PageModel
{
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
}
