using System.Text;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Common;
using Diger.TramitesEstado.Application.Proyectos.Categorias;
using Diger.TramitesEstado.Application.Proyectos.Plantillas;
using Diger.TramitesEstado.Application.Proyectos.Prioridades;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using Diger.TramitesEstado.Application.Tickets.Common;
using Diger.TramitesEstado.Application.Tickets.Queries.GetUsuariosAsignables;
using Diger.TramitesEstado.Infrastructure.Security;
// Con alias y no con un using suelto: hay otra clase Etiquetas en Tickets y el nombre a secas se
// vuelve ambiguo. El alias deja el rótulo de la acción en un solo lugar, el mismo que usa el tablero.
using EtiquetasProyecto = Diger.TramitesEstado.Application.Dashboards.Queries.Etiquetas;

namespace Diger.TramitesEstado.Web.Pages.Proyectos;

[Authorize]
[Permission("Proyectos", AccionModulo.Ver, "Ver proyectos")]
public sealed class IndexModel(ISender sender, AccesoModulosService acceso, ICurrentUserService currentUser) : PageModel
{
    [BindProperty(SupportsGet = true)] public EstadoProyecto?    Estado        { get; set; }
    [BindProperty(SupportsGet = true)] public Guid?              ResponsableId { get; set; }
    [BindProperty(SupportsGet = true)] public int?               Anio          { get; set; }
    [BindProperty(SupportsGet = true)] public string?            Q             { get; set; }
    [BindProperty(SupportsGet = true)] public int?               PrioridadId   { get; set; }
    [BindProperty(SupportsGet = true)] public int?               CategoriaId   { get; set; }
    [BindProperty(SupportsGet = true)] public AccionProyecto?    Accion        { get; set; }
    [BindProperty(SupportsGet = true)] public string?            AreaId        { get; set; }
    [BindProperty(SupportsGet = true)] public string?            UnidadId      { get; set; }

    /// <summary>Señal por la que se filtra. Es lo que hace clicables los indicadores del tablero:
    /// «9 sin responsable» lleva a esos 9 en vez de quedar en un número.</summary>
    [BindProperty(SupportsGet = true)] public SenalProyecto?     Senal         { get; set; }

    // Alta rápida desde el modal del listado.
    [BindProperty] public string?           NuevoNombre     { get; set; }
    [BindProperty] public string?           NuevoObjetivo   { get; set; }
    [BindProperty] public Guid?             NuevoResponsable{ get; set; }
    [BindProperty] public int?              NuevaPrioridadId{ get; set; }
    [BindProperty] public int?              NuevaCategoriaId{ get; set; }
    [BindProperty] public AccionProyecto?   NuevaAccion     { get; set; }
    [BindProperty] public DateOnly?         NuevaFechaInicio{ get; set; }
    [BindProperty] public DateOnly?         NuevaFechaFin   { get; set; }

    /// <summary>Plantilla cuya estructura se copia al proyecto nuevo. <c>0</c> es «sin plantilla»
    /// elegido a propósito; null es que el formulario todavía no se envió, y entonces se propone
    /// la genérica.</summary>
    [BindProperty] public int?              NuevaPlantillaId{ get; set; }

    /// <summary>Plantillas activas que alcanzan a quien crea, para el alta rápida.</summary>
    public IReadOnlyList<PlantillaProyectoResumenDto> Plantillas { get; private set; } = [];

    /// <summary>La plantilla que llega marcada en el modal: la elegida si se vuelve de un rechazo y,
    /// si no, «Proyecto genérico», para que el proyecto no arranque con la estructura en blanco.</summary>
    public int PlantillaPropuesta => NuevaPlantillaId
        ?? Plantillas.FirstOrDefault(p => p.Nombre == PlantillaGenericaSeedService.Nombre)?.Id
        ?? 0;

    public IReadOnlyList<ProyectoListItemDto>  Proyectos { get; private set; } = [];
    public IReadOnlyList<UsuarioAsignableDto>  Usuarios  { get; private set; } = [];

    /// <summary>Opciones vivas del catálogo, para el filtro y el alta rápida.</summary>
    public IReadOnlyList<OpcionPrioridadDto>   Prioridades { get; private set; } = [];

    /// <summary>Opciones vivas del catálogo de categorías, para el filtro y el alta rápida.</summary>
    public IReadOnlyList<OpcionCategoriaDto>   Categorias  { get; private set; } = [];
    public bool PuedeCrear { get; private set; }

