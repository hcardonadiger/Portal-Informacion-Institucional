namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Proyecto interno de DIGER (digitalización, PKI, conectividad, BIEN…). No es un plan de
/// racionalización de trámites — eso es <see cref="PlanTrabajo"/>, que sí pertenece a una
/// institución y cuelga sus metas de expedientes.
///
/// <para><b>Estructura de desglose</b> (2026-08-25): el proyecto se descompone en
/// <see cref="EntregableProyecto">entregables</see> y cada entregable en
/// <see cref="ActividadProyecto">actividades</see>, como la EDT del PMI. Antes era una lista plana
/// de hitos; los hitos existentes <b>son</b> los entregables de hoy — se conservaron con su Id para
/// no desimputar la bitácora que los referencia.</para>
///
/// <para><b>Visibilidad</b>: lleva filtro de alcance como el resto del portal, anclado en
/// <see cref="InstitucionId"/>. Antes no lo tenía —los proyectos no cargaban institución— y eso
/// dejaba el portafolio completo a la vista de cualquiera con <c>Proyectos.Ver</c>, incluidos los
/// usuarios de instituciones externas con rol Empleado. Se corrigió el 2026-08-23 agregando el
/// anclaje y rellenando los proyectos existentes con DIGER, que es quien los ejecuta aunque traten
/// sobre otra institución.</para>
///
/// <para>Con una excepción propia de este agregado: <b>el responsable siempre ve su proyecto</b>,
/// caiga o no dentro de su alcance. Sin eso alguien puede quedar como responsable de un proyecto
/// que no puede abrir, y las acciones que le reservamos —reordenar la estructura, corregir
/// bitácora— serían inalcanzables para la única persona autorizada a ejecutarlas.</para>
/// </summary>
public sealed class Proyecto : BaseAuditableEntity, ISoftDeletable
{
    public bool IsDeleted { get; set; }

    public string  Codigo   { get; private set; } = default!;
    public string  Nombre   { get; set; } = default!;
    public string? Objetivo { get; set; }

    /// <summary>Institución que ejecuta el proyecto — el ancla del filtro de alcance. Para el
    /// portafolio interno es siempre DIGER, aunque el proyecto trate sobre otra institución:
    /// «SOL — CONSUCOOP» lo ejecuta DIGER, no CONSUCOOP.</summary>
    public string? InstitucionId { get; set; }

    /// <summary>Área de DIGER que lo ejecuta. Opcional: null = transversal, lo ve toda la
    /// institución.</summary>
    public string? AreaId { get; set; }

    /// <summary>Unidad dentro del área. Opcional, misma lógica que <see cref="AreaId"/>.</summary>
    public string? UnidadId { get; set; }

    /// <summary>
    /// Quién responde por el proyecto. <b>No se elige entre los interesados</b>, a diferencia de los
    /// responsables de entregables y actividades: sale del padrón completo de usuarios asignables.
    /// Es deliberado — este campo gobierna la guarda de propiedad y una rama del filtro de alcance,
    /// y atarlo al registro de interesados dejaría un proyecto recién creado sin poder nombrar a
    /// nadie hasta que alguien poblara esa lista.
    /// </summary>
    public Guid?   ResponsableId { get; set; }
    /// <summary>Snapshot del nombre del responsable, para el listado y el histórico
    /// (mismo criterio que <see cref="MetaTramite.Responsable"/>).</summary>
    public string? Responsable   { get; set; }

    public PrioridadProyecto Prioridad { get; set; } = PrioridadProyecto.Media;

    /// <summary>Solo lo mueve <see cref="CambiarEstado"/>, que valida la transición.</summary>
    public EstadoProyecto Estado { get; private set; } = EstadoProyecto.Planificado;

    public DateOnly? FechaInicioPlan { get; set; }
    public DateOnly? FechaFinPlan    { get; set; }
    public DateOnly? FechaInicioReal { get; set; }
    public DateOnly? FechaFinReal    { get; set; }

    /// <summary>
    /// Avance del proyecto, <b>calculado</b> desde la estructura: promedio de sus entregables, que
    /// a su vez promedian sus actividades. Ver <see cref="RecalcularAvance"/>.
    ///
    /// <para>Hasta el 2026-08-25 lo declaraba el responsable al reportar un avance. Ya no: el
    /// número sube porque una actividad se movió, no porque alguien lo escribiera. Se conserva
    /// almacenado —y no calculado en cada consulta— porque el listado y el tablero lo leen para
    /// decenas de proyectos a la vez y no vale una agregación por fila.</para>
    /// </summary>
    public int AvancePct { get; private set; }

    private readonly List<EntregableProyecto> _entregables = [];
    public IReadOnlyCollection<EntregableProyecto> Entregables => _entregables.AsReadOnly();

    private Proyecto() { }   // EF

    public static Proyecto Crear(string codigo, string nombre, string? objetivo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        var limpio = (nombre ?? "").Trim();
        if (limpio.Length == 0)
            throw new DomainException("El proyecto necesita un nombre.");

        return new Proyecto
        {
            Codigo   = codigo.Trim().ToUpperInvariant(),
            Nombre   = limpio,
            Objetivo = string.IsNullOrWhiteSpace(objetivo) ? null : objetivo.Trim()
        };
    }

