using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Pages.Siger;

[Authorize]
public sealed class TableroModel(IApplicationDbContext ctx) : PageModel
{
    public TableroSigerData D { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        var all = ctx.TramitesSiger.AsNoTracking();
        var total = await all.CountAsync(ct);

        // KPIs
        D.Total = total;
        D.Instituciones = await all.Select(t => t.Sigla).Distinct().CountAsync(ct);
        D.Aprobados = await all.CountAsync(t => t.EstadoSiger == "Aprobado", ct);
        D.Registrados = await all.CountAsync(t => t.EstadoSiger == "Registrado", ct);
        D.Completos = await all.CountAsync(t => t.EstadoSiger == "Completo", ct);
        D.ConDiagrama = await all.CountAsync(t => t.DiagramaUrl != null && t.DiagramaUrl != "", ct);
        D.ConEnlace = await all.CountAsync(t => t.EnlacePrincipal != null && t.EnlacePrincipal != "", ct);
        D.ConObservaciones = await all.CountAsync(t => t.ObservacionesDiger != null && t.ObservacionesDiger != "", ct);

        // Focos de atención
        var tramiteIds = all.Select(t => t.Id);
        var conPasos = ctx.PasosSiger.Select(p => p.TramiteSigerId).Distinct();
        var conRequisitos = ctx.RequisitosSiger.Select(r => r.TramiteSigerId).Distinct();
        D.SinPasos = await all.CountAsync(t => !conPasos.Contains(t.Id), ct);
        D.SinRequisitos = await all.CountAsync(t => !conRequisitos.Contains(t.Id), ct);
        D.SinDiagrama = total - D.ConDiagrama;
        D.SinEnlace = total - D.ConEnlace;

        // Carga documental
        var reqPorTramite = await ctx.RequisitosSiger
            .GroupBy(r => r.TramiteSigerId)
            .Select(g => new { Id = g.Key, Cnt = g.Count() })
            .ToListAsync(ct);
        var reqMap = reqPorTramite.ToDictionary(x => x.Id, x => x.Cnt);

        D.AltaCarga10 = reqPorTramite.Count(x => x.Cnt >= 10);
        D.AltaCarga15 = reqPorTramite.Count(x => x.Cnt >= 15);

        var todosIds = await all.Select(t => t.Id).ToListAsync(ct);
        var sinReqCount = todosIds.Count(id => !reqMap.ContainsKey(id));
        D.CargaDocumental =
        [
            new("Sin requisitos", sinReqCount),
            new("1–4 requisitos", todosIds.Count(id => reqMap.TryGetValue(id, out var c) && c >= 1 && c <= 4)),
            new("5–9 requisitos", todosIds.Count(id => reqMap.TryGetValue(id, out var c) && c >= 5 && c <= 9)),
            new("10–14 requisitos", todosIds.Count(id => reqMap.TryGetValue(id, out var c) && c >= 10 && c <= 14)),
            new("15 o más", todosIds.Count(id => reqMap.TryGetValue(id, out var c) && c >= 15)),
        ];

        // Por institución (top 15)
        var instRaw = await all
            .GroupBy(t => new { t.Sigla, t.InstitucionId })
            .Select(g => new { Sigla = g.Key.Sigla, InstId = g.Key.InstitucionId, Cnt = g.Count() })
            .OrderByDescending(x => x.Cnt)
            .Take(15)
            .ToListAsync(ct);
        D.PorInstitucion = instRaw
            .Select(x => new InstSigerRow(x.Sigla ?? "", x.InstId, x.Cnt))
            .ToList();

        // Por estado
        var estadoRaw = await all
            .GroupBy(t => t.EstadoSiger)
            .Select(g => new { Estado = g.Key, Cnt = g.Count() })
            .OrderByDescending(x => x.Cnt)
            .ToListAsync(ct);
        D.PorEstado = estadoRaw
            .Select(x => new EstadoRow(x.Estado ?? "Sin dato", x.Cnt))
            .ToList();

        // Completitud de campos
        D.Completitud =
        [
            new("Descripción", total, total),
            new("Objetivo", total, total),
            new("Dirigido a", total, total),
            new("Dependencia", await all.CountAsync(t => t.Dependencia != null && t.Dependencia != "", ct), total),
            new("Diagrama", D.ConDiagrama, total),
            new("Enlace principal", D.ConEnlace, total),
            new("Observaciones DIGER", D.ConObservaciones, total),
        ];
        D.Completitud.Sort((a, b) => a.Pct.CompareTo(b.Pct));

        // Top fichas con mayor carga documental
        var cargaRaw = await all
            .Select(t => new {
                t.Id, t.Codigo, t.Nombre, Sigla = t.Sigla ?? "",
                Req = t.Requisitos.Count, Pas = t.Pasos.Count, Ent = t.Entregables.Count })
            .OrderByDescending(x => x.Req)
            .Take(12)
            .ToListAsync(ct);
        D.MayorCarga = cargaRaw
            .Select(x => new FichaCargaRow(x.Id, x.Codigo, x.Nombre, x.Sigla, x.Req, x.Pas, x.Ent))
            .ToList();

        // Tareas de digitalización
        D.TotalTareas = await ctx.TareasDigitalizacionSiger.CountAsync(ct);
        D.TareasCompletadas = await ctx.TareasDigitalizacionSiger
            .CountAsync(t => t.Estado == "Completada" || t.Estado == "Completado", ct);
        D.TareasEnProceso = D.TotalTareas - D.TareasCompletadas;

        // Tareas de digitalización por institución (top 10)
        var tareasRaw = await (
            from t in ctx.TareasDigitalizacionSiger
            join tr in ctx.TramitesSiger on t.TramiteSigerId equals tr.Id
            group t by new { tr.Sigla, tr.InstitucionId } into g
            select new { Sigla = g.Key.Sigla, InstId = g.Key.InstitucionId, Cnt = g.Count() })
            .OrderByDescending(x => x.Cnt)
            .Take(10)
            .ToListAsync(ct);
        D.TareasPorInstitucion = tareasRaw
            .Select(x => new InstSigerRow(x.Sigla ?? "", x.InstId, x.Cnt))
            .ToList();
    }
}

