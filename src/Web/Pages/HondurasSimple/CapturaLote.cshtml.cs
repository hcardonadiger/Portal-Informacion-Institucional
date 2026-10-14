using System.Linq.Expressions;
using Diger.TramitesEstado.Application.Siger.Bloqueo;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Pages.HondurasSimple;

/// <summary>Asignar categoría/modalidad/tiempo/costo a una selección de trámites filtrada por
/// institución, en una sola pantalla. El plan lo marca como obligatorio para la Fase 3: con un
/// formulario de uno en uno, cientos de fichas por piloto son meses de trabajo humano insufrible.</summary>
[Authorize]
[Permission("HondurasSimple", AccionModulo.Editar, "Captura por lotes de fichas SIGER")]
public sealed class CapturaLoteModel(IApplicationDbContext ctx) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? InstitucionId { get; set; }
    [BindProperty(SupportsGet = true)] public bool SoloIncompletas { get; set; }
    [BindProperty(SupportsGet = true)] public string? Buscar { get; set; }
    [BindProperty(SupportsGet = true)] public int? Pg { get; set; }

    /// <summary>Lo mismo que el inventario. Antes la lista salía entera: con «Todas las
    /// instituciones» eran cientos de filas seguidas debajo del panel (14-10-2026).</summary>
    private const int TamanoPagina = 25;

    [BindProperty] public List<int> Seleccionados { get; set; } = [];
    [BindProperty] public int? CategoriaId { get; set; }
    [BindProperty] public string? Modalidad { get; set; }
    [BindProperty] public string? TiempoTexto { get; set; }
    [BindProperty] public string? CostoTexto { get; set; }
    [BindProperty] public string? CostoEsGratuito { get; set; }

    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];
    public IReadOnlyList<CategoriaTramite> Categorias { get; private set; } = [];
    public PagedResult<FilaLoteDto> Resultado { get; private set; } = PagedResult<FilaLoteDto>.Empty(TamanoPagina);
    public IReadOnlyList<FilaLoteDto> Filas => Resultado.Items;

    public sealed record FilaLoteDto(
        int Id, string Codigo, string Nombre, string? CategoriaNombre, string? Modalidad,
        string? TiempoTexto, string? CostoTexto, bool? CostoEsGratuito, bool EstaEnSol, bool FichaCompleta);

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (Seleccionados.Count > 0)
        {
            var entidades = await ctx.TramitesSiger.Where(t => Seleccionados.Contains(t.Id)).ToListAsync(ct);
            foreach (var t in entidades)
            {
                // Solo se toca lo que el operador explícitamente llenó — dejar un campo vacío
                // en el panel de lote NO borra lo que ya tenía cada trámite.
                if (CategoriaId is not null) t.CategoriaId = CategoriaId;
                if (!string.IsNullOrWhiteSpace(Modalidad)) t.Modalidad = Modalidad;
                if (!string.IsNullOrWhiteSpace(TiempoTexto)) t.TiempoTexto = TiempoTexto;
                if (!string.IsNullOrWhiteSpace(CostoTexto)) t.CostoTexto = CostoTexto;
                t.CostoEsGratuito = CostoEsGratuito switch
                {
                    "true" => true,
                    "false" => false,
                    _ => t.CostoEsGratuito
                };

                // La captura por lotes no toca la publicación: llenar campos y decidir qué ve el
                // ciudadano son cosas distintas, y esta pantalla existe para lo primero. Antes
                // recalculaba la bandera acá, que era donde más dolía —la pantalla de llenar por
                // tandas despublicando justo lo que el técnico acababa de mejorar.
            }
            await ctx.SaveChangesAsync(ct);
            TempData["SuccessMsg"] = $"{entidades.Count} trámite(s) actualizados.";
        }

        await CargarAsync(ct);
        return Page();
    }

    private async Task CargarAsync(CancellationToken ct)
    {
        Categorias = await ctx.CategoriasTramite.AsNoTracking().Where(c => c.Activo).OrderBy(c => c.Orden).ToListAsync(ct);
        var categoriaNombres = Categorias.ToDictionary(c => c.Id, c => c.Nombre);

        // D-23: la captura en lote se queda como está, pero no toca fichas bloqueadas. Sus campos
        // de contenido se editan en el expediente que las tiene, y escribirlos acá sería trabajo
        // que el siguiente pase desde ese expediente borraría sin dejar rastro.
        var q = ctx.TramitesSiger.AsNoTracking().SinBloqueadas(ctx.Tramites.AsNoTracking()).AsQueryable();

        // El selector sale de las fichas, no del catálogo. El catálogo de instituciones pasa por el
        // filtro de alcance y, fuera del rol Administrador, solo deja ver la institución propia: un
        // técnico de DIGER encontraba «DIGER» y nada más, aunque la lista de abajo —que no pasa por
        // ese filtro— le enseñara las fichas de INPREMA. Esconder los nombres no protegía nada; solo
        // rompía el filtro. Por eso se ignora aquí, y solo aquí. Y se ofrecen únicamente las que
        // tienen fichas capturables: elegir una sin ellas dejaría la lista en blanco.
        var conFichas = q.Where(t => t.InstitucionId != null).Select(t => t.InstitucionId!).Distinct();
        Instituciones = await ctx.Instituciones.AsNoTracking().IgnoreQueryFilters()
            .Where(i => i.Activo && conFichas.Contains(i.Id))
            .OrderBy(i => i.Nombre).ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(InstitucionId))
            q = q.Where(t => t.InstitucionId == InstitucionId);

        if (!string.IsNullOrWhiteSpace(Buscar))
        {
            var b = Buscar.Trim();
            q = q.Where(t => t.Nombre.Contains(b) || t.Codigo.Contains(b));
        }

        // En SQL y no en memoria: con paginación, filtrar después de traer la página dejaría
        // páginas a medias.
        if (SoloIncompletas) q = q.Where(FichaIncompleta);

        var total = await q.CountAsync(ct);
        var (_, page, size) = Paginacion.Normalizar(null, Pg, TamanoPagina);
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        if (page > totalPaginas) page = totalPaginas;

        var filas = await q.OrderBy(t => t.Nombre).ThenBy(t => t.Id)
            .Skip((page - 1) * size).Take(size)
            .Select(t => new { t.Id, t.Codigo, t.Nombre, t.CategoriaId, t.Modalidad, t.TiempoTexto, t.CostoTexto, t.CostoEsGratuito, t.EstaEnSol, t.SolUrl, t.SolTramo })
            .ToListAsync(ct);

        var items = filas.Select(f => new FilaLoteDto(
            f.Id, f.Codigo, f.Nombre,
            f.CategoriaId is { } cid && categoriaNombres.TryGetValue(cid, out var n) ? n : null,
            f.Modalidad, f.TiempoTexto, f.CostoTexto, f.CostoEsGratuito, f.EstaEnSol,
            FichaPublicaCompletitud.Evaluar(f.CategoriaId, f.Modalidad, f.TiempoTexto, f.CostoEsGratuito, f.EstaEnSol, f.SolUrl, f.SolTramo)))
            .ToList();

        Resultado = new PagedResult<FilaLoteDto>(items, total, page, size);
    }

    /// <summary>
    /// <see cref="FichaPublicaCompletitud.CamposFaltantes"/> escrita para SQL, campo por campo: si
    /// se agrega un campo obligatorio allá, va también aquí, o el filtro y la columna «Completa»
    /// dirán cosas distintas.
    /// </summary>
    private static readonly Expression<Func<TramiteSiger, bool>> FichaIncompleta =
        t => t.CategoriaId == null || t.Modalidad == null || t.TiempoTexto == null ||
             t.CostoEsGratuito == null || (t.EstaEnSol && t.SolUrl == null && t.SolTramo == null);
}