    // ── Máquina de estados ────────────────────────────────────────
    // Un proyecto no avanza en línea recta como un expediente: puede suspenderse y retomarse,
    // y puede cancelarse desde cualquier punto abierto. Por eso las transiciones se declaran
    // en vez de derivarse del orden del enum.
    private static readonly Dictionary<EstadoProyecto, EstadoProyecto[]> Permitidas = new()
    {
        [EstadoProyecto.Planificado] = [EstadoProyecto.EnEjecucion, EstadoProyecto.Cancelado],
        [EstadoProyecto.EnEjecucion] = [EstadoProyecto.Suspendido, EstadoProyecto.Cerrado, EstadoProyecto.Cancelado],
        [EstadoProyecto.Suspendido]  = [EstadoProyecto.EnEjecucion, EstadoProyecto.Cancelado],
        [EstadoProyecto.Cerrado]     = [],
        [EstadoProyecto.Cancelado]   = []
    };

    public bool PuedePasarA(EstadoProyecto nuevo) => Permitidas[Estado].Contains(nuevo);

    public void CambiarEstado(EstadoProyecto nuevo, string actor)
    {
        if (nuevo == Estado) return;   // no-op, no genera evento

        if (!PuedePasarA(nuevo))
            throw new DomainException(
                Permitidas[Estado].Length == 0
                    ? $"El proyecto está «{Estado}» y ya no admite cambios de estado."
                    : $"No se puede pasar de «{Estado}» a «{nuevo}».");

        var anterior = Estado;
        Estado = nuevo;

        // Las fechas reales las marca la propia transición: son el hecho, no un campo suelto
        // que alguien tenga que acordarse de llenar.
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        if (nuevo == EstadoProyecto.EnEjecucion) FechaInicioReal ??= hoy;
        if (nuevo is EstadoProyecto.Cerrado or EstadoProyecto.Cancelado) FechaFinReal ??= hoy;

        AddDomainEvent(new ProyectoEstadoCambiadoEvent(Id, Codigo, anterior.ToString(), nuevo.ToString(), actor));
    }

    /// <summary>
    /// Devuelve a ejecución un proyecto cerrado o cancelado.
    ///
    /// <para>Va por fuera de <see cref="CambiarEstado"/> y de la tabla de transiciones a propósito.
    /// Si se declarara como una transición más, el selector de estado la ofrecería junto al resto y
    /// reabrir sería un clic indistinguible de suspender — cuando en realidad revierte un cierre
    /// que alguien firmó. Acá exige un motivo, y ese motivo es lo que después explica en la
    /// bitácora por qué un proyecto terminado volvió a estar vivo.</para>
    ///
    /// <para>Limpia <see cref="FechaFinReal"/>: si el proyecto sigue, no terminó. Dejarla puesta
    /// haría que el listado mostrara una fecha de cierre para algo en ejecución, que es justo la
    /// inconsistencia que ya tiene el portafolio en un caso.</para>
    /// </summary>
    public void Reabrir(string motivo, string actor)
    {
        if (Estado is not (EstadoProyecto.Cerrado or EstadoProyecto.Cancelado))
            throw new DomainException($"El proyecto está «{Estado}»: solo se reabre lo que se cerró o se canceló.");

        var limpio = (motivo ?? "").Trim();
        if (limpio.Length == 0)
            throw new DomainException("Hay que decir por qué se reabre el proyecto.");
        if (limpio.Length > MaxMotivoReapertura)
            throw new DomainException($"El motivo no puede superar los {MaxMotivoReapertura} caracteres.");

        var anterior = Estado;
        Estado       = EstadoProyecto.EnEjecucion;
        FechaFinReal = null;

        AddDomainEvent(new ProyectoEstadoCambiadoEvent(
            Id, Codigo, anterior.ToString(), Estado.ToString(), actor));
    }

    public const int MaxMotivoReapertura = 500;

    // ── Avance calculado ──────────────────────────────────────────
    /// <summary>
    /// Recalcula <see cref="AvancePct"/> como el promedio simple de los entregables vigentes.
    ///
    /// <para><b>Recibe los entregables en vez de leer la navegación.</b> Si la colección no se
    /// cargó, leerla devolvería vacío y el proyecto perdería su avance en silencio — un
    /// <c>SaveChanges</c> sobre un proyecto traído sin <c>Include</c> bastaría para dejar todo en
    /// cero. Exigirlos por parámetro obliga a que quien llama los tenga de verdad; en la capa de
    /// aplicación lo centraliza <c>ProyectoConEstructura</c>.</para>
    ///
    /// <para><b>Promedio simple, sin pesos.</b> Un entregable de dos semanas cuenta lo mismo que uno
    /// de seis meses. Es la convención que se eligió por no tener de dónde sacar los pesos: nadie
    /// está estimando esfuerzo por entregable, y un peso inventado se leería como un dato. Si algún
    /// día se estiman, este es el único método que hay que cambiar.</para>
    ///
    /// <para>Los entregables cancelados no cuentan — ni a favor ni en contra, salen del promedio.
    /// Un proyecto sin entregables vigentes queda en 0: no hay estructura contra la cual medir, y
    /// ese cero es justamente la señal de que falta cargarla.</para>
    /// </summary>
    public void RecalcularAvance(IEnumerable<EntregableProyecto> entregables)
    {
        var vigentes = entregables.Where(e => e.Estado != EstadoEntregable.Cancelado).ToList();
        AvancePct = vigentes.Count == 0
            ? 0
            : (int)Math.Round(vigentes.Average(e => (double)e.AvanceCalculado), MidpointRounding.AwayFromZero);
    }

