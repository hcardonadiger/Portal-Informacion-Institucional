using Microsoft.AspNetCore.WebUtilities;

namespace Diger.TramitesEstado.Web.Pages.Reuniones;

[Authorize]
public sealed class CompromisosModel(
    ISender sender, IInstitucionRepository institucionRepo, ICurrentUserService currentUser) : PageModel
{
    public CompromisosResult Resultado { get; private set; } =
        new(PagedResult<CompromisoListItemDto>.Empty(15),
            new CompromisosResumen(0, 0, 0, 0, 0, 0, 0), []);

    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];

    public string?           Q             { get; private set; }
    public EstadoCompromiso? Estado        { get; private set; }
    public int?              InstitucionId { get; private set; }
    public string?           Responsable   { get; private set; }
    public bool              SoloVencidos  { get; private set; }

    public bool PuedeGestionar =>
        User.IsInRole(nameof(RolUsuario.Administrador)) ||
        User.IsInRole(nameof(RolUsuario.Coordinador)) ||
        User.IsInRole(nameof(RolUsuario.Tecnico));

    private async Task<IReadOnlyList<Institucion>> InstitucionesEnAlcanceAsync(CancellationToken ct)
    {
        var insts = await institucionRepo.GetAllActivasAsync(ct);
        return currentUser.EsGlobal ? insts
            : insts.Where(i => currentUser.InstitucionesAsignadas.Contains(i.Id)).ToList();
    }

    public async Task OnGetAsync(
        string? q, EstadoCompromiso? estado, int? institucionId, string? responsable,
        bool soloVencidos, int? page, CancellationToken ct)
    {
        Q = q; Estado = estado; InstitucionId = institucionId; Responsable = responsable; SoloVencidos = soloVencidos;
        Instituciones = await InstitucionesEnAlcanceAsync(ct);
        Resultado = await sender.Send(
            new GetCompromisosQuery(q, estado, institucionId, responsable, soloVencidos, page), ct);
    }

    public async Task<IActionResult> OnPostActualizarAsync(
        int id, EstadoCompromiso estado, DateOnly? fechaCumplimiento, string? nota,
        string? q, EstadoCompromiso? festado, int? finstitucionId, string? fresponsable, bool fsoloVencidos, int? fpage,
        CancellationToken ct)
    {
        if (!PuedeGestionar) return Forbid();

        await sender.Send(new ActualizarSeguimientoCompromisoCommand(id, estado, fechaCumplimiento, nota), ct);
        TempData["SuccessMsg"] = "Seguimiento actualizado.";

        // 'page' es una clave reservada en los route values de Razor Pages (identifica la página),
        // por eso preservamos los filtros + la paginación vía query string explícita.
        var qs = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(q))           qs["q"] = q;
        if (festado is { } fe)                        qs["estado"] = fe.ToString();
        if (finstitucionId is { } fi)                 qs["institucionId"] = fi.ToString();
        if (!string.IsNullOrWhiteSpace(fresponsable)) qs["responsable"] = fresponsable;
        if (fsoloVencidos)                            qs["soloVencidos"] = "true";
        if (fpage is { } fp && fp > 1)                qs["page"] = fp.ToString();

        var url = QueryHelpers.AddQueryString(Url.Page("/Reuniones/Compromisos")!, qs);
        return Redirect(url);
    }
}