    /// <summary>Quién queda de responsable en el modal de alta: el elegido si se vuelve de un
    /// rechazo, y si no quien crea, que es lo que hace el comando cuando no se elige a nadie.</summary>
    public Guid? ResponsablePropuesto => NuevoResponsable ?? currentUser.UserId;

    /// <summary>
    /// El rechazo del alta rápida, dicho dentro del propio modal.
    ///
    /// <para>Antes esto iba a <c>TempData["ErrorMsg"]</c> con un redirect detrás, y el redirect era
    /// el problema: la página se volvía a pintar vacía y lo que la persona acababa de escribir en el
    /// modal —nombre, objetivo, fechas— se perdía. Había que teclearlo todo otra vez para enterarse,
    /// quizá, del siguiente error. Ahora el handler devuelve la misma página: las propiedades
    /// enlazadas conservan lo escrito, el modal se vuelve a abrir con los valores puestos y el
    /// mensaje aparece adentro, no detrás del fondo oscuro.</para>
    ///
    /// <para>La mayoría de estos rechazos ya no debería llegar: el formulario los revisa en el
    /// navegador antes de postear (ver <c>prevalidacion.js</c>). Esto es la red para lo que el
    /// navegador no puede saber.</para>
    /// </summary>
    public string? ErrorAlta { get; private set; }

