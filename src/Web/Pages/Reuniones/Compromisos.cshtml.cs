namespace Diger.TramitesEstado.Web.Pages.Reuniones;

[Authorize]
public sealed class CompromisosModel(
    ISender sender, IInstitucionRepository institucionRepo, ICurrentUserService currentUser) : PageModel
{
    public CompromisosResult Resultado { get; private set; } =
        new(PagedResult<CompromisoListItemDto>.Empty(Paginacion.TamanoDefecto),
            new CompromisosResumen(0, 0, 0, 0, 0, 0, 0), []);

    public IReadOnlyList<CompromisoListItemDto> Todos { get; private set; } = [];
    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];

    public string?           Q             { get; private set; }
    public EstadoCompromiso? Estado        { get; private set; }
    public string?           InstitucionId { get; private set; }
    public string?           Responsable   { get; private set; }
    public bool              SoloVencidos  { get; private set; }

    private async Task<IReadOnlyList<Institucion>> InstitucionesEnAlcanceAsync(CancellationToken ct)
    {
        var insts = await institucionRepo.GetAllActivasAsync(ct);
        return currentUser.EsGlobal ? insts
            : insts.Where(i => currentUser.InstitucionesAsignadas.Contains(i.Id)).ToList();
    }

    public async Task OnGetAsync(
        string? q, EstadoCompromiso? estado, string? institucionId, string? responsable,
        bool soloVencidos, int? pg, CancellationToken ct)
    {
        Q = q; Estado = estado; InstitucionId = institucionId; Responsable = responsable; SoloVencidos = soloVencidos;
        Instituciones = await InstitucionesEnAlcanceAsync(ct);
        Resultado = await sender.Send(
            new GetCompromisosQuery(q, estado, institucionId, responsable, soloVencidos, pg), ct);
        Todos = (await sender.Send(
            new GetCompromisosQuery(q, estado, institucionId, responsable, soloVencidos, Page: 1, Size: 100), ct)).Pagina.Items;
    }
}
