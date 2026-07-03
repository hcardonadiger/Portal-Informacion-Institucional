namespace Diger.TramitesEstado.Web.Pages.Tableros;

[Authorize]
public sealed class ExpedientesModel(ISender sender, IInstitucionRepository institucionRepo, ICurrentUserService currentUser) : PageModel
{
    public ExpedientesDashboardDto Data { get; private set; } = default!;
    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];
    public int? InstitucionId { get; private set; }

    public async Task OnGetAsync(int? institucionId, CancellationToken ct)
    {
        InstitucionId = institucionId;
        var insts = await institucionRepo.GetAllActivasAsync(ct);
        Instituciones = currentUser.EsGlobal ? insts : insts.Where(i => currentUser.InstitucionesAsignadas.Contains(i.Id)).ToList();
        Data = await sender.Send(new GetExpedientesDashboardQuery(institucionId), ct);
    }
}
