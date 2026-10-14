using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Pages.HondurasSimple;

/// <summary>Cuánto le falta a cada ficha para poder publicarse, y cuánto le falta a cada
/// institución. Es la única forma de saber si el piloto está listo para publicarse y de
/// estimar el costo del corte siguiente — el plan lo marca como obligatorio, no adorno.</summary>
/// <remarks>
/// <para>
/// <b>Dos vistas de los mismos datos.</b> «Por ficha» es la lista de trabajo: qué trámite,
/// qué le falta, en orden de lo más atrasado. «Por institución» es la de siempre —cuántas fichas
/// tiene cada una con categoría, modalidad, tiempo, costo y enlace a SOL— para decidir qué
/// piloto está listo. Las dos agrupan exactamente las mismas fichas.
/// </para>
/// <para>
/// <b>La completitud se calcula en memoria, y por eso la página también.</b> La regla es
/// <see cref="FichaPublicaCompletitud.CamposFaltantes"/>, que es C# y no se traduce a SQL; y el
/// tablero de arriba necesita todas las fichas, no solo las veinte de la página. Así que se trae
/// el conjunto filtrado una sola vez, con nada más que las columnas que la regla mira, y el
/// filtro «Mostrar», el orden y el Skip/Take se aplican sobre esa lista. Son unos pocos miles de
/// filas angostas; el día que deje de serlo, el camino es la columna calculada
/// <c>FichaCompleta</c> de la base, no paginar a ciegas.
/// </para>
/// <para>
/// <b>El tablero cuenta el universo, no la página.</b> Respeta la búsqueda y la institución,
/// porque esas dos dicen <i>de qué fichas</i> se habla; ignora «Mostrar», el orden y la página,
/// porque esos solo dicen cuáles se listan. Con «Mostrar: completas» el tablero sigue diciendo
/// cuántas faltan — si las contara solo a ellas diría siempre 100 %, que no le sirve a nadie.
/// </para>
/// </remarks>
[Authorize]
[Permission("HondurasSimple", AccionModulo.Ver, "Ver la completitud de las fichas")]
public sealed class CompletitudModel(IApplicationDbContext ctx) : PageModel
{
    public const int TamanoPagina = 20;

    /// <summary>Por debajo de esto una ficha cuenta como rezagada en el tablero.</summary>
    public const int Umbral = 50;

    public const string VistaFichas = "fichas";
    public const string VistaInstituciones = "instituciones";

    [BindProperty(SupportsGet = true)] public string? Vista { get; set; }
    [BindProperty(SupportsGet = true)] public string? Buscar { get; set; }
    [BindProperty(SupportsGet = true)] public string? InstitucionId { get; set; }

    /// <summary>«incompletas» o uno de los tramos de <see cref="Tramos"/>. Vacío = todas.</summary>
    [BindProperty(SupportsGet = true)] public string? Mostrar { get; set; }

    /// <summary>Clave de <see cref="OrdenesFichas"/> o <see cref="OrdenesInstituciones"/>, según
    /// la vista. Una clave que no es de la vista vale lo mismo que no mandar ninguna.</summary>
    [BindProperty(SupportsGet = true)] public string? Orden { get; set; }

    [BindProperty(SupportsGet = true)] public int? Pg { get; set; }

    public bool PorInstitucion => Vista == VistaInstituciones;

    public PagedResult<FichaCompletitudRow> Fichas { get; private set; } = PagedResult<FichaCompletitudRow>.Empty(TamanoPagina);
    public PagedResult<InstitucionCompletitudRow> Instituciones { get; private set; } = PagedResult<InstitucionCompletitudRow>.Empty(TamanoPagina);
    public ResumenCompletitud Resumen { get; private set; } = ResumenCompletitud.Vacio;

    /// <summary>Las instituciones que tienen fichas, para el selector: elegir una sin fichas
    /// dejaría la página en blanco.</summary>
    public IReadOnlyList<(string Id, string Nombre)> OpcionesInstitucion { get; private set; } = [];

    /// <summary>¿Hay alguna ficha con institución, antes de cualquier filtro? Distingue «no hay
    /// nada todavía» de «su búsqueda no encontró nada».</summary>
    public bool HayFichas { get; private set; }

    // ── Catálogos de la vista ─────────────────────────────────────────────────

    /// <summary>Los tramos del tablero y del filtro «Mostrar». Uno solo para los dos, para que
    /// pulsar una barra filtre exactamente lo que la barra cuenta.</summary>
    public static readonly IReadOnlyList<TramoCompletitud> Tramos =
    [
        new("completas", "Completas",   "100 %",         100, 100, "c-green"),
        new("alta",      "Casi listas", "75 a 99 %",      75,  99, "comp-b-alta"),
        new("media",     "A medias",    "50 a 74 %",      50,  74, "c-amber"),
        new("baja",      "Rezagadas",   $"menos de {Umbral} %", 0, Umbral - 1, "c-red")
    ];