public sealed class TableroSigerData
{
    public int Total { get; set; }
    public int Instituciones { get; set; }
    public int Aprobados { get; set; }
    public int Registrados { get; set; }
    public int Completos { get; set; }
    public int ConDiagrama { get; set; }
    public int ConEnlace { get; set; }
    public int ConObservaciones { get; set; }

    public int SinPasos { get; set; }
    public int SinRequisitos { get; set; }
    public int SinDiagrama { get; set; }
    public int SinEnlace { get; set; }
    public int AltaCarga10 { get; set; }
    public int AltaCarga15 { get; set; }

    public List<RangoRow> CargaDocumental { get; set; } = [];
    public List<InstSigerRow> PorInstitucion { get; set; } = [];
    public List<EstadoRow> PorEstado { get; set; } = [];
    public List<CompletitudRow> Completitud { get; set; } = [];
    public List<FichaCargaRow> MayorCarga { get; set; } = [];
    public List<InstSigerRow> TareasPorInstitucion { get; set; } = [];

    public int TotalTareas { get; set; }
    public int TareasCompletadas { get; set; }
    public int TareasEnProceso { get; set; }
}

public sealed record RangoRow(string Etiqueta, int Cantidad);
public sealed record InstSigerRow(string Sigla, string? InstitucionId, int Cantidad);
public sealed record EstadoRow(string Estado, int Cantidad);
public sealed record CompletitudRow(string Campo, int ConDato, int Total)
{
    public int Pct => Total > 0 ? (int)Math.Round(100.0 * ConDato / Total) : 0;
}
public sealed record FichaCargaRow(int Id, string Codigo, string Nombre, string Sigla,
    int Requisitos, int Pasos, int Entregables);