    // ── Entregables ───────────────────────────────────────────────
    /// <summary>
    /// Vacía la lista de entregables.
    ///
    /// <para><b>Cuidado: esto borra filas.</b> Cada entregable recreado después nace con un Id
    /// nuevo, y la FK de <see cref="AvanceProyecto.EntregableId"/> está en SetNull, así que todo
    /// avance imputado a un entregable borrado se desimputa en silencio — y sus actividades se van
    /// en cascada. Por eso el editor <b>no reemplaza en bloque</b>: reconcilia por Id y solo llama a
    /// <see cref="QuitarEntregable"/> para los que el usuario realmente quitó. Este método queda
    /// para ese caso y para borrados deliberados.</para>
    /// </summary>
    public void LimpiarEntregables() => _entregables.Clear();

    public void Agregar(EntregableProyecto entregable) => _entregables.Add(entregable);

    /// <summary>Quita un entregable del proyecto, con sus actividades. Los avances imputados a él
    /// quedan con <c>EntregableId</c> nulo: la entrada de bitácora no se pierde, deja de estar
    /// imputada.</summary>
    public void QuitarEntregable(EntregableProyecto entregable) => _entregables.Remove(entregable);

    /// <summary>Siguiente número de orden libre, para los entregables que se agregan desde la
    /// ficha: van al final y no alteran la posición de los que ya estaban.</summary>
    public int SiguienteOrden() => _entregables.Count == 0 ? 1 : _entregables.Max(e => e.Orden) + 1;

    /// <summary>
    /// Reordena los entregables según la secuencia de Ids recibida, renumerando
    /// <see cref="EntregableProyecto.Orden"/> de 1 en adelante.
    ///
    /// <para>Exige la lista <b>completa</b> de entregables del proyecto, no un subconjunto:
    /// reordenar con una parte dejaría a los ausentes con un Orden arbitrario respecto de los
    /// movidos, y el cronograma pasaría a mentir sin que nadie lo note. Si la lista no calza
    /// exactamente con los entregables vigentes —porque alguien agregó o borró uno en otra
    /// pestaña— se rechaza el reordenamiento en vez de aplicarlo a medias.</para>
    /// </summary>
    public void ReordenarEntregables(IReadOnlyList<int> idsEnOrden)
    {
        if (idsEnOrden is null || idsEnOrden.Count == 0)
            throw new DomainException("No se recibió el orden de los entregables.");

        if (idsEnOrden.Distinct().Count() != idsEnOrden.Count)
            throw new DomainException("El orden recibido trae entregables repetidos.");

        if (!_entregables.Select(e => e.Id).ToHashSet().SetEquals(idsEnOrden))
            throw new DomainException(
                "El orden recibido no corresponde a los entregables actuales del proyecto. " +
                "Recargue la página y vuelva a intentarlo.");

        var porId = _entregables.ToDictionary(e => e.Id);
        var orden = 0;
        foreach (var id in idsEnOrden)
            porId[id].Orden = ++orden;
    }
}

/// <summary>
/// Entregable del proyecto: el nivel intermedio de la EDT, entre el proyecto y las actividades.
/// Es lo que antes se llamaba hito, con el mismo Id y la misma fila — solo cambió el nombre y le
/// colgaron actividades.
///
/// <para>Su avance <b>no se teclea</b>: lo promedian sus actividades (ver
/// <see cref="AvanceCalculado"/>). Lo que sí se fija a mano es su fecha comprometida de entrega,
/// que es contra lo que se mide el atraso.</para>
/// </summary>
public sealed class EntregableProyecto : BaseAuditableEntity
{
    public int     ProyectoId  { get; set; }
    public int     Orden       { get; set; }
    public string  Nombre      { get; private set; } = default!;
    public string? Descripcion { get; private set; }

    /// <summary>Fecha comprometida de entrega. Es la línea base del entregable: contra ella se
    /// calcula el atraso, no contra las fechas de sus actividades.</summary>
    public DateOnly? FechaPlan { get; private set; }

    /// <summary>Cuándo se entregó de verdad. No se teclea: la fija <see cref="Completar"/>, igual
    /// que las fechas reales del proyecto las fija su cambio de estado.</summary>
    public DateOnly? FechaReal { get; private set; }

    public EstadoEntregable Estado { get; private set; } = EstadoEntregable.Pendiente;

    /// <summary>Quién responde por el entregable. Es el <c>UsuarioId</c> de un interesado del
    /// proyecto — no un usuario cualquiera del portal. Se guarda el UsuarioId y no el Id del
    /// registro de interesado para que quitar a alguien de esa lista no arrastre la asignación por
    /// una FK; quien exige que sea interesado es <c>ResponsablesProyecto</c>, en la capa de
    /// aplicación.</summary>
    public Guid?   ResponsableId { get; private set; }
    public string? Responsable   { get; private set; }

    private readonly List<ActividadProyecto> _actividades = [];
    public IReadOnlyCollection<ActividadProyecto> Actividades => _actividades.AsReadOnly();

    private EntregableProyecto() { }   // EF

