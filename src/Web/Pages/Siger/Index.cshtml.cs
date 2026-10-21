using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Pages.Siger;

[Authorize]
[Permission("Siger", AccionModulo.Ver, "Ver el inventario SIGER")]
public sealed class IndexModel(IApplicationDbContext ctx) : PageModel
{
    public PagedResult<TramiteSigerRow> Resultado { get; private set; } = PagedResult<TramiteSigerRow>.Empty(25);
    public int TotalRegistros { get; private set; }
    public int TotalPublicados { get; private set; }
    public int TotalEnLinea { get; private set; }
    public int TotalInstituciones { get; private set; }

    [BindProperty(SupportsGet = true)] public string? Buscar { get; set; }
    [BindProperty(SupportsGet = true)] public string? Institucion { get; set; }
    [BindProperty(SupportsGet = true)] public string? Estado { get; set; }
    [BindProperty(SupportsGet = true)] public string? Publicado { get; set; }
    [BindProperty(SupportsGet = true)] public string? EnLinea { get; set; }

    public IReadOnlyList<string> Instituciones { get; private set; } = [];
    public IReadOnlyList<string> Estados { get; private set; } = [];

    public async Task OnGetAsync(int? pg, CancellationToken ct)
    {
        var q = ctx.TramitesSiger.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(Buscar))
        {
            var b = Buscar.Trim();
            q = q.Where(t => t.Nombre.Contains(b) || t.Codigo.Contains(b) || t.Institucion.Contains(b)
                || (t.Dependencia != null && t.Dependencia.Contains(b)));
        }
        if (!string.IsNullOrWhiteSpace(Institucion))
            q = q.Where(t => t.Sigla == Institucion || t.InstitucionId == Institucion);
        if (!string.IsNullOrWhiteSpace(Estado))
            q = q.Where(t => t.EstadoSiger == Estado);
        if (Publicado == "Si") q = q.Where(t => t.Publicado);
        else if (Publicado == "No") q = q.Where(t => !t.Publicado);
        if (EnLinea == "Si") q = q.Where(t => t.DisponibleEnLinea);
        else if (EnLinea == "No") q = q.Where(t => !t.DisponibleEnLinea);

        var all = ctx.TramitesSiger.AsNoTracking();
        TotalRegistros = await all.CountAsync(ct);
        TotalPublicados = await all.CountAsync(t => t.Publicado, ct);
        TotalEnLinea = await all.CountAsync(t => t.DisponibleEnLinea, ct);
        TotalInstituciones = await all.Select(t => t.Sigla).Distinct().CountAsync(ct);

        Instituciones = await all.Select(t => t.Sigla!).Where(s => s != null).Distinct().OrderBy(s => s).ToListAsync(ct);
        Estados = await all.Select(t => t.EstadoSiger!).Where(s => s != null).Distinct().OrderBy(s => s).ToListAsync(ct);

        var (_, page, size) = Paginacion.Normalizar(null, pg, 25);
        var total = await q.CountAsync(ct);
        var rawItems = await q.OrderBy(t => t.Institucion).ThenBy(t => t.Codigo)
            .Skip((page - 1) * size).Take(size)
            .Select(t => new
            {
                t.Id, t.IdSiger, t.Codigo, t.Nombre, t.Institucion, t.Sigla,
                t.EstadoSiger, t.Publicado, t.DisponibleEnLinea, t.EnPlanDigitalizacion,
                Pasos = t.Pasos.Count, Requisitos = t.Requisitos.Count,
                Entregables = t.Entregables.Count, t.DiagramaUrl, t.InstitucionId,
                Expedientes = ctx.Tramites.Count(et => et.TramiteSigerId == t.Id)
            })
            .ToListAsync(ct);
        var items = rawItems.Select(t => new TramiteSigerRow(
            t.Id, t.IdSiger, t.Codigo, t.Nombre, t.Institucion, t.Sigla,
            t.EstadoSiger, t.Publicado, t.DisponibleEnLinea, t.EnPlanDigitalizacion,
            t.Pasos, t.Requisitos, t.Entregables, t.DiagramaUrl,
            t.InstitucionId, t.Expedientes)).ToList();

        Resultado = new PagedResult<TramiteSigerRow>(items, total, page, size);
    }
}

public sealed record TramiteSigerRow(
    int Id, int IdSiger, string Codigo, string Nombre, string Institucion, string? Sigla,
    string? Estado, bool Publicado, bool EnLinea, bool EnPlan,
    int Pasos, int Requisitos, int Entregables, string? DiagramaUrl,
    string? InstitucionId, int Expedientes);
