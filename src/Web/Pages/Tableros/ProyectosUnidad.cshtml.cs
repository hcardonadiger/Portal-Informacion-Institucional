using Diger.TramitesEstado.Application.Dashboards.Queries.GetMisProyectosDashboard;

namespace Diger.TramitesEstado.Web.Pages.Tableros;

/// <summary>
/// Tablero de nivel Unidad: los proyectos donde el usuario figura como interesado o
/// responsable. No lleva filtros propios —el recorte lo hace la consulta a partir de la
/// identidad de quien pregunta, no de un parámetro de la URL— y por eso el PageModel es
/// un pasamanos: si aceptara un "usuarioId" cualquiera dejaría de ser "mis proyectos".
/// </summary>
[Authorize]
// Misma clave que sus hermanos /Tableros/Proyectos y /Proyectos/Editor: los proyectos
// internos no tienen filtro de alcance en AppDbContext, así que quién los ve lo decide
// "Proyectos.Ver" y no "Tableros.Ver".
[Permission("Proyectos", AccionModulo.Ver, "Ver proyectos")]
public sealed class ProyectosUnidadModel(ISender sender) : PageModel
{
    public MisProyectosDashboardDto Data { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken ct)
    {
        Data = await sender.Send(new GetMisProyectosDashboardQuery(), ct);
    }
}
