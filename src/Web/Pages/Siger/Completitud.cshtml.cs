using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Pages.Siger;

/// <summary>Por institución, cuántas fichas tienen ya categoría, modalidad, tiempo, costo y
/// enlace a SOL. Es la única forma de saber si el piloto está listo para publicarse y de
/// estimar el costo del corte siguiente — el plan lo marca como obligatorio, no adorno.</summary>
[Authorize]
[Permission("Siger", AccionModulo.Ver, "Ver el inventario SIGER")]
public sealed class CompletitudModel(IApplicationDbContext ctx) : PageModel
{
    public IReadOnlyList<CompletitudInstitucionRow> Filas { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var filas = await ctx.TramitesSiger.AsNoTracking()
            .Where(t => t.InstitucionId != null)
            .GroupBy(t => t.InstitucionId!)
            .Select(g => new
            {
                InstitucionId = g.Key,
                Fichas = g.Count(),
                Publicables = g.Count(t => t.Publicado),
                ConCategoria = g.Count(t => t.CategoriaId != null),
                ConModalidad = g.Count(t => t.Modalidad != null),
                ConTiempo = g.Count(t => t.TiempoTexto != null),
                ConCosto = g.Count(t => t.CostoEsGratuito != null),
                EnSol = g.Count(t => t.EstaEnSol)
            })
            .ToListAsync(ct);

        var nombres = await ctx.Instituciones.AsNoTracking()
            .Where(i => filas.Select(f => f.InstitucionId).Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Nombre, ct);

        Filas = filas
            .Select(f => new CompletitudInstitucionRow(
                f.InstitucionId, nombres.TryGetValue(f.InstitucionId, out var n) ? n : f.InstitucionId,
                f.Fichas, f.Publicables, f.ConCategoria, f.ConModalidad, f.ConTiempo, f.ConCosto, f.EnSol))
            .OrderByDescending(f => f.Fichas)
            .ToList();
    }
}

public sealed record CompletitudInstitucionRow(
    string InstitucionId, string Institucion, int Fichas, int Publicables,
    int ConCategoria, int ConModalidad, int ConTiempo, int ConCosto, int EnSol)
{
    private static int Pct(int n, int total) => total == 0 ? 0 : (int)Math.Round(100.0 * n / total);
    public int PctCategoria => Pct(ConCategoria, Fichas);
    public int PctModalidad => Pct(ConModalidad, Fichas);
    public int PctTiempo    => Pct(ConTiempo, Fichas);
    public int PctCosto     => Pct(ConCosto, Fichas);
    public int PctPublicables => Pct(Publicables, Fichas);
}