    /// <summary>Si el modal de alta tiene que llegar abierto. Lo pide el único caso en que vale:
    /// volver de un rechazo con lo escrito todavía en pantalla.</summary>
    public bool ReabrirAlta => ErrorAlta is not null;

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    [Permission("Proyectos", AccionModulo.Crear, "Crear proyectos")]
    public async Task<IActionResult> OnPostCrearAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(NuevoNombre))
            return await VolverAlAltaAsync("El proyecto necesita un nombre.", ct);

        try
        {
            // Área y unidad nacen vacías: el proyecto queda transversal a la institución y se
            // acota después desde la ficha, si hace falta. La institución la pone el comando
            // con la del usuario que crea; el responsable también, si no se eligió otro, y el
            // nombre que se guarda como snapshot lo resuelve el comando contra el padrón.
            var id = await sender.Send(new CrearProyectoCommand(
                NuevoNombre, NuevoObjetivo, AreaId: null, UnidadId: null,
                ResponsableId: NuevoResponsable,
                PrioridadId: NuevaPrioridadId, CategoriaId: NuevaCategoriaId, Accion: NuevaAccion,
                FechaInicioPlan: NuevaFechaInicio, FechaFinPlan: NuevaFechaFin), ct);

            // La estructura se trae con el mismo comando que usa la ficha, una vez creado el
            // proyecto. Si la plantilla falla, el proyecto ya existe: se avisa y se sigue, porque
            // devolver al modal haría crear un duplicado al reintentar.
            var conPlantilla = "";
            if (NuevaPlantillaId is > 0 and var plantillaId)
            {
                try
                {
                    var cuantos = await sender.Send(new TraerPlantillaAProyectoCommand(id, plantillaId), ct);
                    conPlantilla = $" Se trajo la estructura de la plantilla: {cuantos} entregable{(cuantos == 1 ? "" : "s")}.";
                }
                catch (DomainException ex)
                {
                    TempData["ErrorMsg"] = $"El proyecto se creó, pero no se pudo traer la plantilla: {ex.Message}";
                }
            }

            // El alta sigue en el equipo y no en la estructura: de los interesados salen los
            // responsables de entregables y actividades, así que armar la estructura primero
            // obligaba a volver a ella después a asignarlos. El paso se puede omitir.
            TempData["SuccessMsg"] = "Proyecto creado." + conPlantilla +
                                     " Agregue a su equipo, o omita este paso, y siga con la estructura.";
            return RedirectToPage("/Proyectos/Editor", pageHandler: null,
                routeValues: new { id, alta = true }, fragment: "interesados");
        }
        catch (DomainException ex)
        {
            return await VolverAlAltaAsync(ex.Message, ct);
        }
    }

    /// <summary>Vuelve al listado con el modal de alta abierto, su contenido intacto y el motivo del
    /// rechazo adentro. Recarga el listado porque <see cref="Page"/> no pasa por <c>OnGet</c>.</summary>
    private async Task<IActionResult> VolverAlAltaAsync(string motivo, CancellationToken ct)
    {
        ErrorAlta = motivo;
        await CargarAsync(ct);
        return Page();
    }

    private async Task CargarAsync(CancellationToken ct)
    {
        Proyectos  = await sender.Send(new GetProyectosQuery(Estado, ResponsableId, Anio, Q, PrioridadId, CategoriaId, AreaId, UnidadId, Senal, Accion), ct);
        PuedeCrear = await acceso.PuedeClaveAsync("Proyectos.Crear", ct);

        // La lista de usuarios solo hace falta para el filtro y el modal de alta.
        Usuarios = await sender.Send(new GetUsuariosAsignablesQuery(), ct);
        Prioridades = await sender.Send(new GetOpcionesPrioridadQuery(), ct);
        Categorias  = await sender.Send(new GetOpcionesCategoriaQuery(), ct);
        if (PuedeCrear) Plantillas = await sender.Send(new GetPlantillasProyectoQuery(), ct);
    }

    /// <summary>
    /// Exporta el portafolio como CSV, respetando los filtros que tenga puestos el listado.
    ///
    /// <para>Sale de la misma consulta que la pantalla, así que arrastra el filtro de alcance: cada
    /// quien exporta lo que puede ver, no el portafolio entero.</para>
    ///
    /// <para>Las dos últimas columnas son texto y no fecha a propósito: «Atrasado» y «Sin línea
    /// base» son la lectura que hace el portal, y sin ellas quien abra el archivo tiene que
    /// recalcularlas —y un proyecto sin fecha de cierre se le cuela como si fuera a tiempo.</para>
    /// </summary>
    public async Task<IActionResult> OnGetExportAsync(CancellationToken ct)
    {
        var proyectos = await sender.Send(new GetProyectosQuery(Estado, ResponsableId, Anio, Q, PrioridadId, CategoriaId, AreaId, UnidadId, Senal, Accion), ct);

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",",
            "Código", "Nombre", "Estado", "Acción", "Prioridad", "Categoría", "Responsable",
            "Inicio planificado", "Cierre planificado", "Cierre real",
            "Avance % calculado", "Avance % por entregables cerrados", "Brecha (pts)",
            "Último reporte", "Días sin reporte",
            "Entregables", "Entregables completados", "Entregables atrasados",
            "Actividades", "Actividades vencidas",
            "Atrasado", "Sin línea base", "Sin reportar", "Divergente", "Sin desglosar"));

        foreach (var p in proyectos)
            sb.AppendLine(string.Join(",",
                Q_(p.Codigo), Q_(p.Nombre), Q_(EstadoTxt(p.Estado)),
                Q_(EtiquetasProyecto.Accion(p.Accion)), Q_(p.Prioridad), Q_(p.Categoria ?? ""),
                Q_(p.Responsable ?? "sin asignar"),
                Q_(F(p.FechaInicioPlan)), Q_(F(p.FechaFinPlan)), Q_(F(p.FechaFinReal)),
                p.AvancePct.ToString(),
                p.TotalEntregables > 0 ? p.AvanceFisico.ToString() : "",
                p.TotalEntregables > 0 ? p.Brecha.ToString() : "",
                Q_(p.UltimoAvance?.ToLocalTime().ToString("yyyy-MM-dd") ?? "sin reportes"),
                p.UltimoAvance is { } u ? ((int)(DateTime.UtcNow - u).TotalDays).ToString() : "",
                p.TotalEntregables.ToString(), p.EntregablesCompletados.ToString(), p.EntregablesAtrasados.ToString(),
                p.TotalActividades.ToString(), p.ActividadesAtrasadas.ToString(),
                Q_(p.EstaAtrasado ? "sí" : "no"),
                Q_(p.SinLineaBase ? "sí" : "no"),
                Q_(p.SinReportar ? "sí" : "no"),
                Q_(p.Divergente ? "sí" : "no"),
                Q_(p.SinDesglose ? "sí" : "no")));

        // El preámbulo va porque Excel en Windows abre el CSV en ANSI si no encuentra el BOM, y
        // se come todos los acentos. Mismo motivo por el que los .sql del repo lo llevan.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"portafolio_proyectos_{DateTime.Now:yyyyMMdd}.csv");

        static string F(DateOnly? d) => d?.ToString("yyyy-MM-dd") ?? "";
        static string Q_(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";

        // Local en vez de la clase Etiquetas de Dashboards: hay otra con el mismo nombre en
        // Tickets, internal, y el compilador se queda con esa.
        static string EstadoTxt(EstadoProyecto e) => e == EstadoProyecto.EnEjecucion ? "En ejecución" : e.ToString();
    }
}
