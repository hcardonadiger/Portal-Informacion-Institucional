using Diger.TramitesEstado.Application.Dashboards.Queries.GetEjecutivoProyectos;
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
    [BindProperty(SupportsGet = true)] public string? Unidad      { get; set; }
    [BindProperty(SupportsGet = true)] public Guid?   Responsable { get; set; }

    public EjecutivoProyectosDto       Data     { get; private set; } = default!;
    public EjecutivoProyectosContexto  Contexto { get; private set; } = default!;
    public InstitucionOptions          Institucion => institucion.Value;

    /// <summary>Hay algún recorte elegido por el usuario: se ofrece «Limpiar».</summary>
    public bool HayFiltro => Contexto.UnidadId is not null || Contexto.ResponsableId is not null
                          || (Contexto.PuedeElegirArea && Request.Query.ContainsKey(nameof(Area)));

    public async Task OnGetAsync(CancellationToken ct)
    {
        Contexto = await EjecutivoProyectosContexto.ResolverAsync(
            sender, currentUser, config, institucion.Value,
            Area, Request.Query.ContainsKey(nameof(Area)), Unidad, Responsable,
            conOpciones: true, ct);

        Data = await sender.Send(new GetEjecutivoProyectosQuery(
            Contexto.AreaId, Contexto.UnidadId, Contexto.ResponsableId), ct);
    }
}