    public static EntregableProyecto Crear(string nombre, int orden)
    {
        var e = new EntregableProyecto { Orden = orden };
        e.Definir(nombre, null, null, null, null);
        return e;
    }

    /// <summary>
    /// Fija los datos editables del entregable. El estado y la fecha real quedan fuera: los mueven
    /// <see cref="CambiarEstado"/> y <see cref="Completar"/>.
    /// </summary>
    /// <returns><c>true</c> si algo cambió — lo usa el resumen de bitácora.</returns>
    public bool Definir(
        string    nombre,
        string?   descripcion,
        DateOnly? fechaPlan,
        Guid?     responsableId,
        string?   responsable)
    {
        var limpio = (nombre ?? "").Trim();
        if (limpio.Length == 0)
            throw new DomainException("El entregable necesita un nombre.");
        if (limpio.Length > MaxNombre)
            throw new DomainException($"El nombre del entregable no puede superar los {MaxNombre} caracteres.");

        var desc = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        var resp = string.IsNullOrWhiteSpace(responsable) ? null : responsable.Trim();

        var cambio = Nombre != limpio
                  || Descripcion != desc
                  || FechaPlan != fechaPlan
                  || ResponsableId != responsableId
                  || Responsable != resp;

        Nombre        = limpio;
        Descripcion   = desc;
        FechaPlan     = fechaPlan;
        ResponsableId = responsableId;
        Responsable   = resp;
        return cambio;
    }

    /// <summary>
    /// Avance del entregable: promedio simple de sus actividades vigentes.
    ///
    /// <para><b>Un entregable sin actividades vale por su estado</b>, con la regla 0/50/100 del
    /// PMI: Pendiente 0, En proceso 50, Completado 100. Es lo que sostiene a los entregables que
    /// vienen de la carga inicial —ninguno tiene actividades todavía— y evita que empezar a
    /// desglosar un proyecto haga desaparecer el avance de los demás.</para>
    ///
    /// <para>Necesita las actividades cargadas. Con la colección sin cargar cae en la regla por
    /// estado, que devuelve un número plausible y por eso es peligroso: el recálculo del proyecto
    /// pasa siempre por <c>ProyectoConEstructura</c>, que las trae.</para>
    /// </summary>
    public int AvanceCalculado
    {
        get
        {
            var vigentes = _actividades.Where(a => a.Estado != EstadoActividad.Cancelada).ToList();
            if (vigentes.Count > 0)
                return (int)Math.Round(vigentes.Average(a => (double)a.AvancePct), MidpointRounding.AwayFromZero);

            return Estado switch
            {
                EstadoEntregable.Completado => 100,
                EstadoEntregable.EnProceso  => 50,
                _                           => 0
            };
        }
    }

    /// <summary>Vencido y sin cerrar. Se calcula, no se guarda — igual que EstadoCompromiso.</summary>
    public bool EstaAtrasado(DateOnly hoy) =>
        FechaPlan is { } plan
        && plan < hoy
        && Estado is EstadoEntregable.Pendiente or EstadoEntregable.EnProceso;

    /// <summary>Cambia el estado del entregable. Salir de Completado limpia la fecha real: si el
    /// entregable no está entregado, no tiene fecha de entrega.</summary>
    public bool CambiarEstado(EstadoEntregable nuevo)
    {
        if (nuevo == Estado) return false;

        Estado = nuevo;
        if (nuevo != EstadoEntregable.Completado) FechaReal = null;
        return true;
    }

    /// <summary>
    /// Da el entregable por cumplido y le fija la fecha real si no la tenía.
    ///
    /// <para>Existe para que el reporte de avance pueda cerrar el entregable al que se imputa.
    /// Antes eso había que hacerlo aparte, en la tabla de la ficha, y en la práctica no se hacía:
    /// el avance declarado subía mientras los entregables seguían en «Pendiente», y las dos medidas
    /// del mismo proyecto terminaban contando historias distintas.</para>
    ///
    /// <para>Devuelve <c>false</c> si ya estaba completado, para que quien lo llame no escriba una
    /// entrada de bitácora por algo que no cambió.</para>
    /// </summary>
    public bool Completar(DateOnly fecha)
    {
        if (Estado == EstadoEntregable.Completado) return false;

        if (Estado == EstadoEntregable.Cancelado)
            throw new DomainException(
                $"El entregable «{Nombre}» está cancelado: para darlo por cumplido hay que reactivarlo desde la ficha.");

        Estado = EstadoEntregable.Completado;
        FechaReal ??= fecha;
        return true;
    }

    /// <summary>Todas sus actividades vigentes llegaron al 100 %. Con la lista vacía es
    /// <c>false</c>: un entregable sin actividades no se cierra solo, lo cierra alguien.</summary>
    public bool ActividadesTerminadas()
    {
        var vigentes = _actividades.Where(a => a.Estado != EstadoActividad.Cancelada).ToList();
        return vigentes.Count > 0 && vigentes.All(a => a.Estado == EstadoActividad.Completada);
    }

    // ── Actividades ───────────────────────────────────────────────
    public void Agregar(ActividadProyecto actividad) => _actividades.Add(actividad);

    public void QuitarActividad(ActividadProyecto actividad) => _actividades.Remove(actividad);

    public int SiguienteOrdenActividad() => _actividades.Count == 0 ? 1 : _actividades.Max(a => a.Orden) + 1;

