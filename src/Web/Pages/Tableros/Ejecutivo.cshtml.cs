using Diger.TramitesEstado.Application.Dashboards.Queries.GetEjecutivoProyectos;
using Diger.TramitesEstado.Application.Unidades.Queries;
using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Web.Pages.Tableros;

/// <summary>
/// Tablero ejecutivo de proyectos: la cartera de un área para jefaturas, en sala o en el
/// escritorio. Especificación: <c>docs/tableros/ejecutivo-proyectos.md</c>.
///
/// <para>Pide <c>Proyectos.Ver</c>, como sus hermanos de proyectos: el alcance (institución, área,
/// unidad; el responsable siempre ve su proyecto) lo imponen los filtros globales de la base, y
/// los filtros de esta página solo recortan lo que ya es visible. Solo lectura: no hay POST.</para>
/// </summary>
[Authorize]
[Permission("Proyectos", AccionModulo.Ver, "Ver proyectos")]
public sealed class EjecutivoModel(
    ISender sender, ICurrentUserService currentUser, IConfiguration config,
    IOptions<InstitucionOptions> institucion) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Area        { get; set; }
    [BindProperty(SupportsGet = true)] public int?    Categoria   { get; set; }
    [BindProperty(SupportsGet = true)] public string? Unidad      { get; set; }
    [BindProperty(SupportsGet = true)] public Guid?   Responsable { get; set; }
    [BindProperty(SupportsGet = true)] public List<int> Proyecto  { get; set; } = [];

    public EjecutivoProyectosDto       Data     { get; private set; } = default!;
    public EjecutivoProyectosContexto  Contexto { get; private set; } = default!;
    public InstitucionOptions          Institucion => institucion.Value;

    /// <summary>Nombre de la unidad aplicada cuando no tiene proyectos en esta cartera y por eso
    /// no está entre las opciones del menú.</summary>
    public string? UnidadFueraNombre { get; private set; }

    /// <summary>Hay algún recorte elegido por el usuario: se ofrece «Limpiar». Cuenta lo pedido,
    /// aunque no aplique en esta categoría: sigue en la URL y «Limpiar» es la forma de soltarlo.</summary>
    public bool HayFiltro => Contexto.CategoriaId is not null
                          || Unidad is not null || Responsable is not null || Proyecto.Count > 0
                          || (Contexto.PuedeElegirArea && Request.Query.ContainsKey(nameof(Area)));

    /// <summary>
    /// Enlace a este tablero con otra categoría. Conserva todo lo demás tal como vino en la URL:
    /// el área —incluido «todas», que es <c>Area=</c> vacío—, la unidad o el responsable y los
    /// proyectos elegidos. Se conserva lo <b>pedido</b> y no lo aplicado: una unidad sin proyectos en
    /// la nueva categoría se ignora ahí (la consulta la descarta) y vuelve a aplicarse al regresar.
    /// </summary>
    public string Enlace(int? categoria) => Armar(categoria, Unidad, Responsable, Proyecto);

    /// <summary>Enlace con otra unidad o responsable (son excluyentes; los dos null = toda la
    /// categoría), en la categoría actual y con los proyectos elegidos.</summary>
    public string EnlaceSub(string? unidad, Guid? responsable) =>
        Armar(Contexto.CategoriaId, unidad, responsable, Proyecto);

    /// <summary>El recorte actual sin la selección de proyectos.</summary>
    public string EnlaceSinProyectos() => Armar(Contexto.CategoriaId, Unidad, Responsable, []);

    private string Armar(int? categoria, string? unidad, Guid? responsable, IEnumerable<int> proyectos)
    {
        var qs = new List<string>();
        if (Request.Query.ContainsKey(nameof(Area))) qs.Add($"Area={Uri.EscapeDataString(Area ?? "")}");
        if (categoria is not null)   qs.Add($"Categoria={categoria}");
        if (unidad is not null)      qs.Add($"Unidad={Uri.EscapeDataString(unidad)}");
        if (responsable is not null) qs.Add($"Responsable={responsable}");
        qs.AddRange(proyectos.Distinct().Select(p => $"{nameof(Proyecto)}={p}"));
        return "/Tableros/Ejecutivo" + (qs.Count == 0 ? "" : "?" + string.Join("&", qs));
    }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Contexto = await EjecutivoProyectosContexto.ResolverAsync(
            sender, currentUser, config, institucion.Value,
            Area, Request.Query.ContainsKey(nameof(Area)), Categoria,
            conOpciones: true, ct);

        Data = await sender.Send(new GetEjecutivoProyectosQuery(
            Contexto.AreaId, Unidad, Responsable, Contexto.CategoriaId, Proyecto), ct);

        if (Data.UnidadAplicada is { } u && Data.Unidades.All(o => o.Id != u))
        {
            try { UnidadFueraNombre = (await sender.Send(new GetUnidadByIdQuery(u), ct)).Nombre; }
            catch (NotFoundException) { UnidadFueraNombre = u; }
        }
    }
}
