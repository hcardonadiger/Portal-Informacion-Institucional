using Diger.TramitesEstado.Application.Dashboards.Queries;
using Diger.TramitesEstado.Application.Tickets.Common;
using Diger.TramitesEstado.Application.Tickets.Queries.GetUsuariosAsignables;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.Tableros;

[Authorize]
// Sus hermanos de /Tableros piden "Tableros.Ver"; este pide "Proyectos.Ver" a propósito.
// Los proyectos internos no tienen filtro de alcance en AppDbContext — quién los ve lo
// decide su permiso — así que gatear este tablero con la clave de los tableros abriría
// por la ventana lo que el módulo cierra por la puerta. La regla del DESIGN.md aplica
// igual: el enlace del hub se muestra con la clave de su destino.
[Permission("Proyectos", AccionModulo.Ver, "Ver proyectos")]
public sealed class ProyectosModel(ISender sender) : PageModel
{
    public ProyectosDashboardDto Data { get; private set; } = default!;
    public IReadOnlyList<UsuarioAsignableDto> Usuarios { get; private set; } = [];

    [BindProperty(SupportsGet = true)] public EstadoProyecto?    Estado        { get; set; }
    [BindProperty(SupportsGet = true)] public Guid?              ResponsableId { get; set; }
    [BindProperty(SupportsGet = true)] public PrioridadProyecto? Prioridad     { get; set; }

    public bool HayFiltro => Estado is not null || ResponsableId is not null || Prioridad is not null;

    public async Task OnGetAsync(CancellationToken ct)
    {
        Data     = await sender.Send(new GetProyectosDashboardQuery(Estado, ResponsableId, Prioridad), ct);
        Usuarios = await sender.Send(new GetUsuariosAsignablesQuery(), ct);
    }
}
