namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Proyecto interno de DIGER (digitalización, PKI, conectividad, BIEN…). No es un plan de
/// racionalización de trámites — eso es <see cref="PlanTrabajo"/>, que sí pertenece a una
/// institución y cuelga sus metas de expedientes.
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
/// que no puede abrir, y las acciones que le reservamos —reordenar hitos, corregir bitácora— serían
/// inalcanzables para la única persona autorizada a ejecutarlas.</para>
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
    /// Último porcentaje reportado por el responsable. Es un <b>snapshot</b> para que el listado
    /// no tenga que agregar sobre la bitácora; el histórico completo vive en
    /// <see cref="AvanceProyecto"/>. Por eso el setter es privado: solo lo escribe
    /// <see cref="AplicarAvance"/>, invocado al registrar un avance — nunca el editor de la ficha.
    /// </summary>
    public int AvancePct { get; private set; }

    private readonly List<HitoProyecto> _hitos = [];
    public IReadOnlyCollection<HitoProyecto> Hitos => _hitos.AsReadOnly();

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

    /// <summary>Fija el porcentaje que acaba de reportar el responsable. Lo llama el comando de
    /// registrar avance, después de haber creado la entrada de bitácora.</summary>
    public void AplicarAvance(int porcentaje)
    {
        if (porcentaje is < 0 or > 100)
            throw new DomainException("El avance reportado debe estar entre 0 y 100.");
        AvancePct = porcentaje;
    }

    // ── Hitos ─────────────────────────────────────────────────────
    /// <summary>
    /// Vacía la lista de hitos.
    ///
    /// <para><b>Cuidado: esto borra filas.</b> Cada hito recreado después nace con un Id nuevo, y
    /// la FK de <see cref="AvanceProyecto.HitoId"/> está en SetNull, así que todo avance imputado
    /// a un hito borrado se desimputa en silencio. Por eso el editor <b>ya no reemplaza en bloque</b>:
    /// reconcilia por Id y solo llama a <see cref="QuitarHito"/> para los que el usuario realmente
    /// quitó. Este método queda para ese caso y para borrados deliberados.</para>
    /// </summary>
    public void LimpiarHitos() => _hitos.Clear();

    public void Agregar(HitoProyecto hito) => _hitos.Add(hito);

    /// <summary>Quita un hito del proyecto. Los avances imputados a él quedan con
    /// <c>HitoId</c> nulo: la entrada de bitácora no se pierde, deja de estar imputada.</summary>
    public void QuitarHito(HitoProyecto hito) => _hitos.Remove(hito);

    /// <summary>Siguiente número de orden libre, para los hitos que se agregan desde la ficha:
    /// van al final y no alteran la posición de los que ya estaban.</summary>
    public int SiguienteOrden() => _hitos.Count == 0 ? 1 : _hitos.Max(h => h.Orden) + 1;

    /// <summary>
    /// Reordena los hitos según la secuencia de Ids recibida, renumerando <see cref="HitoProyecto.Orden"/>
    /// de 1 en adelante.
    ///
    /// <para>Exige la lista <b>completa</b> de hitos del proyecto, no un subconjunto: reordenar con
    /// una parte dejaría a los ausentes con un Orden arbitrario respecto de los movidos, y el
    /// cronograma pasaría a mentir sin que nadie lo note. Si la lista no calza exactamente con los
    /// hitos vigentes —porque alguien agregó o borró uno en otra pestaña— se rechaza el reordenamiento
    /// en vez de aplicarlo a medias.</para>
    /// </summary>
    public void ReordenarHitos(IReadOnlyList<int> idsEnOrden)
    {
        if (idsEnOrden is null || idsEnOrden.Count == 0)
            throw new DomainException("No se recibió el orden de los hitos.");

        if (idsEnOrden.Distinct().Count() != idsEnOrden.Count)
            throw new DomainException("El orden recibido trae hitos repetidos.");

        if (!_hitos.Select(h => h.Id).ToHashSet().SetEquals(idsEnOrden))
            throw new DomainException(
                "El orden recibido no corresponde a los hitos actuales del proyecto. " +
                "Recargue la página y vuelva a intentarlo.");

        var porId = _hitos.ToDictionary(h => h.Id);
        var orden = 0;
        foreach (var id in idsEnOrden)
            porId[id].Orden = ++orden;
    }
}

/// <summary>
/// Entregable planificado dentro de un proyecto. Sirve para el cronograma y el semáforo de
/// atraso; <b>no</b> calcula el porcentaje de avance — ese lo reporta el responsable.
/// </summary>
public sealed class HitoProyecto : BaseEntity
{
    public int     ProyectoId  { get; set; }
    public int     Orden       { get; set; }
    public string  Nombre      { get; set; } = default!;
    public string? Descripcion { get; set; }
    public DateOnly? FechaPlan { get; set; }
    public DateOnly? FechaReal { get; set; }
    public EstadoHito Estado   { get; set; } = EstadoHito.Pendiente;
    public Guid?   ResponsableId { get; set; }
    public string? Responsable   { get; set; }

