using Diger.TramitesEstado.Application.Dashboards.Queries.GetMisProyectosDashboard;

namespace Diger.TramitesEstado.Web.Pages.Tableros;

/// <summary>
/// Tablero de nivel Área: los mismos proyectos que ve <c>/Tableros/ProyectosUnidad</c>, pero
/// desglosados por unidad.
///
/// <para><b>Consume la misma consulta a propósito.</b> Un jefe de área no necesita un alcance
/// aparte: la sincronización automática de interesados (<c>IInteresadosAutomaticosSync</c>) ya lo
/// dejó como interesado de cada proyecto de su área, así que «mis proyectos» y «los proyectos de
/// mi área» son el mismo conjunto para él. Darle a esta página una consulta propia que leyera por
/// <c>AreaId</c> sería un segundo camino de acceso a los proyectos —uno que no pasa por
/// <c>InteresadoProyecto</c>— y habría que auditarlo aparte.</para>
///
/// <para>Lo único propio de esta vista es la presentación: el desglose por unidad lo arma la
/// vista sobre la lista que ya trae la consulta, sin una segunda consulta.</para>
/// </summary>
[Authorize]
// Misma clave que sus hermanos /Tableros/Proyectos y /Tableros/ProyectosUnidad: los proyectos
// internos no tienen filtro de alcance en AppDbContext, así que quién los ve lo decide
// "Proyectos.Ver" y no "Tableros.Ver".
[Permission("Proyectos", AccionModulo.Ver, "Ver proyectos")]
public sealed class ProyectosAreaModel(ISender sender) : PageModel
{
    public MisProyectosDashboardDto Data { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken ct)
    {
        Data = await sender.Send(new GetMisProyectosDashboardQuery(), ct);
    }
}
