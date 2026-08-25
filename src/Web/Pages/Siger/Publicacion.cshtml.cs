using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Pages.Siger;

/// <summary>
/// Qué trámites ve el ciudadano en HondurasÁgil, y la pantalla para decidirlo.
/// </summary>
/// <remarks>
/// <para>
/// Publicar dejó de ser una consecuencia del estado administrativo de la ficha y pasó a ser un
/// acto deliberado (D-08, D-10). Esta pantalla es donde ocurre ese acto y, sobre todo, donde se
/// puede responder de un vistazo «¿qué está viendo el ciudadano ahora mismo?».
/// </para>
/// <para>
/// <b>Los avisos no bloquean.</b> Una ficha con estado Registrado o con campos sin llenar se
/// puede publicar igual: el aviso informa, no impone. Quien administra conoce el trámite mejor
/// que una regla, y ya se decidió (P-09) que al ciudadano le sirve más ver el trámite con un
/// guion donde falta el dato que no encontrarlo.
/// </para>
/// <para>
/// <b>Permiso propio.</b> Publicar al ciudadano no es lo mismo que editar una ficha, así que
/// tiene su propia clave: alguien puede corregir contenido todo el día sin poder decidir qué
/// sale al público.
/// </para>
/// </remarks>
[Authorize]
[Permission("Siger.Publicacion", AccionModulo.Ver, "Ver qué trámites están publicados en HondurasÁgil")]
public sealed class PublicacionModel(IApplicationDbContext ctx) : PageModel
{
    public const string TabPublicadas = "publicadas";
    public const string TabCandidatas = "candidatas";
    public const string TabTodas      = "todas";

    private const int TamanoPagina = 25;