    /// <summary>Rango que ocupan las actividades planificadas. Es informativo: el atraso del
    /// entregable se mide contra <see cref="FechaPlan"/>, no contra esto.</summary>
    public (DateOnly? Inicio, DateOnly? Fin) RangoPlanificado()
    {
        var inicios = _actividades.Where(a => a.FechaInicioPlan.HasValue).Select(a => a.FechaInicioPlan!.Value).ToList();
        var fines   = _actividades.Where(a => a.FechaFinPlan.HasValue).Select(a => a.FechaFinPlan!.Value).ToList();
        return (inicios.Count == 0 ? null : inicios.Min(),
                fines.Count   == 0 ? null : fines.Max());
    }

    /// <summary>Mismo contrato que <see cref="Proyecto.ReordenarEntregables"/>: la lista completa
    /// de actividades del entregable, o no se aplica.</summary>
    public void ReordenarActividades(IReadOnlyList<int> idsEnOrden)
    {
        if (idsEnOrden is null || idsEnOrden.Count == 0)
            throw new DomainException("No se recibió el orden de las actividades.");

        if (idsEnOrden.Distinct().Count() != idsEnOrden.Count)
            throw new DomainException("El orden recibido trae actividades repetidas.");

        if (!_actividades.Select(a => a.Id).ToHashSet().SetEquals(idsEnOrden))
            throw new DomainException(
                $"El orden recibido no corresponde a las actividades de «{Nombre}». " +
                "Recargue la página y vuelva a intentarlo.");

        var porId = _actividades.ToDictionary(a => a.Id);
        var orden = 0;
        foreach (var id in idsEnOrden)
            porId[id].Orden = ++orden;
    }

    public const int MaxNombre      = 300;
    public const int MaxDescripcion = 2000;
}

/// <summary>
/// Actividad de un entregable: el nivel donde se trabaja y donde se mide.
///
/// <para>Es el único punto del árbol donde alguien escribe un porcentaje. De ahí sube todo:
/// la actividad promedia hacia el entregable y el entregable hacia el proyecto. Su ventana
/// —<see cref="FechaInicioPlan"/> a <see cref="FechaFinPlan"/>— es lo que permite ver un atraso
/// antes de que venza el entregable completo.</para>
///
/// <para><b>Las fechas reales no se teclean.</b> Las marca <see cref="Reportar"/> a partir del
/// porcentaje: pasar de 0 sella el inicio, llegar a 100 sella el fin, y bajar de 100 lo suelta.
/// Es el mismo criterio que ya usaba <c>Proyecto.CambiarEstado</c>: la fecha real es el hecho, no
/// un campo que alguien tenga que acordarse de llenar.</para>
/// </summary>
public sealed class ActividadProyecto : BaseAuditableEntity
{
    public int     EntregableId { get; set; }
    public int     Orden        { get; set; }
    public string  Nombre       { get; private set; } = default!;
    public string? Descripcion  { get; private set; }

    public DateOnly? FechaInicioPlan { get; private set; }
    public DateOnly? FechaFinPlan    { get; private set; }
    public DateOnly? FechaInicioReal { get; private set; }
    public DateOnly? FechaFinReal    { get; private set; }

    /// <summary>Lo que reporta quien la ejecuta, de 0 a 100. Solo lo mueve
    /// <see cref="Reportar"/>, que además mantiene sincronizados el estado y las fechas reales.</summary>
    public int AvancePct { get; private set; }

    public EstadoActividad Estado { get; private set; } = EstadoActividad.Pendiente;

    /// <summary>Interesado del proyecto que responde por la actividad. Ver la nota de
    /// <see cref="EntregableProyecto.ResponsableId"/>.</summary>
    public Guid?   ResponsableId { get; private set; }
    public string? Responsable   { get; private set; }

    /// <summary>
    /// Actividades que tienen que terminar antes de que esta pueda arrancar (Fin → Comienzo).
    /// La dependencia la posee la sucesora; ver <see cref="DependenciaActividad"/>.
    ///
    /// <para>Solo se puede depender de actividades <b>ya guardadas</b>: la fila nueva del editor
    /// todavía no tiene Id y no hay con qué referenciarla.</para>
    /// </summary>
    private readonly List<DependenciaActividad> _predecesoras = [];
    public IReadOnlyCollection<DependenciaActividad> Predecesoras => _predecesoras.AsReadOnly();

    public IEnumerable<int> PredecesoraIds => _predecesoras.Select(d => d.PredecesoraId);

    private ActividadProyecto() { }   // EF

    public static ActividadProyecto Crear(string nombre, int orden)
    {
        var a = new ActividadProyecto { Orden = orden };
        a.Definir(nombre, null, null, null, null, null);
        return a;
    }

