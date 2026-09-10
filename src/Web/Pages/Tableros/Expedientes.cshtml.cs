namespace Diger.TramitesEstado.Web.Pages.Tableros;

[Authorize]
[Permission("Tableros", AccionModulo.Ver, "Ver tableros")]
public sealed class ExpedientesModel(ISender sender, IInstitucionRepository institucionRepo, ICurrentUserService currentUser) : PageModel
{
    public ExpedientesDashboardDto Data { get; private set; } = default!;
    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];
    public string? InstitucionId { get; private set; }
    public DateOnly? Desde { get; private set; }
    public DateOnly? Hasta { get; private set; }
    public EstadoTramite? Estado { get; private set; }

    public async Task OnGetAsync(string? institucionId, DateOnly? desde, DateOnly? hasta, string? estado, CancellationToken ct)
    {
        InstitucionId = institucionId;
        Desde = desde;
        Hasta = hasta;
        Estado = Enum.TryParse<EstadoTramite>(estado, ignoreCase: true, out var est) ? est : null;

        var insts = await institucionRepo.GetAllActivasAsync(ct);
        Instituciones = currentUser.EsGlobal ? insts : insts.Where(i => currentUser.InstitucionesAsignadas.Contains(i.Id)).ToList();
        Data = await sender.Send(new GetExpedientesDashboardQuery(institucionId, Desde, Hasta, Estado), ct);
    }
}