    [BindProperty(SupportsGet = true)] public string  Tab    { get; set; } = TabPublicadas;
    [BindProperty(SupportsGet = true)] public string? Buscar { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sigla  { get; set; }
    [BindProperty(SupportsGet = true)] public int?    Pg     { get; set; }

    /// <summary>Fichas marcadas en el formulario. Solo se actúa sobre estas.</summary>
    [BindProperty] public List<int> Seleccion { get; set; } = [];

    public PagedResult<FilaVm>   Resultado { get; private set; } = PagedResult<FilaVm>.Empty(TamanoPagina);
    public IReadOnlyList<string> Siglas    { get; private set; } = [];

    public int TotalFichas { get; private set; }
    public int Publicadas  { get; private set; }
    public int Candidatas  { get; private set; }

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    // ── Acciones ──────────────────────────────────────────────────────────────

    [Permission("Siger.Publicacion", AccionModulo.Editar, "Publicar trámites en HondurasÁgil")]
    public Task<IActionResult> OnPostPublicarAsync(CancellationToken ct)
        => CambiarPublicacionAsync(true, ct);

    /// <summary>Quitar de HA es despublicar, no borrar (D-16): la ficha se queda entera y se
    /// puede volver a publicar cuando se corrija lo que hiciera falta.</summary>
    [Permission("Siger.Publicacion", AccionModulo.Editar, "Quitar trámites de HondurasÁgil")]
    public Task<IActionResult> OnPostQuitarAsync(CancellationToken ct)
        => CambiarPublicacionAsync(false, ct);

    private async Task<IActionResult> CambiarPublicacionAsync(bool publicar, CancellationToken ct)
    {
        var ids = Seleccion.Distinct().ToList();
        if (ids.Count == 0)
        {
            TempData["SuccessMsg"] = "No se marcó ninguna ficha.";
            return Redirigir();
        }

        var fichas = await ctx.TramitesSiger.Where(t => ids.Contains(t.Id)).ToListAsync(ct);

        // Se cuenta lo que realmente cambia, no lo que venía marcado: marcar algo que ya estaba
        // como se pide no es un cambio, y decir "3 publicadas" cuando dos ya lo estaban miente.
        var cambiadas = 0;
        foreach (var ficha in fichas.Where(f => f.Publicado != publicar))
        {
            ficha.Publicado = publicar;
            cambiadas++;
        }

        if (cambiadas > 0) await ctx.SaveChangesAsync(ct);

        TempData["SuccessMsg"] = cambiadas == 0
            ? "Ninguna ficha cambió de estado."
            : publicar
                ? $"{cambiadas} ficha(s) publicada(s) en HondurasÁgil."
                : $"{cambiadas} ficha(s) retirada(s) de HondurasÁgil. Siguen completas en el inventario.";

        return Redirigir();
    }

    private IActionResult Redirigir() => RedirectToPage(new { Tab, Buscar, Sigla, Pg });

    // ── Carga ─────────────────────────────────────────────────────────────────

    private async Task CargarAsync(CancellationToken ct)
    {
        var todas = ctx.TramitesSiger.AsNoTracking();

        TotalFichas = await todas.CountAsync(ct);
        Publicadas  = await todas.CountAsync(t => t.Publicado, ct);
        Candidatas  = await todas.CountAsync(EsCandidata, ct);

        Siglas = await todas.Where(t => t.Sigla != null)
            .Select(t => t.Sigla!).Distinct().OrderBy(s => s).ToListAsync(ct);

        var q = Tab switch
        {
            TabCandidatas => todas.Where(EsCandidata),
            TabTodas      => todas,
            _             => todas.Where(t => t.Publicado)
        };

        if (!string.IsNullOrWhiteSpace(Buscar))
        {
            var b = Buscar.Trim();
            q = q.Where(t => t.Nombre.Contains(b) || t.Codigo.Contains(b) || t.Institucion.Contains(b));
        }

        if (!string.IsNullOrWhiteSpace(Sigla))
            q = q.Where(t => t.Sigla == Sigla);

        var total = await q.CountAsync(ct);

        var (_, page, size) = Paginacion.Normalizar(null, Pg, TamanoPagina);
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        if (page > totalPaginas) page = totalPaginas;

        // La completitud se evalúa en memoria y solo sobre la página visible: es una regla de C#
        // que no se puede traducir a SQL, y replicarla acá crearía la segunda copia que ya se
        // eliminó una vez. Filtrar por ella tendría otro costo; enseñarla no.
        var filas = await q
            .OrderByDescending(t => t.Publicado).ThenBy(t => t.Institucion).ThenBy(t => t.Nombre)
            .Skip((page - 1) * size).Take(size)
            .Select(t => new
            {
                t.Id, t.Codigo, t.Nombre, t.Institucion, t.Sigla, t.EstadoSiger, t.Publicado,
                t.CategoriaId, t.Modalidad, t.TiempoTexto, t.CostoEsGratuito, t.EstaEnSol, t.SolUrl, t.SolTramo
            })
            .ToListAsync(ct);

        var vm = filas.Select(t => new FilaVm(
            t.Id, t.Codigo, t.Nombre, t.Institucion, t.Sigla, t.EstadoSiger, t.Publicado,
            ReglaPublicacion.EstadoListoParaPublicar(t.EstadoSiger),
            FichaPublicaCompletitud.CamposFaltantes(
                t.CategoriaId, t.Modalidad, t.TiempoTexto, t.CostoEsGratuito, t.EstaEnSol, t.SolUrl, t.SolTramo)))
            .ToList();

        Resultado = new PagedResult<FilaVm>(vm, total, page, size);
    }

    /// <summary>
    /// Candidata = todavía no está en HondurasÁgil y su estado no da motivo para dudar.
    /// </summary>
    /// <remarks>
    /// Es un árbol de expresión y no un método porque esto viaja a SQL: EF no puede traducir una
    /// llamada a C# dentro de un Where, y el intento se cae con un 500 en tiempo de ejecución que
    /// ninguna compilación advierte. Mismo patrón que <c>FichaCompleta</c> en el inventario.
    /// </remarks>
    private static readonly Expression<Func<TramiteSiger, bool>> EsCandidata =
        t => !t.Publicado
          && (t.EstadoSiger == ReglaPublicacion.Aprobado || t.EstadoSiger == ReglaPublicacion.Completo);
    /// <param name="Faltantes">Campos sin llenar. No impide publicar; se enseña como aviso.</param>
    public sealed record FilaVm(
        int      Id,
        string   Codigo,
        string   Nombre,
        string   Institucion,
        string?  Sigla,
        string?  EstadoSiger,
        bool     Publicado,
        bool     EstadoListo,
        IReadOnlyList<string> Faltantes)
    {
        public bool TieneAviso => !EstadoListo || Faltantes.Count > 0;

        /// <summary>Un solo texto para la columna de avisos. Se arma acá y no en la vista para
        /// que la pantalla no tenga que decidir nada.</summary>
        public string Aviso
        {
            get
            {
                var partes = new List<string>(2);
                if (!EstadoListo)
                    partes.Add($"estado {EstadoSiger ?? "sin capturar"}");
                if (Faltantes.Count > 0)
                    partes.Add($"falta {string.Join(", ", Faltantes)}");
                return string.Join(" · ", partes);
            }
        }
    }
}
