using System.Linq.Expressions;
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

    /// <summary>Cuántas fichas del inventario completo no se pueden publicar todavía. Va en la
    /// cabecera para que el técnico vea el tamaño del pendiente sin recorrer las páginas.</summary>
    public int TotalIncompletas { get; private set; }

    [BindProperty(SupportsGet = true)] public string? Buscar { get; set; }
    [BindProperty(SupportsGet = true)] public string? Institucion { get; set; }
    [BindProperty(SupportsGet = true)] public string? Estado { get; set; }
    [BindProperty(SupportsGet = true)] public string? Publicado { get; set; }
    [BindProperty(SupportsGet = true)] public string? EnLinea { get; set; }

    /// <summary>Filtro "solo las que les falta algo". Es el que convierte la alerta en una lista
    /// de trabajo: sin él, el técnico tiene que ir fila por fila buscando el aviso.</summary>
    [BindProperty(SupportsGet = true)] public string? Completa { get; set; }

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

        // El criterio se repite en SQL porque FichaPublicaCompletitud no se puede traducir; es la
        // misma condición, campo por campo, que CamposFaltantes evalúa en memoria.
        if (Completa == "No") q = q.Where(FichaIncompleta);
        else if (Completa == "Si") q = q.Where(FichaCompleta);

        var all = ctx.TramitesSiger.AsNoTracking();
        TotalRegistros = await all.CountAsync(ct);
        TotalPublicados = await all.CountAsync(t => t.Publicado, ct);
        TotalEnLinea = await all.CountAsync(t => t.DisponibleEnLinea, ct);
        TotalInstituciones = await all.Select(t => t.Sigla).Distinct().CountAsync(ct);
        TotalIncompletas = await all.Where(FichaIncompleta).CountAsync(ct);

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
                Expedientes = ctx.Tramites.Count(et => et.TramiteSigerId == t.Id),
                // Los seis campos de la ficha pública viajan solo para calcular qué le falta.
                t.CategoriaId, t.Modalidad, t.TiempoTexto, t.CostoEsGratuito, t.EstaEnSol, t.SolUrl
            })
            .ToListAsync(ct);
        var items = rawItems.Select(t => new TramiteSigerRow(
            t.Id, t.IdSiger, t.Codigo, t.Nombre, t.Institucion, t.Sigla,
            t.EstadoSiger, t.Publicado, t.DisponibleEnLinea, t.EnPlanDigitalizacion,
            t.Pasos, t.Requisitos, t.Entregables, t.DiagramaUrl,
            t.InstitucionId, t.Expedientes,
            FichaPublicaCompletitud.CamposFaltantes(
                t.CategoriaId, t.Modalidad, t.TiempoTexto, t.CostoEsGratuito, t.EstaEnSol, t.SolUrl))).ToList();

        Resultado = new PagedResult<TramiteSigerRow>(items, total, page, size);
    }

    /// <summary>La negación de "ficha completa", escrita como árbol de expresión para que EF la
    /// traduzca a SQL: un método normal no se puede traducir y reventaría dentro de un
    /// <c>Where</c>. Repite campo por campo lo que <see cref="FichaPublicaCompletitud"/> evalúa
    /// en memoria — el mismo criterio, en el único lenguaje que entiende la base.</summary>
    private static readonly Expression<Func<TramiteSiger, bool>> FichaIncompleta =
        t => t.CategoriaId == null || t.Modalidad == null || t.TiempoTexto == null ||
             t.CostoEsGratuito == null || (t.EstaEnSol && t.SolUrl == null);

    /// <summary>Lo contrario de <see cref="FichaIncompleta"/>, derivado de ella en vez de
    /// escrito a mano: dos condiciones gemelas mantenidas por separado terminan discrepando.</summary>
    private static readonly Expression<Func<TramiteSiger, bool>> FichaCompleta =
        Expression.Lambda<Func<TramiteSiger, bool>>(
            Expression.Not(FichaIncompleta.Body), FichaIncompleta.Parameters);
}

/// <param name="Faltantes">Qué le falta a la ficha para poder publicarse. Vacía = completa.</param>
/// <param name="IdSiger">Vacío en una ficha creada desde un expediente: no existe en SIGER.</param>
public sealed record TramiteSigerRow(
    int Id, int? IdSiger, string Codigo, string Nombre, string Institucion, string? Sigla,
    string? Estado, bool Publicado, bool EnLinea, bool EnPlan,
    int Pasos, int Requisitos, int Entregables, string? DiagramaUrl,
    string? InstitucionId, int Expedientes,
    IReadOnlyList<string> Faltantes)
{
    public bool FichaCompleta => Faltantes.Count == 0;

    /// <summary>La frase de la alerta. La redacta <see cref="FichaPublicaCompletitud.Frase"/>
    /// para que el inventario, el detalle y el editor digan exactamente lo mismo.</summary>
    public string TextoFaltantes => FichaPublicaCompletitud.Frase(Faltantes);
}