    /// <summary>
    /// Fija los datos editables de la actividad. El porcentaje, el estado y las fechas reales
    /// quedan fuera: los mueven <see cref="Reportar"/> y <see cref="Cancelar"/>.
    /// </summary>
    /// <returns><c>true</c> si algo cambió.</returns>
    public bool Definir(
        string    nombre,
        string?   descripcion,
        DateOnly? inicioPlan,
        DateOnly? finPlan,
        Guid?     responsableId,
        string?   responsable)
    {
        var limpio = (nombre ?? "").Trim();
        if (limpio.Length == 0)
            throw new DomainException("La actividad necesita un nombre.");
        if (limpio.Length > MaxNombre)
            throw new DomainException($"El nombre de la actividad no puede superar los {MaxNombre} caracteres.");

        // Se valida acá y no en la capa de aplicación porque es la regla de la propia actividad:
        // una ventana que termina antes de empezar no es un dato incompleto, es un dato imposible.
        if (inicioPlan is { } ini && finPlan is { } fin && fin < ini)
            throw new DomainException(
                $"«{limpio}»: la fecha de fin no puede ser anterior a la de inicio.");

        var desc = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        var resp = string.IsNullOrWhiteSpace(responsable) ? null : responsable.Trim();

        var cambio = Nombre != limpio
                  || Descripcion != desc
                  || FechaInicioPlan != inicioPlan
                  || FechaFinPlan != finPlan
                  || ResponsableId != responsableId
                  || Responsable != resp;

        Nombre          = limpio;
        Descripcion     = desc;
        FechaInicioPlan = inicioPlan;
        FechaFinPlan    = finPlan;
        ResponsableId   = responsableId;
        Responsable     = resp;
        return cambio;
    }

    /// <summary>
    /// Reporta el avance de la actividad y deja el estado y las fechas reales en consecuencia.
    /// </summary>
    /// <returns><c>true</c> si el porcentaje cambió.</returns>
    public bool Reportar(int porcentaje, DateOnly hoy)
    {
        if (porcentaje is < 0 or > 100)
            throw new DomainException("El avance de la actividad debe estar entre 0 y 100.");

        if (Estado == EstadoActividad.Cancelada)
            throw new DomainException(
                $"La actividad «{Nombre}» está cancelada: reactívela antes de reportar avance.");

        var cambio = AvancePct != porcentaje;
        AvancePct  = porcentaje;

        Estado = porcentaje switch
        {
            0   => EstadoActividad.Pendiente,
            100 => EstadoActividad.Completada,
            _   => EstadoActividad.EnProceso
        };

        if (porcentaje > 0) FechaInicioReal ??= hoy;
        else                FechaInicioReal   = null;

        // Bajar del 100 % suelta la fecha de cierre: si la actividad volvió a estar abierta, no
        // terminó. Es la misma corrección que hace Proyecto.Reabrir con FechaFinReal.
        if (porcentaje == 100) FechaFinReal ??= hoy;
        else                   FechaFinReal   = null;

        return cambio;
    }

    /// <summary>Saca la actividad del promedio del entregable. No se borra: cancelarla es un dato,
    /// y las entradas de bitácora imputadas a ella siguen apuntando a algo legible.</summary>
    public bool Cancelar()
    {
        if (Estado == EstadoActividad.Cancelada) return false;
        Estado = EstadoActividad.Cancelada;
        return true;
    }

    /// <summary>La devuelve al ruedo con el porcentaje que tenía.</summary>
    public bool Reactivar()
    {
        if (Estado != EstadoActividad.Cancelada) return false;
        Estado = AvancePct switch
        {
            0   => EstadoActividad.Pendiente,
            100 => EstadoActividad.Completada,
            _   => EstadoActividad.EnProceso
        };
        return true;
    }

    /// <summary>
    /// Deja las predecesoras exactamente en la lista recibida.
    ///
    /// <para><b>Reemplazo en bloque</b>, no reconciliación por Id, al revés que los entregables y
    /// las actividades: la fila de dependencia no es destino de ninguna FK, así que recrearla no
    /// desimputa nada. Los Ids que no existan todavía —una fila nueva del editor— se descartan:
    /// no se puede depender de algo que aún no tiene identidad.</para>
    /// </summary>
    /// <returns><c>true</c> si el conjunto cambió, para que la bitácora no registre un no-cambio.</returns>
    public bool FijarPredecesoras(IEnumerable<int>? ids)
    {
        var nuevas = (ids ?? []).Where(id => id > 0).Distinct().ToHashSet();

        // Un ciclo de largo 1. Se ataja acá y no en el grafo porque no necesita ver el resto:
        // así el mensaje nombra a esta actividad en vez de hablar de un «círculo» de uno.
        if (Id > 0 && nuevas.Contains(Id))
            throw new DomainException($"«{Nombre}» no puede depender de sí misma.");

        var actuales = _predecesoras.Select(d => d.PredecesoraId).ToHashSet();
        if (actuales.SetEquals(nuevas)) return false;

        _predecesoras.RemoveAll(d => !nuevas.Contains(d.PredecesoraId));
        foreach (var id in nuevas.Where(id => !actuales.Contains(id)))
            _predecesoras.Add(DependenciaActividad.Crear(id));

        return true;
    }

    /// <summary>Todavía no terminó. Es lo que hace que una sucesora esté bloqueada: una
    /// predecesora <b>cancelada</b> no bloquea a nadie — dejó de ser parte del plan, igual que
    /// sale del promedio del entregable.</summary>
    public bool SigueAbierta => Estado is EstadoActividad.Pendiente or EstadoActividad.EnProceso;

    /// <summary>Su ventana venció y no llegó al 100 %.</summary>
    public bool EstaAtrasada(DateOnly hoy) =>
        FechaFinPlan is { } fin
        && fin < hoy
        && Estado is EstadoActividad.Pendiente or EstadoActividad.EnProceso;

