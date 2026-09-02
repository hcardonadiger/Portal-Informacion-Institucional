using System.Text;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Common;
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
public sealed class IndexModel(ISender sender, AccesoModulosService acceso) : PageModel
{
    [BindProperty(SupportsGet = true)] public EstadoProyecto?    Estado        { get; set; }
    [BindProperty(SupportsGet = true)] public Guid?              ResponsableId { get; set; }
    [BindProperty(SupportsGet = true)] public int?               Anio          { get; set; }
    [BindProperty(SupportsGet = true)] public string?            Q             { get; set; }
    [BindProperty(SupportsGet = true)] public PrioridadProyecto? Prioridad     { get; set; }
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
    [BindProperty] public PrioridadProyecto NuevaPrioridad  { get; set; } = PrioridadProyecto.Media;
    [BindProperty] public AccionProyecto?   NuevaAccion     { get; set; }
    [BindProperty] public DateOnly?         NuevaFechaInicio{ get; set; }
    [BindProperty] public DateOnly?         NuevaFechaFin   { get; set; }

    public IReadOnlyList<ProyectoListItemDto>  Proyectos { get; private set; } = [];
    public IReadOnlyList<UsuarioAsignableDto>  Usuarios  { get; private set; } = [];
    public bool PuedeCrear { get; private set; }

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    [Permission("Proyectos", AccionModulo.Crear, "Crear proyectos")]
    public async Task<IActionResult> OnPostCrearAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(NuevoNombre))
        {
            TempData["ErrorMsg"] = "El proyecto necesita un nombre.";
            return RedirectToPage();
        }

        try
        {
            // El nombre del responsable se guarda como snapshot junto al Id: el listado y el
            // histórico lo muestran aunque después el usuario cambie de nombre o se desactive.
            var usuarios = await sender.Send(new GetUsuariosAsignablesQuery(), ct);
            var responsable = usuarios.FirstOrDefault(u => u.Id == NuevoResponsable)?.Nombre;

            // Área y unidad nacen vacías: el proyecto queda transversal a la institución y se
            // acota después desde la ficha, si hace falta. La institución la pone el comando
            // con la del usuario que crea.
            var id = await sender.Send(new CrearProyectoCommand(
                NuevoNombre, NuevoObjetivo, AreaId: null, UnidadId: null,
                ResponsableId: NuevoResponsable, Responsable: responsable,
                Prioridad: NuevaPrioridad, Accion: NuevaAccion,
                FechaInicioPlan: NuevaFechaInicio, FechaFinPlan: NuevaFechaFin), ct);

            TempData["SuccessMsg"] = "Proyecto creado. Cargue sus entregables y las actividades de cada uno.";
            return RedirectToPage("/Proyectos/Editor", new { id });
        }
        catch (DomainException ex)
        {
            TempData["ErrorMsg"] = ex.Message;
            return RedirectToPage();
        }
    }

    private async Task CargarAsync(CancellationToken ct)
    {
        Proyectos  = await sender.Send(new GetProyectosQuery(Estado, ResponsableId, Anio, Q, Prioridad, AreaId, UnidadId, Senal, Accion), ct);
        PuedeCrear = await acceso.PuedeClaveAsync("Proyectos.Crear", ct);

        // La lista de usuarios solo hace falta para el filtro y el modal de alta.
        Usuarios = await sender.Send(new GetUsuariosAsignablesQuery(), ct);
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
        var proyectos = await sender.Send(new GetProyectosQuery(Estado, ResponsableId, Anio, Q, Prioridad, AreaId, UnidadId, Senal, Accion), ct);

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",",
            "Código", "Nombre", "Estado", "Acción", "Prioridad", "Responsable",
            "Inicio planificado", "Cierre planificado", "Cierre real",
            "Avance % calculado", "Avance % por entregables cerrados", "Brecha (pts)",
            "Último reporte", "Días sin reporte",
            "Entregables", "Entregables completados", "Entregables atrasados",
            "Actividades", "Actividades vencidas",
            "Atrasado", "Sin línea base", "Sin reportar", "Divergente", "Sin desglosar"));

        foreach (var p in proyectos)
            sb.AppendLine(string.Join(",",
                Q_(p.Codigo), Q_(p.Nombre), Q_(EstadoTxt(p.Estado)),
                Q_(EtiquetasProyecto.Accion(p.Accion)), Q_(p.Prioridad.ToString()),
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
