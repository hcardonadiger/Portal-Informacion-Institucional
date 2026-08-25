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
    IWebHostEnvironment env) : PageModel
{
    public ProyectoDetailDto Proyecto { get; private set; } = default!;
    public IReadOnlyList<AvanceProyectoDto> Avances { get; private set; } = [];
    public IReadOnlyList<BitacoraProyectoDto> Auditoria { get; private set; } = [];
    public IReadOnlyList<RiesgoProyectoDto> Riesgos { get; private set; } = [];
    public IReadOnlyList<InteresadoProyectoDto> Interesados { get; private set; } = [];
    public AlcanceOpcionesDto Alcance { get; private set; } = new([], []);
    public IReadOnlyList<UsuarioAsignableDto> Usuarios { get; private set; } = [];

    public bool PuedeEditar         { get; private set; }
    public bool PuedeReabrir        { get; private set; }
    public bool PuedeEliminar       { get; private set; }
    public bool PuedeReportarAvance { get; private set; }

    /// <summary>El usuario en sesión es el responsable del proyecto. Sin bypass de administrador:
    /// ver <c>PropiedadProyecto</c> en la capa de aplicación, que es quien realmente lo exige.
    /// Acá solo decide si se pintan los controles, para no ofrecer lo que el comando va a rechazar.</summary>
    public bool EsPropietario { get; private set; }

    /// <summary>Reordenar toca el cronograma: exige el permiso de edición y además ser el dueño.</summary>
    public bool PuedeReordenar { get; private set; }

    /// <summary>Corregir la bitácora tiene clave propia — se puede reportar sin poder enmendar.</summary>
    public bool PuedeCorregirBitacora { get; private set; }

    /// <summary>Una fila de la tabla de hitos tal como viaja en el formulario.</summary>
    public sealed class HitoForm
    {
        /// <summary>Id del hito existente; 0 en una fila recién agregada. Sin esto el comando
        /// tendría que borrar y recrear, y eso desimputa los avances de la bitácora.</summary>
        public int        Id            { get; set; }
        public string?    Nombre        { get; set; }
        public string?    Descripcion   { get; set; }
        public DateOnly?  FechaPlan     { get; set; }
        public DateOnly?  FechaReal     { get; set; }
        public EstadoHito Estado        { get; set; } = EstadoHito.Pendiente;
        public Guid?      ResponsableId { get; set; }
    }

    // ── Ficha ───────────────────────────────────────────────────────────────
    [BindProperty] public string?           Nombre          { get; set; }
    [BindProperty] public string?           Objetivo        { get; set; }
    [BindProperty] public Guid?             ResponsableId   { get; set; }
    [BindProperty] public string?           AreaId          { get; set; }
    [BindProperty] public string?           UnidadId        { get; set; }
    [BindProperty] public PrioridadProyecto Prioridad       { get; set; }
    [BindProperty] public DateOnly?         FechaInicioPlan { get; set; }
    [BindProperty] public DateOnly?         FechaFinPlan    { get; set; }
    [BindProperty] public List<HitoForm>    Hitos           { get; set; } = [];

    // ── Avance ──────────────────────────────────────────────────────────────
    [BindProperty] public string?    AvanceDescripcion { get; set; }
    [BindProperty] public int        AvancePorcentaje  { get; set; }
    [BindProperty] public int?       AvanceHitoId      { get; set; }
    [BindProperty] public string?    AvanceBloqueo     { get; set; }
    [BindProperty] public IFormFile? AvanceArchivo     { get; set; }
    [BindProperty] public bool       AvanceCompletarHito { get; set; }
    [BindProperty] public int?       AvanceRiesgoId      { get; set; }
    [BindProperty] public string?    MotivoReapertura    { get; set; }

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

        return RedirectToPage(new { id });
    }

    // ── Reordenar hitos ─────────────────────────────────────────────────────
    /// <summary>Ids de los hitos en el orden en que quedaron en pantalla. Se manda la lista
    /// completa: el dominio rechaza un subconjunto (ver Proyecto.ReordenarHitos).</summary>
    [BindProperty] public List<int> OrdenHitos { get; set; } = [];

    // ── Corregir una entrada de la bitácora ─────────────────────────────────
    [BindProperty] public int     CorreccionAvanceId    { get; set; }
    [BindProperty] public string? CorreccionDescripcion { get; set; }
    [BindProperty] public string? CorreccionBloqueo     { get; set; }
    [BindProperty] public int?    CorreccionHitoId      { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        if (!await CargarAsync(id, ct)) return NotFound();

        Nombre          = Proyecto.Nombre;
        Objetivo        = Proyecto.Objetivo;
        ResponsableId   = Proyecto.ResponsableId;
        AreaId          = Proyecto.AreaId;
        UnidadId        = Proyecto.UnidadId;
        Prioridad       = Proyecto.Prioridad;
        FechaInicioPlan = Proyecto.FechaInicioPlan;
        FechaFinPlan    = Proyecto.FechaFinPlan;

        return Page();
    }

    // ── Guardar ficha + hitos ───────────────────────────────────────────────
    [Permission("Proyectos", AccionModulo.Editar, "Editar proyectos")]
    public async Task<IActionResult> OnPostGuardarAsync(int id, CancellationToken ct)
    {
        var usuarios = await sender.Send(new GetUsuariosAsignablesQuery(), ct);
        string? NombreDe(Guid? uid) => usuarios.FirstOrDefault(u => u.Id == uid)?.Nombre;

        try
        {
            await sender.Send(new ActualizarProyectoCommand(
                id,
                Nombre ?? "",
                Objetivo,
                AreaId,
                UnidadId,
                ResponsableId,
                NombreDe(ResponsableId),
                Prioridad,
                FechaInicioPlan,
                FechaFinPlan,
                Hitos.Select(h => new HitoInput(
                    h.Id,                    // 0 = fila nueva; con Id se actualiza en su lugar
                    h.Nombre ?? "",
                    h.Descripcion,
                    h.FechaPlan,
                    h.FechaReal,
                    h.Estado,
                    h.ResponsableId,
                    NombreDe(h.ResponsableId))).ToList()), ct);

            TempData["SuccessMsg"] = "Proyecto actualizado.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return RedirectToPage(new { id });
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

        return RedirectToPage(new { id });
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

            await sender.Send(new RegistrarAvanceCommand(
                id, AvanceDescripcion ?? "", AvancePorcentaje,
                AvanceHitoId, AvanceBloqueo, nombre, url, tamano,
                CompletarHito: AvanceCompletarHito && AvanceHitoId is not null,
                RiesgoId: AvanceRiesgoId), ct);

            TempData["SuccessMsg"] =
                AvanceRiesgoId is not null       ? "Avance registrado. El riesgo vinculado quedó como materializado."
                : AvanceCompletarHito && AvanceHitoId is not null ? "Avance registrado y hito dado por cumplido."
                : "Avance registrado.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return RedirectToPage(new { id });
    }

    // ── Reordenar hitos ─────────────────────────────────────────────────────
    // Handler propio en vez de colgarse del guardado de la ficha: mover un hito es una acción
    // del dueño y no debería exigir que además tenga la ficha entera en un estado válido.
    [Permission("Proyectos", AccionModulo.Editar, "Editar proyectos")]
    public async Task<IActionResult> OnPostReordenarHitosAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new ReordenarHitosCommand(id, OrdenHitos), ct);
            TempData["SuccessMsg"] = "Orden de los hitos actualizado.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return RedirectToPage(new { id });
    }

    // ── Corregir una entrada de la bitácora ─────────────────────────────────
    // Clave propia, igual que Crear: se puede querer que un técnico reporte avances sin poder
    // volver atrás a enmendar lo ya reportado.
    [Permission("Proyectos.Avance", AccionModulo.Editar, "Corregir avances de proyecto")]
    public async Task<IActionResult> OnPostActualizarAvanceAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new ActualizarAvanceCommand(
                CorreccionAvanceId,
                CorreccionDescripcion ?? "",
                CorreccionBloqueo,
                CorreccionHitoId), ct);

            TempData["SuccessMsg"] = "Entrada de bitácora corregida.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return RedirectToPage(new { id });
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

        return RedirectToPage(new { id });
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

        return RedirectToPage(new { id });
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

        return RedirectToPage(new { id });
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
                TempData["SuccessMsg"] = "Interesado agregado. Ya puede ver el proyecto.";
            }
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return RedirectToPage(new { id });
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

        return RedirectToPage(new { id });
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
    public async Task<IActionResult> OnGetEvidenciaAsync(int avanceId, CancellationToken ct)
    {
        var ev = await sender.Send(new GetEvidenciaAvanceQuery(avanceId), ct);
        if (ev is null) return NotFound();

        var raiz = Path.GetFullPath(Path.Combine(env.ContentRootPath, "App_Data", "uploads"));
        var rel  = ev.ArchivoUrl.TrimStart('/');
        if (rel.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            rel = rel["uploads/".Length..];

        var ruta = Path.GetFullPath(Path.Combine(raiz, rel.Replace('/', Path.DirectorySeparatorChar)));

        // Cinturón y tirantes: la ruta la generamos nosotros, pero si alguna vez entra por otro
        // camino, que no se pueda salir de la carpeta de subidas.
        if (!ruta.StartsWith(raiz, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(ruta))
        {
            TempData["ErrorMsg"] = "El archivo de evidencia ya no está disponible en el servidor.";
            return RedirectToPage(new { id = ev.ProyectoId });
        }

        return PhysicalFile(ruta, "application/octet-stream", ev.ArchivoNombre);
    }

    private async Task<bool> CargarAsync(int id, CancellationToken ct)
    {
        var dto = await sender.Send(new GetProyectoQuery(id), ct);
        if (dto is null) return false;

        Proyecto  = dto;
        Avances   = await sender.Send(new GetAvancesProyectoQuery(id), ct);
        Auditoria   = await sender.Send(new GetBitacoraProyectoQuery(id), ct);
        Riesgos     = await sender.Send(new GetRiesgosProyectoQuery(id), ct);
        Interesados = await sender.Send(new GetInteresadosProyectoQuery(id), ct);

        var mutable = HttpContext.CanMutate() && !dto.EstaCerrado;
        PuedeEditar         = mutable && await acceso.PuedeEditarAsync("Proyectos", ct);
        PuedeEliminar       = HttpContext.CanMutate() && await acceso.PuedeEliminarAsync("Proyectos", ct);

        // Reabrir es la excepción a «cerrado = solo lectura»: es justamente la acción que saca al
        // proyecto de ese estado, así que no puede depender de PuedeEditar, que ya lo excluye.
        PuedeReabrir        = HttpContext.CanMutate() && dto.EstaCerrado
                              && await acceso.PuedeEditarAsync("Proyectos", ct);
        PuedeReportarAvance = mutable && await acceso.PuedeClaveAsync("Proyectos.Avance.Crear", ct);

        EsPropietario         = dto.ResponsableId is { } resp && currentUser.UserId == resp;
        PuedeReordenar        = PuedeEditar && EsPropietario && dto.Hitos.Count > 1;
        PuedeCorregirBitacora = mutable && EsPropietario
                                && await acceso.PuedeClaveAsync("Proyectos.Avance.Editar", ct);

        if (PuedeEditar)
            Alcance = await sender.Send(new GetAlcanceOpcionesQuery(), ct);

        if (PuedeEditar || PuedeReportarAvance)
            Usuarios = await sender.Send(new GetUsuariosAsignablesQuery(), ct);

        return true;
    }
}
