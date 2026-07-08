namespace Diger.TramitesEstado.Web.Pages.Tableros;

[Authorize]
public sealed class ReunionesModel(ISender sender, IInstitucionRepository institucionRepo, ICurrentUserService currentUser) : PageModel
{
    public ReunionesDashboardDto Data { get; private set; } = default!;
    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];
    public string? InstitucionId { get; private set; }

    public async Task OnGetAsync(string? institucionId, CancellationToken ct)
    {
        InstitucionId = institucionId;
        var insts = await institucionRepo.GetAllActivasAsync(ct);
        Instituciones = currentUser.EsGlobal ? insts : insts.Where(i => currentUser.InstitucionesAsignadas.Contains(i.Id)).ToList();
        Data = await sender.Send(new GetReunionesDashboardQuery(institucionId), ct);
    }
}