    public static readonly IReadOnlyList<(string Clave, string Etiqueta)> OrdenesFichas =
    [
        ("pct",         "Menor completitud primero"),
        ("pct-desc",    "Mayor completitud primero"),
        ("nombre",      "Nombre del trámite"),
        ("codigo",      "Código"),
        ("institucion", "Institución")
    ];

    public static readonly IReadOnlyList<(string Clave, string Etiqueta)> OrdenesInstituciones =
    [
        ("pct",         "Menor completitud primero"),
        ("pct-desc",    "Mayor completitud primero"),
        ("institucion", "Institución"),
        ("fichas",      "Más fichas primero")
    ];

    /// <summary>Los campos que exige la regla, con el nombre que les da
    /// <see cref="FichaPublicaCompletitud.CamposFaltantes"/>: el conteo de «lo que más falta»
    /// sale de esos mismos textos, así que un campo nuevo en la regla aparece aquí solo.</summary>
    public static readonly IReadOnlyList<string> Campos = ["categoría", "modalidad", "tiempo", "costo", "enlace a SOL"];

    private static readonly StringComparer Alfabetico =
        StringComparer.Create(CultureInfo.GetCultureInfo("es"), ignoreCase: true);

    // ── Carga ─────────────────────────────────────────────────────────────────

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (Vista != VistaInstituciones) Vista = VistaFichas;
        var ordenes = PorInstitucion ? OrdenesInstituciones : OrdenesFichas;
        if (!ordenes.Any(o => o.Clave == Orden)) Orden = ordenes[0].Clave;
        if (Mostrar != "incompletas" && Tramos.All(t => t.Clave != Mostrar)) Mostrar = null;
        Buscar = string.IsNullOrWhiteSpace(Buscar) ? null : Buscar.Trim();
        InstitucionId = string.IsNullOrWhiteSpace(InstitucionId) ? null : InstitucionId;

        // Solo cuentan las fichas con institución vinculada, como siempre: las demás no tienen
        // a quién atribuirse y la API no las reparte a nadie.
        var conInstitucion = ctx.TramitesSiger.AsNoTracking().Where(t => t.InstitucionId != null);
        HayFichas = await conInstitucion.AnyAsync(ct);

        // Los nombres pasan por encima del filtro de alcance, igual que en Captura por lotes: las
        // fichas no pasan por ese filtro, y esconder el nombre de una institución cuyas fichas sí
        // se ven no protege nada — solo deja al técnico leyendo siglas.
        var catalogo = ctx.Instituciones.AsNoTracking().IgnoreQueryFilters();
        var idsConFichas = conInstitucion.Select(t => t.InstitucionId!).Distinct();
        var nombres = await catalogo.Where(i => idsConFichas.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Nombre, ct);

        var q = conInstitucion;
        if (InstitucionId is not null)
            q = q.Where(t => t.InstitucionId == InstitucionId);
        if (Buscar is not null)
        {
            var b = Buscar;
            // La institución se busca por su nombre de catálogo además del texto de la ficha:
            // el texto viene de SIGER y no siempre coincide con el nombre que se ve en pantalla.
            var porNombre = catalogo.Where(i => i.Nombre.Contains(b)).Select(i => i.Id);
            q = q.Where(t => t.Codigo.Contains(b) || t.Nombre.Contains(b) || t.Institucion.Contains(b)
                || (t.Sigla != null && t.Sigla.Contains(b)) || t.InstitucionId!.Contains(b)
                || porNombre.Contains(t.InstitucionId!));
        }

        var crudas = await q
            .Select(t => new
            {
                t.Id, t.Codigo, t.Nombre, t.InstitucionId, t.Sigla, t.Publicado,
                // Los campos de la ficha pública viajan solo para calcular qué le falta.
                t.CategoriaId, t.Modalidad, t.TiempoTexto, t.CostoEsGratuito, t.EstaEnSol, t.SolUrl, t.SolTramo
            })
            .ToListAsync(ct);

        var universo = crudas.Select(f => new FichaCompletitudRow(
                f.Id, f.Codigo, f.Nombre, f.InstitucionId!,
                nombres.TryGetValue(f.InstitucionId!, out var n) ? n : f.InstitucionId!,
                f.Sigla, f.Publicado, f.EstaEnSol,
                FichaPublicaCompletitud.CamposFaltantes(
                    f.CategoriaId, f.Modalidad, f.TiempoTexto, f.CostoEsGratuito, f.EstaEnSol, f.SolUrl, f.SolTramo)))
            .ToList();