    /// <summary>Debía haber arrancado y sigue en cero. Es la señal que el entregable no da: se ve
    /// semanas antes de que haya algo vencido.</summary>
    public bool NoArrancada(DateOnly hoy) =>
        FechaInicioPlan is { } ini
        && ini < hoy
        && Estado == EstadoActividad.Pendiente;

    public const int MaxNombre      = 300;
    public const int MaxDescripcion = 2000;
}

/// <summary>
/// Bitácora de ejecución del proyecto: qué se hizo, cuándo, quién lo reportó, con qué evidencia
/// y qué lo está bloqueando. Registro acumulativo — cada entrada queda con su fecha y su autor.
///
/// <para><b>Corrección, no reescritura.</b> El responsable del proyecto puede enmendar una entrada
/// con <see cref="Actualizar"/>, pero la entrada no pierde su identidad: conserva su
/// <see cref="Fecha"/> y su <see cref="Autor"/> originales, y queda sellada con
/// <see cref="EditadoEn"/> y <see cref="EditadoPor"/> para que la vista pueda decir que fue
/// corregida y por quién. El <see cref="PorcentajeReportado"/> queda deliberadamente fuera de lo
/// editable: es el número que movió a una actividad, y cambiarlo en una entrada vieja no desharía
/// ese efecto. Para corregir un porcentaje se reporta de nuevo.</para>
///
/// <para>Tabla independiente, <b>no</b> navegación del agregado <see cref="Proyecto"/>: lo que
/// cuelga del agregado se arrastra en sus operaciones de colección. Mismo patrón y misma razón que
/// <see cref="NotaSeguimientoExpediente"/> y <see cref="BitacoraExpediente"/>.</para>
/// </summary>
public sealed class AvanceProyecto : BaseEntity
{
    public int  ProyectoId { get; private set; }

    /// <summary>Entregable al que se imputa el avance. Null = avance general del proyecto.
    /// Es la columna que antes se llamaba <c>HitoId</c>, con sus mismos datos.</summary>
    public int? EntregableId { get; private set; }

    /// <summary>Actividad concreta a la que se imputa, dentro de <see cref="EntregableId"/>.
    /// Null = el reporte habla del entregable en general. Es lo que le da sentido a
    /// <see cref="PorcentajeReportado"/>: el porcentaje se reporta sobre una actividad.</summary>
    public int? ActividadId { get; private set; }

    public DateTime Fecha  { get; private set; }
    public string   Autor  { get; private set; } = default!;
    public string   Descripcion { get; private set; } = default!;

    /// <summary>
    /// Porcentaje que este reporte le fijó a <see cref="ActividadId"/>. <b>Null</b> cuando la
    /// entrada no mueve ningún número: una nota de ejecución, un bloqueo, un avance general.
    ///
    /// <para>Las entradas anteriores al 2026-08-25 lo traen siempre lleno y significan otra cosa —
    /// el porcentaje del <i>proyecto</i>, que el responsable declaraba a mano cuando el avance se
    /// declaraba en vez de calcularse. Se conservaron tal cual: reinterpretarlas habría inventado
    /// un dato que nadie reportó.</para>
    /// </summary>
    public int? PorcentajeReportado { get; private set; }

    /// <summary>Qué está trabando la ejecución, si algo. Null = sin bloqueos.</summary>
    public string?  Bloqueo { get; private set; }

    /// <summary>
    /// Riesgo del registro que este bloqueo vino a confirmar. Null = el bloqueo no estaba
    /// anticipado, que también es un dato.
    ///
    /// <para>Es lo que une las dos listas que hasta el 2026-08-25 corrían en paralelo: riesgos por
    /// un lado, bloqueos por otro, sin forma de saber si lo que hoy traba la ejecución es
    /// exactamente lo que alguien previó hace dos meses. Al vincularlo, el riesgo pasa a
    /// <see cref="EstadoRiesgo.Materializado"/>, que existía para esto y nadie podía accionar.</para>
    /// </summary>
    public int? RiesgoId { get; private set; }

    /// <summary>Cuándo se corrigió la entrada. Null = nunca se tocó desde que se registró.</summary>
    public DateTime? EditadoEn  { get; private set; }
    /// <summary>Quién la corrigió. Convive con <see cref="Autor"/>: la entrada sigue siendo de quien
    /// la reportó, y este sello dice quién la enmendó después.</summary>
    public string?   EditadoPor { get; private set; }

    public string?  ArchivoNombre { get; private set; }
    /// <summary>Ruta relativa devuelta por AdjuntoStorage. <b>No se enlaza directo desde la vista</b>:
    /// /uploads se sirve como estático sin autorización, así que la evidencia baja por el handler
    /// autenticado de la página.</summary>
    public string?  ArchivoUrl    { get; private set; }
    public long?    ArchivoTamano { get; private set; }

    private AvanceProyecto() { }   // EF

