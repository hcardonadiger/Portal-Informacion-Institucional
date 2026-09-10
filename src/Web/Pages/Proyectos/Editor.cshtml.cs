using Diger.TramitesEstado.Application.Proyectos.Commands.RegistrarDescargaDocumento;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Common;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using Diger.TramitesEstado.Application.Tickets.Common;
using Diger.TramitesEstado.Application.Tickets.Queries.GetUsuariosAsignables;
using Diger.TramitesEstado.Infrastructure.Security;
using Diger.TramitesEstado.Web.Common;

namespace Diger.TramitesEstado.Web.Pages.Proyectos;

[Authorize]
// Los OnGet solo consultan (incluida la descarga de evidencia, que es lectura del proyecto).
// Las mutaciones llevan su propio [Permission] por handler más abajo.
[Permission("Proyectos", AccionModulo.Ver, "Ver proyectos")]
public sealed class EditorModel(
    ISender sender,
    AccesoModulosService acceso,
    ICurrentUserService currentUser,
    IProyectoPdfService proyectoPdf,
    IWebHostEnvironment env) : PageModel
{
    public ProyectoDetailDto Proyecto { get; private set; } = default!;
    public IReadOnlyList<AvanceProyectoDto> Avances { get; private set; } = [];
    public IReadOnlyList<BitacoraProyectoDto> Auditoria { get; private set; } = [];
    public IReadOnlyList<RiesgoProyectoDto> Riesgos { get; private set; } = [];
    public IReadOnlyList<InteresadoProyectoDto> Interesados { get; private set; } = [];

    /// <summary>Reuniones, expedientes y tickets vinculados, más el conteo de los que existen pero quedan
    /// fuera del alcance de quien mira. Ver la nota de GetVinculosProyectoQuery.</summary>
    public VinculosProyectoDto Vinculos { get; private set; } = new([], [], [], 0, 0, 0);

    /// <summary>Lo que se puede vincular: dentro del alcance y no vinculado ya.</summary>
    public IReadOnlyList<OpcionVinculoDto> ReunionesVinculables   { get; private set; } = [];
    public IReadOnlyList<OpcionVinculoDto> ExpedientesVinculables { get; private set; } = [];
    public IReadOnlyList<OpcionVinculoDto> TicketsVinculables     { get; private set; } = [];
    public AlcanceOpcionesDto Alcance { get; private set; } = new([], []);
    public IReadOnlyList<UsuarioAsignableDto> Usuarios { get; private set; } = [];

    /// <summary>El cronograma dibujable del proyecto. Se arma en la capa de aplicación —la
    /// aritmética de porcentajes tiene pruebas— y la vista solo aplica left y width.</summary>
    public CronogramaDto Cronograma { get; private set; } = new(null, null, [], [], [], null);

    /// <summary>El repositorio documental del proyecto, agrupado por categoría en la vista.</summary>
    public IReadOnlyList<DocumentoProyectoDto> Documentos { get; private set; } = [];

    /// <summary>Solo las activas: una categoría desactivada no se ofrece para clasificar algo
    /// nuevo, pero los documentos que ya la tienen la conservan y la muestran.</summary>
    public IReadOnlyList<CategoriaDocumentoDto> Categorias { get; private set; } = [];

    public bool PuedeEditar         { get; private set; }
    public bool PuedeReabrir        { get; private set; }
    public bool PuedeEliminar       { get; private set; }
    public bool PuedeReportarAvance { get; private set; }

    /// <summary>Claves propias del submódulo de documentos: se puede consultar la documentación
    /// sin poder subirla, y subirla sin poder archivarla.</summary>
    public bool PuedeVerDocumentos      { get; private set; }
    public bool PuedeSubirDocumentos    { get; private set; }
    public bool PuedeEditarDocumentos   { get; private set; }
    public bool PuedeArchivarDocumentos { get; private set; }

    /// <summary>El usuario en sesión es el responsable del proyecto. Es lo único que habilita a
    /// corregir la bitácora: esa acción no admite bypass de administrador — ver
    /// <c>PropiedadProyecto</c> en la capa de aplicación, que es quien realmente lo exige.
    /// Acá solo decide si se pintan los controles, para no ofrecer lo que el comando va a rechazar.</summary>
    public bool EsPropietario { get; private set; }

    /// <summary>Reordenar toca el cronograma: exige el permiso de edición y además ser el dueño
    /// <b>o</b> administrador. Solo tiene sentido con más de un entregable que mover.</summary>
    public bool PuedeReordenar { get; private set; }

    /// <summary>Misma atribución, un nivel más abajo. Va aparte de <see cref="PuedeReordenar"/>
    /// porque un proyecto con un solo entregable puede tener diez actividades que ordenar.</summary>
    public bool PuedeReordenarActividades { get; private set; }

    /// <summary>Corregir la bitácora tiene clave propia — se puede reportar sin poder enmendar.</summary>
    public bool PuedeCorregirBitacora { get; private set; }

    /// <summary>
    /// Días desde el último reporte de avance; null si nunca se reportó. Se calcula acá y no en el
    /// DTO del proyecto porque depende de la bitácora, que es otra consulta.
    /// </summary>
    public int? DiasSinReportar { get; private set; }

    /// <summary>En ejecución y sin reportes en más de 30 días. Mismo umbral que el listado y el
    /// tablero, para que las tres pantallas no digan cosas distintas del mismo proyecto.</summary>
    public bool SinReportar =>
        Proyecto.Estado == EstadoProyecto.EnEjecucion && (DiasSinReportar is null || DiasSinReportar > 30);

    /// <summary>Una fila de la tabla de entregables tal como viaja en el formulario.</summary>
    public sealed class EntregableForm
    {
        /// <summary>Id del entregable existente; 0 en una fila recién agregada. Sin esto el comando
        /// tendría que borrar y recrear, y eso desimputa los avances de la bitácora.</summary>
        public int              Id            { get; set; }
        public string?          Nombre        { get; set; }
        public string?          Descripcion   { get; set; }
        public DateOnly?        FechaPlan     { get; set; }
        public EstadoEntregable Estado        { get; set; } = EstadoEntregable.Pendiente;
        public Guid?            ResponsableId { get; set; }

        public List<ActividadForm> Actividades { get; set; } = [];
    }

    /// <summary>Una fila de actividad, anidada bajo su entregable en el mismo formulario.</summary>
    public sealed class ActividadForm
    {
        public int       Id              { get; set; }
        public string?   Nombre          { get; set; }
        public string?   Descripcion     { get; set; }
        public DateOnly? FechaInicioPlan { get; set; }
        public DateOnly? FechaFinPlan    { get; set; }
        public int       AvancePct       { get; set; }
        public bool      Cancelada       { get; set; }
        public Guid?     ResponsableId   { get; set; }

        /// <summary>Ids de las actividades de las que depende esta. Viajan como campos repetidos
        /// con el mismo nombre —sin índice— porque son enteros sueltos: el binder los junta en la
        /// lista igual, y así quitar un chip en el navegador no obliga a renumerar nada.</summary>
        public List<int> Predecesoras { get; set; } = [];
    }

    // ── Ficha ───────────────────────────────────────────────────────────────
    [BindProperty] public string?           Nombre          { get; set; }
    [BindProperty] public string?           Objetivo        { get; set; }
    [BindProperty] public Guid?             ResponsableId   { get; set; }
    [BindProperty] public string?           AreaId          { get; set; }
    [BindProperty] public string?           UnidadId        { get; set; }
    [BindProperty] public PrioridadProyecto Prioridad       { get; set; }
    [BindProperty] public AccionProyecto?   Accion          { get; set; }
    [BindProperty] public DateOnly?         FechaInicioPlan { get; set; }
    [BindProperty] public DateOnly?         FechaFinPlan    { get; set; }
    [BindProperty] public List<EntregableForm> Entregables  { get; set; } = [];

    // ── Vínculos ────────────────────────────────────────────────────────────
    [BindProperty] public int     VinculoReunionId    { get; set; }
    [BindProperty] public int     VinculoExpedienteId { get; set; }
    [BindProperty] public int     VinculoTicketId     { get; set; }
    [BindProperty] public string? VinculoNota         { get; set; }
    [BindProperty] public int     VinculoId           { get; set; }

    // ── Documentos ──────────────────────────────────────────────────────────
    /// <summary>El archivo de la versión nueva. El alta de documentos usa
    /// <see cref="DocArchivos"/>, que acepta varios.</summary>
    [BindProperty] public IFormFile? DocArchivo     { get; set; }

    /// <summary>Los archivos del alta. <b>Cada uno da de alta su propio documento</b>, no varias
    /// versiones del mismo: subir cinco actas es cinco documentos, y decidir lo contrario habría
    /// convertido un lote en un historial de versiones que nadie pidió.</summary>
    [BindProperty] public List<IFormFile> DocArchivos { get; set; } = [];
    [BindProperty] public int        DocCategoriaId { get; set; }
    [BindProperty] public string?    DocTitulo      { get; set; }
    [BindProperty] public string?    DocDescripcion { get; set; }

    /// <summary>Qué cambió respecto de la versión anterior. Se pide porque un historial sin
    /// motivo no explica nada seis meses después.</summary>
    [BindProperty] public string?    DocNotas       { get; set; }

    // ── Avance ──────────────────────────────────────────────────────────────
    /// <summary>
    /// A qué se imputa el reporte, en un solo selector: <c>""</c> avance general,
    /// <c>e:12</c> el entregable 12, <c>a:45</c> la actividad 45.
    ///
    /// <para>Un selector y no dos porque la actividad ya sabe de qué entregable cuelga: pedir los
    /// dos obligaría a mantenerlos coherentes en el navegador y el comando rechazaría la
    /// combinación imposible después de que el usuario llenó todo el formulario.</para>
    /// </summary>
    [BindProperty] public string?    AvanceDestino     { get; set; }
    [BindProperty] public string?    AvanceDescripcion { get; set; }

    /// <summary>Porcentaje de la actividad imputada. Null cuando el reporte no es de una actividad:
    /// el proyecto ya no tiene un porcentaje que declarar, lo calcula su árbol.</summary>
    [BindProperty] public int?       AvancePorcentaje  { get; set; }
    [BindProperty] public string?    AvanceBloqueo     { get; set; }
    [BindProperty] public IFormFile? AvanceArchivo     { get; set; }
    [BindProperty] public bool       AvanceCompletarEntregable { get; set; }
    [BindProperty] public int?       AvanceRiesgoId      { get; set; }
    [BindProperty] public string?    MotivoReapertura    { get; set; }

    /// <summary>
    /// Vuelve a la ficha dejando abierta la pestaña donde el usuario estaba trabajando.
    ///
    /// <para>Desde que la ficha se organiza en pestañas, un redirect pelado aterriza siempre en la
    /// primera: guardar un riesgo devolvía al usuario a la estructura, sin ver el resultado de lo
    /// que acababa de hacer. El ancla lo resuelve del lado del servidor, así que no depende de que
    /// el navegador recuerde nada ni de que el JS haya corrido.</para>
    ///
    /// <para>Sin ancla para lo que no pertenece a ninguna pestaña —cambiar de estado, reabrir—:
    /// esas acciones viven en el encabezado y devolver a la ficha entera es lo correcto.</para>
    /// </summary>
    private IActionResult VolverA(int id, string? ancla = null) =>
        RedirectToPage(pageName: null, pageHandler: null, routeValues: new { id }, fragment: ancla);

    /// <summary>
    /// Devuelve a ejecución un proyecto cerrado o cancelado.
    ///
    /// <para>Va por <c>Proyectos.Editar</c> y no por una clave propia: quien puede administrar el
    /// proyecto puede corregir su cierre. Lo que lo distingue de cualquier otro cambio de estado es
    /// el motivo obligatorio, que es lo que después explica en la bitácora por qué algo terminado
    /// volvió a estar vivo.</para>
    /// </summary>
    [Permission("Proyectos", AccionModulo.Editar, "Editar proyectos")]
    public async Task<IActionResult> OnPostReabrirAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new ReabrirProyectoCommand(id, MotivoReapertura ?? ""), ct);
            TempData["SuccessMsg"] = "Proyecto reabierto y devuelto a ejecución.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return VolverA(id);
    }

    // ── Reordenar ───────────────────────────────────────────────────────────
    /// <summary>Ids de los entregables en el orden en que quedaron en pantalla. Se manda la lista
    /// completa: el dominio rechaza un subconjunto (ver Proyecto.ReordenarEntregables).</summary>
    [BindProperty] public List<int> OrdenEntregables { get; set; } = [];

    /// <summary>Entregable cuyas actividades se están reordenando, y sus Ids en orden. Viaja uno
    /// por vez: se reordena dentro de un entregable, no el árbol entero.</summary>
    [BindProperty] public int       OrdenActividadesDe { get; set; }
    [BindProperty] public List<int> OrdenActividades   { get; set; } = [];

    // ── Corregir una entrada de la bitácora ─────────────────────────────────
    [BindProperty] public int     CorreccionAvanceId    { get; set; }
    [BindProperty] public string? CorreccionDescripcion { get; set; }
    [BindProperty] public string? CorreccionBloqueo     { get; set; }
    [BindProperty] public string? CorreccionDestino     { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        if (!await CargarAsync(id, ct)) return NotFound();

        Nombre          = Proyecto.Nombre;
        Objetivo        = Proyecto.Objetivo;
        ResponsableId   = Proyecto.ResponsableId;
        AreaId          = Proyecto.AreaId;
        UnidadId        = Proyecto.UnidadId;
        Prioridad       = Proyecto.Prioridad;
        Accion          = Proyecto.Accion;
        FechaInicioPlan = Proyecto.FechaInicioPlan;
        FechaFinPlan    = Proyecto.FechaFinPlan;

        return Page();
    }

    /// <summary>
    /// Descarga la estructura completa del proyecto en PDF.
    ///
    /// <para>No lleva permiso propio: reutiliza la carga de la ficha, y esa consulta ya está
    /// filtrada por alcance —quien no puede abrir el proyecto recibe el mismo NotFound que en
    /// pantalla—. Pedir una clave aparte cerraría más el documento que la página de la que sale,
    /// y no hay dato en el PDF que no esté ya a la vista de quien lo pide.</para>
    /// </summary>
    public async Task<IActionResult> OnGetPdfAsync(int id, CancellationToken ct)
    {
        if (!await CargarAsync(id, ct)) return NotFound();

        var dto = new ProyectoPdfDto(
            Proyecto, Cronograma, Avances, Interesados, Riesgos, Documentos, Vinculos, Auditoria,
            InstitucionNombre: Proyecto.InstitucionId,
            AreaNombre:        Alcance.Areas.FirstOrDefault(a => a.Id == Proyecto.AreaId)?.Nombre,
            UnidadNombre:      Alcance.Unidades.FirstOrDefault(u => u.Id == Proyecto.UnidadId)?.Nombre);

        var bytes  = proyectoPdf.Generar(dto);
        var nombre = $"{Proyecto.Codigo}_estructura_{DateTime.Now:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", nombre);
    }

    // ── Guardar ficha + estructura ──────────────────────────────────────────
    [Permission("Proyectos", AccionModulo.Editar, "Editar proyectos")]
    public async Task<IActionResult> OnPostGuardarAsync(int id, CancellationToken ct)
    {
        try
        {
            var usuarios    = await sender.Send(new GetUsuariosAsignablesQuery(), ct);
            var interesados = await sender.Send(new GetInteresadosProyectoQuery(id), ct);
            var actual      = await sender.Send(new GetProyectoQuery(id), ct)
                              ?? throw new NotFoundException(nameof(Proyecto), id);

            // El nombre del responsable se resuelve contra los interesados —que es de donde salen
            // ahora— y contra el padrón de usuarios. Si no está en ninguno se conserva el que ya
            // tenía la fila: es el caso de los responsables que vienen de la carga inicial, y
            // perderles el nombre por no encontrarlo en una lista sería borrar un dato bueno.
            string? NombreDe(Guid? uid, string? actualNombre) =>
                uid is null ? null
                : interesados.FirstOrDefault(i => i.UsuarioId == uid)?.Nombre
                  ?? usuarios.FirstOrDefault(u => u.Id == uid)?.Nombre
                  ?? actualNombre;

            var porId    = actual.Entregables.ToDictionary(e => e.Id);
            var actsPorId = actual.Entregables.SelectMany(e => e.Actividades).ToDictionary(a => a.Id);

            var entregables = Entregables.Select(e => new EntregableInput(
                e.Id,                        // 0 = fila nueva; con Id se actualiza en su lugar
                e.Nombre ?? "",
                e.Descripcion,
                e.FechaPlan,
                e.Estado,
                e.ResponsableId,
                NombreDe(e.ResponsableId, porId.TryGetValue(e.Id, out var prev) ? prev.Responsable : null),
                (e.Actividades ?? []).Select(a => new ActividadInput(
                    a.Id,
                    a.Nombre ?? "",
                    a.Descripcion,
                    a.FechaInicioPlan,
                    a.FechaFinPlan,
                    a.AvancePct,
                    a.Cancelada,
                    a.ResponsableId,
                    NombreDe(a.ResponsableId, actsPorId.TryGetValue(a.Id, out var pa) ? pa.Responsable : null),
                    a.Predecesoras))
                    .ToList())).ToList();

            await sender.Send(new ActualizarProyectoCommand(
                id,
                Nombre ?? "",
                Objetivo,
                AreaId,
                UnidadId,
                ResponsableId,
                usuarios.FirstOrDefault(u => u.Id == ResponsableId)?.Nombre ?? actual.Responsable,
                Prioridad,
                Accion,
                FechaInicioPlan,
                FechaFinPlan,
                entregables), ct);

            TempData["SuccessMsg"] = "Proyecto actualizado.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return VolverA(id, "estructura");
    }

    // ── Cambiar estado ──────────────────────────────────────────────────────
    [Permission("Proyectos", AccionModulo.Editar, "Editar proyectos")]
    public async Task<IActionResult> OnPostCambiarEstadoAsync(int id, EstadoProyecto nuevo, CancellationToken ct)
    {
        try
        {
            await sender.Send(new CambiarEstadoProyectoCommand(id, nuevo), ct);
            TempData["SuccessMsg"] = $"El proyecto pasó a «{nuevo}».";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return VolverA(id);
    }

    /// <summary>
    /// Parte el valor del selector de imputación en sus dos Ids.
    /// <c>e:12</c> → entregable 12; <c>a:45</c> → actividad 45; vacío → avance general.
    /// El entregable de una actividad lo resuelve el comando contra el árbol, no la vista.
    /// </summary>
    private static (int? Entregable, int? Actividad) Destino(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return (null, null);

        var partes = valor.Split(':', 2);
        if (partes.Length != 2 || !int.TryParse(partes[1], out var id) || id <= 0) return (null, null);

        return partes[0] switch
        {
            "e" => (id, null),
            "a" => (null, id),
            _   => (null, null)
        };
    }

    // ── Registrar avance ────────────────────────────────────────────────────
    // Clave propia: reportar avance no es editar la ficha. Un técnico puede tener
    // Proyectos.Avance.Crear sin poder tocar fechas ni alcance del proyecto.
    [Permission("Proyectos.Avance", AccionModulo.Crear, "Registrar avances de proyecto")]
    public async Task<IActionResult> OnPostRegistrarAvanceAsync(int id, CancellationToken ct)
    {
        try
        {
            string? nombre = null, url = null; long? tamano = null;

            if (AvanceArchivo is { Length: > 0 })
            {
                // Mismo almacenamiento y mismas validaciones de tamaño/extensión que tickets.
                var guardados = await AdjuntoStorage.GuardarAsync([AvanceArchivo], env, ct, carpeta: "proyectos");
                if (guardados.Count > 0)
                {
                    nombre = guardados[0].Nombre;
                    url    = guardados[0].Url;
                    tamano = guardados[0].Tamano;
                }
            }

            var (entregableId, actividadId) = Destino(AvanceDestino);

            // El porcentaje solo viaja si hay actividad a la cual referirlo: sin ella el número no
            // tiene dueño, y el comando lo rechazaría. Que el formulario lo descarte acá evita
            // pedirle al usuario que corrija algo que la propia pantalla ya sabía.
            var porcentaje = actividadId is null ? null : AvancePorcentaje;

            await sender.Send(new RegistrarAvanceCommand(
                id, AvanceDescripcion ?? "",
                entregableId, actividadId, porcentaje,
                AvanceBloqueo, nombre, url, tamano,
                CompletarEntregable: AvanceCompletarEntregable && entregableId is not null,
                RiesgoId: AvanceRiesgoId), ct);

            TempData["SuccessMsg"] =
                AvanceRiesgoId is not null ? "Avance registrado. El riesgo vinculado quedó como materializado."
                : porcentaje is not null   ? "Avance registrado y actividad actualizada."
                : AvanceCompletarEntregable && entregableId is not null ? "Avance registrado y entregable dado por cumplido."
                : "Avance registrado.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return VolverA(id, "bitacora");
    }

    // ── Reordenar entregables ───────────────────────────────────────────────
    // Handler propio en vez de colgarse del guardado de la ficha: mover un entregable es una acción
    // del dueño y no debería exigir que además tenga la ficha entera en un estado válido.
    [Permission("Proyectos", AccionModulo.Editar, "Editar proyectos")]
    public async Task<IActionResult> OnPostReordenarEntregablesAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new ReordenarEntregablesCommand(id, OrdenEntregables), ct);
            TempData["SuccessMsg"] = "Orden de los entregables actualizado.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return VolverA(id, "estructura");
    }

    // ── Reordenar actividades ───────────────────────────────────────────────
    [Permission("Proyectos", AccionModulo.Editar, "Editar proyectos")]
    public async Task<IActionResult> OnPostReordenarActividadesAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new ReordenarActividadesCommand(id, OrdenActividadesDe, OrdenActividades), ct);
            TempData["SuccessMsg"] = "Orden de las actividades actualizado.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return VolverA(id, "estructura");
    }

    // ── Corregir una entrada de la bitácora ─────────────────────────────────
    // Clave propia, igual que Crear: se puede querer que un técnico reporte avances sin poder
    // volver atrás a enmendar lo ya reportado.
    [Permission("Proyectos.Avance", AccionModulo.Editar, "Corregir avances de proyecto")]
    public async Task<IActionResult> OnPostActualizarAvanceAsync(int id, CancellationToken ct)
    {
        try
        {
            var (entregableId, actividadId) = Destino(CorreccionDestino);

            // La actividad viaja con su entregable: el comando lo exige y la vista lo sabe, porque
            // el selector se armó desde el árbol.
            if (actividadId is { } act && entregableId is null)
            {
                var dto = await sender.Send(new GetProyectoQuery(id), ct);
                entregableId = dto?.Entregables.FirstOrDefault(e => e.Actividades.Any(a => a.Id == act))?.Id;
            }

            await sender.Send(new ActualizarAvanceCommand(
                CorreccionAvanceId,
                CorreccionDescripcion ?? "",
                CorreccionBloqueo,
                entregableId,
                actividadId), ct);

            TempData["SuccessMsg"] = "Entrada de bitácora corregida.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return VolverA(id, "bitacora");
    }

    // ── Riesgos ─────────────────────────────────────────────────────────────
    // Bajo Proyectos.Editar y no con clave propia: administrar riesgos es gestionar el proyecto,
    // no reportar sobre él. Si algún día hace falta que un técnico registre riesgos sin tocar la
    // ficha, se separa en su clave y se concede aparte.
    [BindProperty] public int              RiesgoId          { get; set; }
    [BindProperty] public string?          RiesgoDescripcion { get; set; }
    [BindProperty] public CategoriaRiesgo  RiesgoCategoria   { get; set; }
    [BindProperty] public NivelCualitativo RiesgoProbabilidad{ get; set; } = NivelCualitativo.Media;
    [BindProperty] public NivelCualitativo RiesgoImpacto     { get; set; } = NivelCualitativo.Media;
    [BindProperty] public EstrategiaRiesgo RiesgoEstrategia  { get; set; } = EstrategiaRiesgo.Mitigar;
    [BindProperty] public string?          RiesgoMitigacion  { get; set; }
    [BindProperty] public Guid?            RiesgoResponsable { get; set; }
    [BindProperty] public DateOnly?        RiesgoRevision    { get; set; }
    [BindProperty] public EstadoRiesgo     RiesgoEstado      { get; set; }

    [Permission("Proyectos", AccionModulo.Editar, "Editar proyectos")]
    public async Task<IActionResult> OnPostGuardarRiesgoAsync(int id, CancellationToken ct)
    {
        try
        {
            var usuarios = await sender.Send(new GetUsuariosAsignablesQuery(), ct);
            var nombre   = usuarios.FirstOrDefault(u => u.Id == RiesgoResponsable)?.Nombre;

            if (RiesgoId > 0)
            {
                await sender.Send(new ActualizarRiesgoCommand(
                    RiesgoId, RiesgoDescripcion ?? "", RiesgoCategoria, RiesgoProbabilidad,
                    RiesgoImpacto, RiesgoEstrategia, RiesgoMitigacion, RiesgoResponsable,
                    nombre, RiesgoRevision), ct);
                TempData["SuccessMsg"] = "Riesgo actualizado.";
            }
            else
            {
                await sender.Send(new RegistrarRiesgoCommand(
                    id, RiesgoDescripcion ?? "", RiesgoCategoria, RiesgoProbabilidad,
                    RiesgoImpacto, RiesgoEstrategia, RiesgoMitigacion, RiesgoResponsable,
                    nombre, RiesgoRevision), ct);
                TempData["SuccessMsg"] = "Riesgo registrado.";
            }
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return VolverA(id, "riesgos");
    }

    [Permission("Proyectos", AccionModulo.Editar, "Editar proyectos")]
    public async Task<IActionResult> OnPostEstadoRiesgoAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new CambiarEstadoRiesgoCommand(RiesgoId, RiesgoEstado), ct);
            TempData["SuccessMsg"] = $"El riesgo pasó a «{RiesgoEstado}».";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return VolverA(id, "riesgos");
    }

    [Permission("Proyectos", AccionModulo.Editar, "Editar proyectos")]
    public async Task<IActionResult> OnPostEliminarRiesgoAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new EliminarRiesgoCommand(RiesgoId), ct);
            TempData["SuccessMsg"] = "Riesgo eliminado.";
        }
        catch (NotFoundException) { return NotFound(); }

        return VolverA(id, "riesgos");
    }

    // ── Interesados ─────────────────────────────────────────────────────────
    // El nombre y el correo ya no se atan: los pone el comando desde el usuario elegido.
    [BindProperty] public int              IntId         { get; set; }
    [BindProperty] public Guid             IntUsuarioId  { get; set; }
    [BindProperty] public RolInteresado    IntRol        { get; set; }
    [BindProperty] public NivelCualitativo IntInfluencia { get; set; } = NivelCualitativo.Media;
    [BindProperty] public string?          IntInstitucion{ get; set; }
    [BindProperty] public string?          IntCargo      { get; set; }
    [BindProperty] public string?          IntNotas      { get; set; }

    [Permission("Proyectos", AccionModulo.Editar, "Editar proyectos")]
    public async Task<IActionResult> OnPostGuardarInteresadoAsync(int id, CancellationToken ct)
    {
        try
        {
            if (IntId > 0)
            {
                // Al editar solo cambia el papel: la persona es la que es. Para cambiarla se
                // quita el registro y se agrega otro, porque eso además mueve quién tiene acceso.
                await sender.Send(new ActualizarInteresadoCommand(
                    IntId, IntRol, IntInfluencia, IntInstitucion, IntCargo, IntNotas), ct);
                TempData["SuccessMsg"] = "Interesado actualizado.";
            }
            else if (IntUsuarioId == Guid.Empty)
            {
                TempData["ErrorMsg"] = "Elija al usuario que va a quedar como interesado.";
            }
            else
            {
                await sender.Send(new AgregarInteresadoCommand(
                    id, IntUsuarioId, IntRol, IntInfluencia, IntInstitucion, IntCargo, IntNotas), ct);
                TempData["SuccessMsg"] =
                    "Interesado agregado. Ya puede ver el proyecto y quedar a cargo de entregables y actividades.";
            }
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return VolverA(id, "interesados");
    }

    [Permission("Proyectos", AccionModulo.Editar, "Editar proyectos")]
    public async Task<IActionResult> OnPostQuitarInteresadoAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new QuitarInteresadoCommand(IntId), ct);
            TempData["SuccessMsg"] = "Interesado quitado.";
        }
        catch (NotFoundException) { return NotFound(); }

        return VolverA(id, "interesados");
    }

    // ── Vínculos con reuniones y expedientes ────────────────────────────────
    // Van con Proyectos.Editar y no con una clave propia: vincular no expone nada de la reunión
    // ni del expediente —el destino sigue detrás de su propio filtro— y es una afirmación sobre
    // el proyecto, igual que cambiarle el objetivo.

    [Permission("Proyectos", AccionModulo.Editar, "Editar proyectos")]
    public async Task<IActionResult> OnPostVincularReunionAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new VincularReunionCommand(id, VinculoReunionId, VinculoNota), ct);
            TempData["SuccessMsg"] = "Reunión vinculada.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return VolverA(id, "vinculos");
    }

    [Permission("Proyectos", AccionModulo.Editar, "Editar proyectos")]
    public async Task<IActionResult> OnPostVincularExpedienteAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new VincularExpedienteCommand(id, VinculoExpedienteId, VinculoNota), ct);
            TempData["SuccessMsg"] = "Expediente vinculado.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return VolverA(id, "vinculos");
    }

    [Permission("Proyectos", AccionModulo.Editar, "Editar proyectos")]
    public async Task<IActionResult> OnPostQuitarVinculoReunionAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new QuitarVinculoReunionCommand(id, VinculoId), ct);
            TempData["SuccessMsg"] = "Reunión desvinculada.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return VolverA(id, "vinculos");
    }

    [Permission("Proyectos", AccionModulo.Editar, "Editar proyectos")]
    public async Task<IActionResult> OnPostQuitarVinculoExpedienteAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new QuitarVinculoExpedienteCommand(id, VinculoId), ct);
            TempData["SuccessMsg"] = "Expediente desvinculado.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return VolverA(id, "vinculos");
    }

    [Permission("Proyectos", AccionModulo.Editar, "Editar proyectos")]
    public async Task<IActionResult> OnPostVincularTicketAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new VincularTicketCommand(id, VinculoTicketId, VinculoNota), ct);
            TempData["SuccessMsg"] = "Ticket vinculado.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return VolverA(id, "vinculos");
    }

    [Permission("Proyectos", AccionModulo.Editar, "Editar proyectos")]
    public async Task<IActionResult> OnPostQuitarVinculoTicketAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new QuitarVinculoTicketCommand(id, VinculoId), ct);
            TempData["SuccessMsg"] = "Ticket desvinculado.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return VolverA(id, "vinculos");
    }

    // ── Eliminar ────────────────────────────────────────────────────────────
    [Permission("Proyectos", AccionModulo.Eliminar, "Eliminar proyectos")]
    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new EliminarProyectoCommand(id), ct);
            TempData["SuccessMsg"] = "Proyecto eliminado.";
        }
        catch (NotFoundException) { return NotFound(); }

        return RedirectToPage("/Proyectos/Index");
    }

    // ── Descarga de evidencia ───────────────────────────────────────────────
    /// <summary>
    /// La evidencia se sirve por acá y no por su ruta bajo /uploads: Program.cs publica esa
    /// carpeta como archivos estáticos <b>sin autorización</b>, así que un enlace directo
    /// entregaría el documento a cualquiera que lo tuviera. Acá pasa por [Authorize] y por
    /// Proyectos.Ver como cualquier otro handler de la página.
    /// </summary>

    // ── Repositorio documental ──────────────────────────────────────────────
    // Los cuatro POST comparten forma: guardar el archivo si lo hay, mandar el comando, y dejar
    // el resultado en TempData. La autorización de fondo no la hacen ellos: la hace el comando al
    // cargar el proyecto por su consulta filtrada. El atributo solo evita ofrecer lo que se va a
    // rechazar y da la clave que la administración de roles muestra.

    /// <summary>
    /// Da de alta uno o varios documentos en una sola pasada.
    ///
    /// <para><b>Cada archivo entra por su cuenta.</b> Un lote de diez donde el séptimo es un .exe
    /// no puede perder los otros nueve, así que no hay transacción que los abarque: se procesan
    /// uno a uno y al final se dice qué entró y qué no, nombrando cada archivo rechazado y su
    /// motivo. Un lote que falla en silencio es peor que no poder subir en lote.</para>
    ///
    /// <para><b>El título se usa siempre que se escriba</b>, sean uno o veinte archivos; si se deja
    /// vacío, cada documento toma el nombre del suyo. Una sola regla y ningún caso especial.</para>
    ///
    /// <para>La primera versión descartaba el título cuando el lote traía varios archivos, con el
    /// argumento de que un título no se reparte entre cinco documentos. Era un mal cambio: el
    /// formulario aceptaba el texto y lo tiraba, y quien lo escribió se enteraba al ver los
    /// registros nombrados como sus archivos. Si el resultado son cinco documentos con el mismo
    /// título, es lo que se pidió —y cada fila muestra su propio nombre de archivo debajo, así que
    /// siguen distinguiéndose—.</para>
    /// </summary>
    [Permission("Proyectos.Documentos", AccionModulo.Crear, "Subir documentos de proyecto")]
    public async Task<IActionResult> OnPostSubirDocumentoAsync(int id, CancellationToken ct)
    {
        var archivos = DocArchivos.Where(a => a is { Length: > 0 }).ToList();

        if (archivos.Count == 0)
        {
            TempData["ErrorMsg"] = "Elija al menos un archivo para subir.";
            return VolverA(id, "documentos");
        }

        var subidos  = 0;
        var fallidos = new List<string>();

        var tituloComun = string.IsNullOrWhiteSpace(DocTitulo) ? null : DocTitulo.Trim();
        var enLote      = archivos.Count > 1;

        foreach (var archivo in archivos)
        {
            try
            {
                var guardado = await DocumentosStorage.GuardarAsync(archivo, env, ct);

                var titulo = TituloDelDocumento(tituloComun, archivo.FileName, enLote);

                await sender.Send(new SubirDocumentoCommand(
                    id, DocCategoriaId, titulo, DocDescripcion,
                    guardado.Nombre, guardado.Url, guardado.Tamano, guardado.Sha256), ct);

                subidos++;
            }
            // Se atrapa por archivo, no por lote: el rechazo de uno no arrastra a los demás.
            catch (DomainException ex) { fallidos.Add($"«{archivo.FileName}»: {ex.Message}"); }
            catch (NotFoundException)  { return NotFound(); }
        }

        if (subidos > 0)
            TempData["SuccessMsg"] = subidos == 1
                ? "Documento agregado."
                : $"{subidos} documentos agregados.";

        if (fallidos.Count > 0)
            TempData["ErrorMsg"] = fallidos.Count == 1
                ? $"No se subió {fallidos[0]}"
                : $"No se subieron {fallidos.Count} archivos. " + string.Join(" ", fallidos);

        return VolverA(id, "documentos");
    }

    /// <summary>
    /// Cómo se llama cada documento del alta.
    ///
    /// <para>Un solo archivo: el título escrito, o el nombre del archivo si no se escribió ninguno.</para>
    ///
    /// <para>Varios: <c>«Título — nombre del archivo»</c>. Es la única forma que no pierde nada ni
    /// deja el listado con N filas idénticas. Las dos alternativas se probaron y fallaron: descartar
    /// el título hacía que el formulario aceptara texto y lo tirara, y repetirlo tal cual llenaba la
    /// biblioteca de documentos indistinguibles por su nombre.</para>
    /// </summary>
    private static string TituloDelDocumento(string? titulo, string archivo, bool enLote)
    {
        var delArchivo = TituloDesdeArchivo(archivo);

        if (titulo is null)   return delArchivo;
        if (!enLote)          return titulo;

        var compuesto = $"{titulo} — {delArchivo}";
        return compuesto.Length > DocumentoProyecto.MaxTitulo
            ? compuesto[..DocumentoProyecto.MaxTitulo]
            : compuesto;
    }

    /// <summary>El nombre del archivo sin su extensión, recortado al largo que admite el título.
    /// Si quedara vacío —un archivo llamado «.pdf»— se usa el nombre completo, porque el dominio
    /// exige título y quedarse sin uno perdería el archivo por un detalle del nombre.</summary>
    private static string TituloDesdeArchivo(string nombre)
    {
        var sinExt = Path.GetFileNameWithoutExtension(nombre).Trim();
        if (sinExt.Length == 0) sinExt = nombre.Trim();

        return sinExt.Length > DocumentoProyecto.MaxTitulo
            ? sinExt[..DocumentoProyecto.MaxTitulo]
            : sinExt;
    }

    [Permission("Proyectos.Documentos", AccionModulo.Crear, "Subir documentos de proyecto")]
    public async Task<IActionResult> OnPostSubirVersionAsync(int id, int documentoId, CancellationToken ct)
    {
        try
        {
            if (DocArchivo is null || DocArchivo.Length == 0)
                throw new DomainException("Elija el archivo de la versión nueva.");

            var guardado = await DocumentosStorage.GuardarAsync(DocArchivo, env, ct);

            var numero = await sender.Send(new SubirVersionDocumentoCommand(
                id, documentoId, guardado.Nombre, guardado.Url,
                guardado.Tamano, guardado.Sha256, DocNotas), ct);

            TempData["SuccessMsg"] = $"Versión {numero} agregada. La anterior sigue en el historial.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return VolverA(id, "documentos");
    }

    [Permission("Proyectos.Documentos", AccionModulo.Editar, "Editar la ficha de un documento")]
    public async Task<IActionResult> OnPostGuardarDocumentoAsync(int id, int documentoId, CancellationToken ct)
    {
        try
        {
            await sender.Send(new ActualizarDocumentoCommand(
                id, documentoId, DocCategoriaId, DocTitulo ?? "", DocDescripcion), ct);

            TempData["SuccessMsg"] = "Documento actualizado.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return VolverA(id, "documentos");
    }

    [Permission("Proyectos.Documentos", AccionModulo.Eliminar, "Archivar documentos de proyecto")]
    public async Task<IActionResult> OnPostArchivarDocumentoAsync(int id, int documentoId, CancellationToken ct)
    {
        try
        {
            await sender.Send(new EliminarDocumentoCommand(id, documentoId), ct);
            TempData["SuccessMsg"] = "Documento archivado. Sigue en la base, deja de listarse.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return VolverA(id, "documentos");
    }

    /// <summary>
    /// Sirve una versión del documento.
    ///
    /// <para>La consulta arranca en la tabla de versiones, que lleva su ancla al documento y por él
    /// al proyecto: pedir la versión de un documento ajeno devuelve null y acá se convierte en 404,
    /// sin comprobar nada a mano.</para>
    /// </summary>
    [Permission("Proyectos.Documentos", AccionModulo.Ver, "Ver y descargar documentos de proyecto")]
    public async Task<IActionResult> OnGetDocumentoAsync(int versionId, CancellationToken ct)
    {
        var meta = await sender.Send(new GetDescargaDocumentoQuery(versionId), ct);
        if (meta is null) return NotFound();

        var ruta = ArchivosProtegidos.Resolver(env, meta.ArchivoUrl);
        if (ruta is null)
        {
            TempData["ErrorMsg"] = "El archivo ya no está disponible en el servidor.";
            return VolverA(meta.ProyectoId, "documentos");
        }

        // Después de resolver la ruta y antes de servir: no se anota una descarga que no ocurrió
        // porque el archivo ya no estaba.
        await sender.Send(new RegistrarDescargaDocumentoCommand(versionId), ct);

        return PhysicalFile(ruta, ArchivosProtegidos.TipoContenido(meta.ArchivoNombre), meta.ArchivoNombre);
    }
    public async Task<IActionResult> OnGetEvidenciaAsync(int avanceId, CancellationToken ct)
    {
        var ev = await sender.Send(new GetEvidenciaAvanceQuery(avanceId), ct);
        if (ev is null) return NotFound();

        // La resolución de la ruta y la guarda contra salirse de la carpeta viven en
        // ArchivosProtegidos: este handler dejó de ser el único que sirve archivos con sesión.
        var ruta = ArchivosProtegidos.Resolver(env, ev.ArchivoUrl);
        if (ruta is null)
        {
            TempData["ErrorMsg"] = "El archivo de evidencia ya no está disponible en el servidor.";
            return RedirectToPage(new { id = ev.ProyectoId });
        }

        return PhysicalFile(ruta, ArchivosProtegidos.TipoContenido(ev.ArchivoNombre), ev.ArchivoNombre);
    }

    private async Task<bool> CargarAsync(int id, CancellationToken ct)
    {
        var dto = await sender.Send(new GetProyectoQuery(id), ct);
        if (dto is null) return false;

        Proyecto  = dto;
        Avances   = await sender.Send(new GetAvancesProyectoQuery(id), ct);

        DiasSinReportar = Avances.Count == 0
            ? null
            : (int)(DateTime.UtcNow - Avances.Max(a => a.Fecha)).TotalDays;

        Cronograma  = CronogramaProyecto.Construir(dto, DateOnly.FromDateTime(DateTime.UtcNow));
        Auditoria   = await sender.Send(new GetBitacoraProyectoQuery(id), ct);
        Riesgos     = await sender.Send(new GetRiesgosProyectoQuery(id), ct);
        Interesados = await sender.Send(new GetInteresadosProyectoQuery(id), ct);
        Vinculos    = await sender.Send(new GetVinculosProyectoQuery(id), ct);

        var vinculables = await sender.Send(new GetVinculablesQuery(id), ct);
        ReunionesVinculables   = vinculables.Reuniones;
        ExpedientesVinculables = vinculables.Expedientes;
        TicketsVinculables     = vinculables.Tickets;

        var mutable = HttpContext.CanMutate() && !dto.EstaCerrado;
        PuedeEditar         = mutable && await acceso.PuedeEditarAsync("Proyectos", ct);
        PuedeEliminar       = HttpContext.CanMutate() && await acceso.PuedeEliminarAsync("Proyectos", ct);

        // Reabrir es la excepción a «cerrado = solo lectura»: es justamente la acción que saca al
        // proyecto de ese estado, así que no puede depender de PuedeEditar, que ya lo excluye.
        PuedeReabrir        = HttpContext.CanMutate() && dto.EstaCerrado
                              && await acceso.PuedeEditarAsync("Proyectos", ct);
        PuedeReportarAvance = mutable && await acceso.PuedeClaveAsync("Proyectos.Avance.Crear", ct);

        EsPropietario         = dto.ResponsableId is { } resp && currentUser.UserId == resp;

        // Reordenar sí admite al administrador; corregir la bitácora no. Por eso la condición del
        // orden va aparte de EsPropietario en vez de ensancharlo: ensancharlo habría abierto de
        // paso la corrección de la bitácora, que es justo lo que no se quiso abrir.
        var mandaEnElOrden    = EsPropietario || acceso.EsAdministrador;
        PuedeReordenar        = PuedeEditar && mandaEnElOrden && dto.Entregables.Count > 1;
        PuedeReordenarActividades = PuedeEditar && mandaEnElOrden;
        PuedeCorregirBitacora = mutable && EsPropietario
                                && await acceso.PuedeClaveAsync("Proyectos.Avance.Editar", ct);

        if (PuedeEditar)
            Alcance = await sender.Send(new GetAlcanceOpcionesQuery(), ct);

        if (PuedeEditar || PuedeReportarAvance)
            Usuarios = await sender.Send(new GetUsuariosAsignablesQuery(), ct);

        // Repositorio documental. Se consulta siempre que se pueda ver: la documentación de un
        // proyecto cerrado se sigue leyendo, solo deja de poder tocarse.
        PuedeVerDocumentos      = await acceso.PuedeClaveAsync("Proyectos.Documentos.Ver", ct);
        PuedeSubirDocumentos    = mutable && await acceso.PuedeClaveAsync("Proyectos.Documentos.Crear", ct);
        PuedeEditarDocumentos   = mutable && await acceso.PuedeClaveAsync("Proyectos.Documentos.Editar", ct);
        PuedeArchivarDocumentos = mutable && await acceso.PuedeClaveAsync("Proyectos.Documentos.Eliminar", ct);

        if (PuedeVerDocumentos)
        {
            Documentos = await sender.Send(new GetDocumentosProyectoQuery(id), ct);
            if (PuedeSubirDocumentos || PuedeEditarDocumentos)
                Categorias = await sender.Send(new GetCategoriasDocumentoQuery(), ct);
        }

        return true;
    }
}