        Resumen = Resumir(universo);

        OpcionesInstitucion = nombres
            .Select(kv => (kv.Key, kv.Value))
            .OrderBy(o => o.Value, Alfabetico)
            .ToList();

        // «Mostrar» acota la lista, no el tablero (ver remarks de la clase).
        IEnumerable<FichaCompletitudRow> mostradas = Mostrar switch
        {
            null          => universo,
            "incompletas" => universo.Where(f => !f.Completa),
            _             => universo.Where(f => f.Tramo.Clave == Mostrar)
        };

        if (PorInstitucion)
            Instituciones = Paginar(OrdenarInstituciones(AgruparPorInstitucion(mostradas)).ToList());
        else
            Fichas = Paginar(OrdenarFichas(mostradas).ToList());
    }

    private PagedResult<T> Paginar<T>(IReadOnlyList<T> todas)
    {
        var (_, page, _) = Paginacion.Normalizar(null, Pg, TamanoPagina);
        // Una página que ya no existe —el enlace se compartió antes de que cambiaran los datos,
        // o se cambió un filtro a mano en la barra— lleva a la última, no a una tabla vacía.
        var paginas = Math.Max(1, (int)Math.Ceiling(todas.Count / (double)TamanoPagina));
        page = Math.Min(page, paginas);
        var items = todas.Skip((page - 1) * TamanoPagina).Take(TamanoPagina).ToList();
        return new PagedResult<T>(items, todas.Count, page, TamanoPagina);
    }

    // El desempate es siempre institución y código: sin él, dos fichas con el mismo porcentaje
    // cambiarían de página entre una carga y otra y el paginador saltaría filas.
    private IEnumerable<FichaCompletitudRow> OrdenarFichas(IEnumerable<FichaCompletitudRow> f) => Orden switch
    {
        "pct-desc"    => f.OrderByDescending(x => x.Pct).ThenBy(x => x.Institucion, Alfabetico).ThenBy(x => x.Codigo, Alfabetico),
        "nombre"      => f.OrderBy(x => x.Nombre, Alfabetico).ThenBy(x => x.Codigo, Alfabetico),
        "codigo"      => f.OrderBy(x => x.Codigo, Alfabetico),
        "institucion" => f.OrderBy(x => x.Institucion, Alfabetico).ThenBy(x => x.Codigo, Alfabetico),
        _             => f.OrderBy(x => x.Pct).ThenBy(x => x.Institucion, Alfabetico).ThenBy(x => x.Codigo, Alfabetico)
    };

    private IEnumerable<InstitucionCompletitudRow> OrdenarInstituciones(IEnumerable<InstitucionCompletitudRow> i) => Orden switch
    {
        "pct-desc"    => i.OrderByDescending(x => x.Promedio).ThenBy(x => x.Nombre, Alfabetico),
        "institucion" => i.OrderBy(x => x.Nombre, Alfabetico),
        "fichas"      => i.OrderByDescending(x => x.Fichas).ThenBy(x => x.Nombre, Alfabetico),
        _             => i.OrderBy(x => x.Promedio).ThenByDescending(x => x.Fichas).ThenBy(x => x.Nombre, Alfabetico)
    };

    private static IEnumerable<InstitucionCompletitudRow> AgruparPorInstitucion(IEnumerable<FichaCompletitudRow> fichas) =>
        fichas.GroupBy(f => f.InstitucionId).Select(g =>
        {
            int Con(string campo) => g.Count(f => !f.Faltantes.Contains(campo));
            return new InstitucionCompletitudRow(
                g.Key, g.First().Institucion, g.Count(),
                Completas: g.Count(f => f.Completa),
                Promedio: Redondeo.Entero(g.Average(f => f.Pct)),
                Publicadas: g.Count(f => f.Publicado),
                ConCategoria: Con("categoría"), ConModalidad: Con("modalidad"),
                ConTiempo: Con("tiempo"), ConCosto: Con("costo"),
                EnSol: g.Count(f => f.EstaEnSol),
                // El enlace solo se le exige a la ficha que está en SOL; sobre las demás no
                // falta ni sobra, así que no entran en este porcentaje.
                ConEnlaceSol: g.Count(f => f.EstaEnSol && !f.Faltantes.Contains("enlace a SOL")));
        });

    private static ResumenCompletitud Resumir(IReadOnlyList<FichaCompletitudRow> fichas)
    {
        if (fichas.Count == 0) return ResumenCompletitud.Vacio;

        var porTramo = Tramos.Select(t => (t, fichas.Count(f => f.Tramo.Clave == t.Clave))).ToList();

        var faltan = Campos
            .Select(c => (Campo: c, Cantidad: fichas.Count(f => f.Faltantes.Contains(c))))
            .OrderByDescending(x => x.Cantidad)
            .ToList();

        // Solo las que tienen algo pendiente: una institución al 100 % no es de las peores
        // aunque sea la última que queda en la lista.
        var peores = AgruparPorInstitucion(fichas)
            .Where(i => i.Promedio < 100)
            .OrderBy(i => i.Promedio).ThenByDescending(i => i.Fichas)
            .Take(5)
            .ToList();

        return new ResumenCompletitud(
            Total: fichas.Count,
            Instituciones: fichas.Select(f => f.InstitucionId).Distinct().Count(),
            Promedio: Redondeo.Entero(fichas.Average(f => f.Pct)),
            Completas: fichas.Count(f => f.Completa),
            Rezagadas: fichas.Count(f => f.Pct < Umbral),
            Publicadas: fichas.Count(f => f.Publicado),
            PorTramo: porTramo,
            QueFalta: faltan,
            Peores: peores);
    }
}