    public static AvanceProyecto Crear(
        int      proyectoId,
        int?     entregableId,
        int?     actividadId,
        string   descripcion,
        int?     porcentajeReportado,
        string   autor,
        string?  bloqueo = null,
        string?  archivoNombre = null,
        string?  archivoUrl = null,
        long?    archivoTamano = null,
        int?     riesgoId = null)
    {
        if (proyectoId <= 0)
            throw new DomainException("El avance debe pertenecer a un proyecto.");

        // El porcentaje es de la actividad: sin actividad no hay a qué referirlo. Guardarlo suelto
        // repetiría el equívoco del modelo anterior, donde un número que nadie sabía de qué era
        // terminaba siendo el avance del proyecto entero.
        if (porcentajeReportado is not null && actividadId is null)
            throw new DomainException("Para reportar un porcentaje hay que imputar el avance a una actividad.");

        if (porcentajeReportado is { } pct && pct is < 0 or > 100)
            throw new DomainException("El avance reportado debe estar entre 0 y 100.");

        // La actividad vive dentro de un entregable: imputar a una sin decir a cuál dejaría la
        // entrada colgando de media jerarquía.
        if (actividadId is not null && entregableId is null)
            throw new DomainException("La actividad imputada tiene que venir con su entregable.");

        // Señalar un riesgo sin decir qué está trabando deja el registro sin sentido: lo que
        // materializa al riesgo es el bloqueo, no la referencia suelta.
        if (riesgoId is not null && string.IsNullOrWhiteSpace(bloqueo))
            throw new DomainException("Para vincular un riesgo hay que describir el bloqueo que lo materializa.");

        var limpio = (descripcion ?? "").Trim();
        if (limpio.Length == 0)
            throw new DomainException("Hay que describir qué se avanzó.");
        if (limpio.Length > MaxDescripcion)
            throw new DomainException($"La descripción no puede superar los {MaxDescripcion} caracteres.");

        return new AvanceProyecto
        {
            ProyectoId          = proyectoId,
            EntregableId        = entregableId,
            ActividadId         = actividadId,
            Descripcion         = limpio,
            PorcentajeReportado = porcentajeReportado,
            Autor               = string.IsNullOrWhiteSpace(autor) ? "—" : autor.Trim(),
            Bloqueo             = string.IsNullOrWhiteSpace(bloqueo) ? null : bloqueo.Trim(),
            RiesgoId            = riesgoId,
            ArchivoNombre       = string.IsNullOrWhiteSpace(archivoNombre) ? null : archivoNombre.Trim(),
            ArchivoUrl          = string.IsNullOrWhiteSpace(archivoUrl) ? null : archivoUrl.Trim(),
            ArchivoTamano       = archivoTamano,
            Fecha               = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Corrige el contenido de la entrada y la sella con quién y cuándo. No toca
    /// <see cref="Fecha"/>, <see cref="Autor"/>, el porcentaje ni la evidencia adjunta: lo primero
    /// porque la entrada pertenece a quien la reportó, lo segundo por lo explicado en el resumen
    /// del tipo.
    /// </summary>
    public void Actualizar(string descripcion, string? bloqueo, int? entregableId, int? actividadId, string editor)
    {
        var limpio = (descripcion ?? "").Trim();
        if (limpio.Length == 0)
            throw new DomainException("Hay que describir qué se avanzó.");
        if (limpio.Length > MaxDescripcion)
            throw new DomainException($"La descripción no puede superar los {MaxDescripcion} caracteres.");

        if (actividadId is not null && entregableId is null)
            throw new DomainException("La actividad imputada tiene que venir con su entregable.");

        // Reimputar a otra actividad no deshace el efecto que tuvo el reporte sobre la primera, así
        // que una entrada que movió un porcentaje se queda donde está. Corregir el número es
        // reportar de nuevo, no reescribir la historia.
        if (PorcentajeReportado is not null && actividadId != ActividadId)
            throw new DomainException(
                "Esta entrada fijó el avance de una actividad: no se puede reimputar a otra. " +
                "Registre un reporte nuevo con el valor correcto.");

        Descripcion  = limpio;
        Bloqueo      = string.IsNullOrWhiteSpace(bloqueo) ? null : bloqueo.Trim();
        EntregableId = entregableId;
        ActividadId  = actividadId;
        EditadoEn    = DateTime.UtcNow;
        EditadoPor   = string.IsNullOrWhiteSpace(editor) ? "—" : editor.Trim();

        // Si al corregir se borró el bloqueo, el vínculo con el riesgo pierde sentido: quedaría
        // un riesgo señalado como materializado por un hecho que ya no está escrito en ningún lado.
        if (Bloqueo is null) RiesgoId = null;
    }

    /// <summary>Suelta el vínculo con el riesgo, sin tocar el bloqueo ni marcar la entrada como
    /// editada: lo llama el borrado del riesgo, y el bloqueo ocurrió igual — lo único que se
    /// pierde es la referencia a lo que lo anticipaba.</summary>
    public void DesvincularRiesgo()
    {
        RiesgoId = null;
    }

    /// <summary>
    /// Suelta la imputación a la actividad, conservando la del entregable. Lo llama la
    /// reconciliación del editor <b>antes</b> de borrar una actividad: su FK es NoAction —no puede
    /// ser SetNull sin chocar con el error 1785, ver la configuración— así que el borrado fallaría
    /// con la entrada apuntándola.
    ///
    /// <para>No marca la entrada como editada: nadie la corrigió, desapareció aquello a lo que
    /// apuntaba. El porcentaje que reportó se conserva —es lo que se reportó ese día— y el texto
    /// sigue diciendo qué se hizo.</para>
    /// </summary>
    public void DesimputarActividad()
    {
        ActividadId = null;
    }

    public const int MaxDescripcion = 2000;
}
