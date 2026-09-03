using Diger.TramitesEstado.Application.Areas.Queries;
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
public sealed class ProyectosModel(ISender sender, ICurrentUserService currentUser) : PageModel
{
    public ProyectosDashboardDto Data { get; private set; } = default!;
    public IReadOnlyList<UsuarioAsignableDto> Usuarios { get; private set; } = [];
    public IReadOnlyList<AreaListItemDto> Areas { get; private set; } = [];

    [BindProperty(SupportsGet = true)] public EstadoProyecto?    Estado        { get; set; }
    [BindProperty(SupportsGet = true)] public Guid?              ResponsableId { get; set; }
    [BindProperty(SupportsGet = true)] public PrioridadProyecto? Prioridad     { get; set; }
    [BindProperty(SupportsGet = true)] public string[]?          AreaIds       { get; set; }

    public bool HayFiltro => Estado is not null || ResponsableId is not null || Prioridad is not null
                           || (AreaIds?.Length ?? 0) > 0;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        // Este es el tablero del portafolio completo de la institución. Quien no llega a ese
        // nivel no ve una página vacía ni un 403: se le lleva al tablero que sí es suyo. El
        // corte lo decide el alcance del rol (y la capacidad EsJefeDeArea), nunca su nombre.
        var puedeInstitucion = currentUser.EsGlobal
            || currentUser.NivelAlcance is NivelAlcance.Institucion or NivelAlcance.Global;

        if (!puedeInstitucion)
        {
            return currentUser.EsJefeDeArea
                ? RedirectToPage("/Tableros/ProyectosArea")
                : RedirectToPage("/Tableros/ProyectosUnidad");
        }

        Data     = await sender.Send(new GetProyectosDashboardQuery(Estado, ResponsableId, Prioridad, AreaIds), ct);
        Usuarios = await sender.Send(new GetUsuariosAsignablesQuery(), ct);

        // Acotada a la institución activa, y NO por gusto: el filtro global de Area en
        // AppDbContext es `_alcanceGlobal || a.InstitucionId == _activeInst`, así que para el
        // destinatario natural de este tablero —un rol global— el filtro se cortocircuita y la
        // consulta sin argumento devolvería las áreas de TODAS las instituciones. El desplegable
        // ofrecería áreas que este tablero no muestra y filtrar por una devolvería un tablero
        // vacío sin explicación.
        var areas = await sender.Send(new GetAreasQuery(currentUser.ActiveInstitucionId), ct);

        // Las áreas desactivadas no se ofrecen —ya no son una opción a futuro— salvo la que el
        // usuario trae seleccionada: una URL guardada de cuando el área seguía activa sigue
        // recortando el tablero, y si su opción desapareciera del desplegable el recorte no se
        // vería en ninguna parte ni habría manera de deseleccionarla desde la propia lista.
        Areas = areas
            .Where(a => a.Activo || AreaIds?.Contains(a.Id) == true)
            .ToList();

        return Page();
    }
}