    /// <summary>Vencido y sin cerrar. Se calcula, no se guarda — igual que EstadoCompromiso.</summary>
    public bool EstaAtrasado(DateOnly hoy) =>
        FechaPlan is { } plan
        && plan < hoy
        && Estado is EstadoHito.Pendiente or EstadoHito.EnProceso;

    /// <summary>
    /// Da el hito por cumplido y le fija la fecha real si no la tenía.
    ///
    /// <para>Existe para que el reporte de avance pueda cerrar el hito al que se imputa. Antes eso
    /// había que hacerlo aparte, en la tabla de la ficha, y en la práctica no se hacía: el avance
    /// declarado subía mientras los hitos seguían en «Pendiente», y las dos medidas del mismo
    /// proyecto terminaban contando historias distintas.</para>
    ///
    /// <para>Devuelve <c>false</c> si ya estaba completado, para que quien lo llame no escriba una
    /// entrada de bitácora por algo que no cambió.</para>
    /// </summary>
    public bool Completar(DateOnly fecha)
    {
        if (Estado == EstadoHito.Completado) return false;

        if (Estado == EstadoHito.Cancelado)
            throw new DomainException(
                $"El hito «{Nombre}» está cancelado: para darlo por cumplido hay que reactivarlo desde la ficha.");

        Estado = EstadoHito.Completado;
        FechaReal ??= fecha;
        return true;
    }
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
/// editable: es el número que alimenta el snapshot <see cref="Proyecto.AvancePct"/>, y cambiarlo
/// en una entrada vieja lo desincronizaría del último reporte sin que nadie lo advierta. Para
/// corregir un porcentaje se registra un avance nuevo, que es lo que el snapshot lee.</para>
///
/// <para>Tabla independiente, <b>no</b> navegación del agregado <see cref="Proyecto"/>: el editor
/// reemplaza los hitos en bloque (<see cref="Proyecto.LimpiarHitos"/>) y una bitácora colgada del
/// agregado se perdería en ese reemplazo. Mismo patrón y misma razón que
/// <see cref="NotaSeguimientoExpediente"/> y <see cref="BitacoraExpediente"/>.</para>
/// </summary>
public sealed class AvanceProyecto : BaseEntity
{
    public int  ProyectoId { get; private set; }
    /// <summary>Hito al que se imputa el avance. Null = avance general del proyecto.</summary>
    public int? HitoId     { get; private set; }

    public DateTime Fecha  { get; private set; }
    public string   Autor  { get; private set; } = default!;
    public string   Descripcion { get; private set; } = default!;

    /// <summary>Porcentaje de avance del proyecto declarado en este reporte.</summary>
    public int      PorcentajeReportado { get; private set; }

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
        int?     hitoId,
        string   descripcion,
        int      porcentajeReportado,
        string   autor,
        string?  bloqueo = null,
        string?  archivoNombre = null,
        string?  archivoUrl = null,
        long?    archivoTamano = null,
        int?     riesgoId = null)
    {
        if (proyectoId <= 0)
            throw new DomainException("El avance debe pertenecer a un proyecto.");

        // Señalar un riesgo sin decir qué está trabando deja el registro sin sentido: lo que
        // materializa al riesgo es el bloqueo, no la referencia suelta.
        if (riesgoId is not null && string.IsNullOrWhiteSpace(bloqueo))
            throw new DomainException("Para vincular un riesgo hay que describir el bloqueo que lo materializa.");

        var limpio = (descripcion ?? "").Trim();
        if (limpio.Length == 0)
            throw new DomainException("Hay que describir qué se avanzó.");
        if (limpio.Length > MaxDescripcion)
            throw new DomainException($"La descripción no puede superar los {MaxDescripcion} caracteres.");

        if (porcentajeReportado is < 0 or > 100)
            throw new DomainException("El avance reportado debe estar entre 0 y 100.");

        return new AvanceProyecto
        {
            ProyectoId          = proyectoId,
            HitoId              = hitoId,
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
    public void Actualizar(string descripcion, string? bloqueo, int? hitoId, string editor)
    {
        var limpio = (descripcion ?? "").Trim();
        if (limpio.Length == 0)
            throw new DomainException("Hay que describir qué se avanzó.");
        if (limpio.Length > MaxDescripcion)
            throw new DomainException($"La descripción no puede superar los {MaxDescripcion} caracteres.");

        Descripcion = limpio;
        Bloqueo     = string.IsNullOrWhiteSpace(bloqueo) ? null : bloqueo.Trim();
        HitoId      = hitoId;
        EditadoEn   = DateTime.UtcNow;
        EditadoPor  = string.IsNullOrWhiteSpace(editor) ? "—" : editor.Trim();

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

    public const int MaxDescripcion = 2000;
}