/// <param name="Clave">Lo que viaja en <c>?Mostrar=</c>.</param>
/// <param name="ClaseBarra">Variante de <c>.bar-fill</c> con el color del tramo.</param>
public sealed record TramoCompletitud(string Clave, string Etiqueta, string Rango, int Desde, int Hasta, string ClaseBarra);

/// <param name="Faltantes">Lo que dice <see cref="FichaPublicaCompletitud.CamposFaltantes"/>.
/// Vacía = completa.</param>
public sealed record FichaCompletitudRow(
    int Id, string Codigo, string Nombre, string InstitucionId, string Institucion, string? Sigla,
    bool Publicado, bool EstaEnSol, IReadOnlyList<string> Faltantes)
{
    /// <summary>Cuatro campos para todas; el enlace solo se exige a la que está en SOL.</summary>
    public int Requeridos => EstaEnSol ? 5 : 4;
    public int Capturados => Requeridos - Faltantes.Count;
    public int Pct => Redondeo.Pct(Capturados, Requeridos);
    public bool Completa => Faltantes.Count == 0;
    public TramoCompletitud Tramo => CompletitudModel.Tramos.First(t => Pct >= t.Desde && Pct <= t.Hasta);
}

public sealed record InstitucionCompletitudRow(
    string Id, string Nombre, int Fichas, int Completas, int Promedio, int Publicadas,
    int ConCategoria, int ConModalidad, int ConTiempo, int ConCosto, int EnSol, int ConEnlaceSol)
{
    private static int Pct(int n, int total) => Redondeo.Pct(n, total);
    public int PctCompletas  => Pct(Completas, Fichas);
    public int PctCategoria  => Pct(ConCategoria, Fichas);
    public int PctModalidad  => Pct(ConModalidad, Fichas);
    public int PctTiempo     => Pct(ConTiempo, Fichas);
    public int PctCosto      => Pct(ConCosto, Fichas);
    public int PctEnlaceSol  => Pct(ConEnlaceSol, EnSol);
    public int PctPublicadas => Pct(Publicadas, Fichas);
}

/// <summary>El tablero de arriba. Se calcula sobre todas las fichas de la búsqueda, no sobre
/// la página (ver remarks de <see cref="CompletitudModel"/>).</summary>
public sealed record ResumenCompletitud(
    int Total, int Instituciones, int Promedio, int Completas, int Rezagadas, int Publicadas,
    IReadOnlyList<(TramoCompletitud Tramo, int Cantidad)> PorTramo,
    IReadOnlyList<(string Campo, int Cantidad)> QueFalta,
    IReadOnlyList<InstitucionCompletitudRow> Peores)
{
    public static readonly ResumenCompletitud Vacio = new(0, 0, 0, 0, 0, 0, [], [], []);
    public int Incompletas => Total - Completas;
    public static int Pct(int n, int total) => Redondeo.Pct(n, total);
}

/// <summary>Los porcentajes de la pantalla redondean como una persona: 62,5 es 63. El redondeo
/// por defecto de .NET es el bancario (al par), y daba 62 — un número que nadie obtiene haciendo
/// la cuenta a mano.</summary>
internal static class Redondeo
{
    public static int Entero(double x) => (int)Math.Round(x, MidpointRounding.AwayFromZero);
    public static int Pct(int n, int total) => total == 0 ? 0 : Entero(100.0 * n / total);
}
