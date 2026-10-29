namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Reunión o capacitación documentada (acta + asistencia + acuerdos).
/// Agregado de captura: escalares asignables; hijos reemplazados en bloque.
/// </summary>
public sealed class Reunion : BaseAuditableEntity, ISoftDeletable
{
    // ── Soft Delete ───────────────────────────────────────────────
    public bool IsDeleted { get; set; }
    public string Titulo { get; private set; } = default!;

    /// <summary>Id del registro origen (p. ej. Supabase) cuando la reunión fue importada.
    /// Permite reimportar de forma idempotente; null para reuniones creadas en el portal.</summary>
    public string? OrigenExternoId { get; set; }

    // ── Visibilidad ───────────────────────────────────────────────
    /// <summary>Pública (visible para su institución/alcance) o Privada (solo el creador).</summary>
    public VisibilidadReunion Visibilidad { get; set; } = VisibilidadReunion.Publica;
    /// <summary>Id del usuario que creó la reunión (para las reuniones privadas).</summary>
    public Guid? CreadoPorId { get; set; }

    // ── Hilo (reuniones enlazadas) ────────────────────────────────
    /// <summary>Identificador del hilo al que pertenece la reunión. Las reuniones que comparten
    /// el mismo <see cref="HiloId"/> forman una secuencia enlazada para dar seguimiento a acciones
    /// y tareas a través del tiempo. <c>null</c> = no pertenece a ningún hilo.</summary>
    public Guid? HiloId { get; private set; }

    /// <summary>Enlaza la reunión al hilo indicado.</summary>
    public void EnlazarAHilo(Guid hiloId) => HiloId = hiloId;

    /// <summary>Saca la reunión de su hilo actual.</summary>
    public void SalirDelHilo() => HiloId = null;

    // ── Auto-registro de asistencia (enlace + QR público) ─────────
    /// <summary>Token público (difícil de adivinar) del enlace de auto-registro de participantes.</summary>
    public Guid RegistroToken  { get; private set; } = Guid.NewGuid();
    /// <summary>Si el formulario público de auto-registro acepta nuevos participantes.</summary>
    public bool RegistroAbierto { get; set; } = true;

    public void RegenerarToken() => RegistroToken = Guid.NewGuid();

    // ── Datos generales ───────────────────────────────────────────
    public DateOnly? Fecha     { get; set; }
    public string?   Hora      { get; set; }
    public string?   Duracion  { get; set; }
    public string?   Modalidad { get; set; }   // Presencial / Virtual / Híbrida / Otro
    public string?   Lugar     { get; set; }
    public string?   InstitucionId { get; set; } // beneficiaria (opcional; permite "Otro")

    /// <summary>
    /// Institución DUEÑA: la casa que convocó. No confundir con <see cref="InstitucionId"/>, que es
    /// la CONTRAPARTE invitada y vale null cuando no se eligió ninguna.
    ///
    /// Existe porque el filtro de alcance necesitaba un ancla institucional y solo tenía la
    /// contraparte: un usuario de DIGER veía las reuniones donde DIGER figuraba como invitada —un
    /// recorte arbitrario de 14 de 81— y, peor, los usuarios de CONSUCOOP veían 17 reuniones
    /// internas de DIGER, con sus acuerdos y sus listas de asistentes, solo por estar nombrados
    /// como contraparte. Area y Unidad ya anclaban al dueño, pero llegan nulas en 80 de 81.
    /// </summary>
    public string?   InstitucionDuenaId { get; set; }

    public string?   AreaId        { get; set; }
    public string?   UnidadId      { get; set; }
    public string?   Institucion   { get; set; } // snapshot del nombre
    public string?   Tipo      { get; set; }     // Taller / Seminario / Reunión técnica / …
    public bool      EsCapacitacionPlataforma { get; set; }

    // ── Expediente vinculado ──────────────────────────────────────
    public int?    ExpedienteId     { get; set; }
    public string? ExpedienteCodigo { get; set; }

    // ── Memoria ───────────────────────────────────────────────────
    public string? ObjetivoAgenda { get; set; }
    public string? Desarrollo     { get; set; }

    // ── Capacitación ──────────────────────────────────────────────
    public string? Tema        { get; set; }
    public string? ObjetivoCap  { get; set; }
    public string? Contenido    { get; set; } // puntos, uno por línea

    // ── Enlace institucional ──────────────────────────────────────
    public string? EpNombre { get; set; }
    public string? EpCargo  { get; set; }
    public string? EpCorreo { get; set; }
    public string? EpTel    { get; set; }

    // ── Facilitador DIGER ─────────────────────────────────────────
    public string? FacNombre { get; set; }
    public string? FacCargo  { get; set; }
    public string? FacCorreo { get; set; }

    // ── Resultados ────────────────────────────────────────────────
    public int?    Convocados    { get; set; }
    public int?    NumAsistentes { get; set; }
    public int?    PctAsistencia { get; set; }
    public int?    SatisfaccionCalificacion { get; set; } // 1-5
    public string? Satisfaccion  { get; set; } // comentario libre
    public string? Compromisos   { get; set; } // uno por línea

    // ── Validación y evidencias ───────────────────────────────────
    /// <summary>La reunión levanta encuesta de satisfacción. Se enciende desde «Generales» y es lo
    /// que decide si el cierre pide una segunda firma: sin encuesta valida solo la DIGER; con
    /// encuesta valida también la institución, que es quien responde por lo que opinaron los suyos.
    /// Es un campo propio y no se deduce de si hay respuestas: una encuesta recién activada todavía
    /// no tiene ninguna, y al recargar la pantalla aparecería apagada.</summary>
    public bool    EncuestaActiva { get; set; }
    public string? ValDiger    { get; set; }
    public string? ValInst     { get; set; }
    public string? DocsRecursos { get; set; }
    public string? Foto1Url  { get; set; }
    public string? Foto1Desc { get; set; }
    public string? Foto2Url  { get; set; }
    public string? Foto2Desc { get; set; }

    // ── Outlook (fase 1 de reuniones a Outlook) ───────────────────
    // Ver docs/superpowers/specs/2026-10-22-reuniones-a-outlook-design.md. El portal es la
    // fuente de la verdad: estas columnas solo recuerdan qué cita le corresponde en Outlook y si
    // la última sincronización llegó.

    /// <summary>Id de la cita en el calendario del organizador. null = nunca se envió.</summary>
    public string?   OutlookEventId        { get; private set; }
    /// <summary>transactionId de Graph: se genera una vez y se reusa en cada reintento del
    /// envío, para que Graph descarte la creación repetida en vez de duplicar la cita.</summary>
    public Guid?     OutlookTransaccionId  { get; private set; }
    /// <summary>Usuario en cuyo calendario vive la cita. Actualizar y cancelar van siempre con
    /// SU conexión, aunque edite otra persona.</summary>
    public Guid?     OutlookOrganizadorId  { get; private set; }
    /// <summary>Teams nace encendido: las reuniones que van a Outlook (virtuales e híbridas) se
    /// graban para sacar la transcripción. Queda fijo tras el envío (Graph no le quita el Teams a
    /// una cita que ya lo tiene).</summary>
    public bool      OutlookConTeams       { get; private set; } = true;
    public string?   OutlookEnlaceTeams    { get; private set; }
    /// <summary>SHA-256 del último evento enviado. Guardar sin cambios visibles no llama a Graph:
    /// cada PATCH le manda «reunión actualizada» a todos los invitados.</summary>
    public string?   OutlookHuella         { get; private set; }
    public DateTime? OutlookSincronizadaEl { get; private set; }
    public bool      OutlookPendiente      { get; private set; }
    public string?   OutlookError          { get; private set; }

    public bool EstaEnOutlook => OutlookEventId is not null;

    /// <summary>Solo las reuniones virtuales e híbridas se conectan a Outlook (decisión del
    /// 2026-10-28): una presencial no necesita cita ni Teams. Se lee la modalidad escrita, así
    /// que «Híbrida», «hibrida» o «Virtual (Teams)» también cuentan.</summary>
    public bool AdmiteOutlook => EsModalidadEnLinea(Modalidad);

    public static bool EsModalidadEnLinea(string? modalidad)
    {
        if (string.IsNullOrWhiteSpace(modalidad)) return false;
        var sinTildes = new string(modalidad.Trim().Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                        != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray()).ToLowerInvariant();
        return sinTildes.StartsWith("virtual") || sinTildes.StartsWith("hibrid");
    }

    // ── Hijos ─────────────────────────────────────────────────────
    private readonly List<Asistente>          _asistentes  = [];
    private readonly List<AcuerdoReunion>     _acuerdos    = [];
    private readonly List<ReunionAdjunto>     _adjuntos    = [];
    private readonly List<ReunionInstitucion> _institucionesParticipantes = [];

    public IReadOnlyCollection<Asistente>          Asistentes             => _asistentes.AsReadOnly();
    public IReadOnlyCollection<AcuerdoReunion>     Acuerdos               => _acuerdos.AsReadOnly();
    /// <summary>Archivos y enlaces del cierre: documentos de respaldo y evidencia adicional.
    /// Ver <see cref="ReunionAdjunto"/> para por que los videos van como enlace y no como archivo.</summary>
    public IReadOnlyCollection<ReunionAdjunto>     Adjuntos               => _adjuntos.AsReadOnly();
    /// <summary>Instituciones convocadas a la reunión (acumulables). La primera agregada
    /// se conserva como <see cref="InstitucionId"/>/<see cref="Institucion"/> (institución principal),
    /// usada para el alcance institucional, el acta y el tablero.</summary>
    public IReadOnlyCollection<ReunionInstitucion> InstitucionesParticipantes => _institucionesParticipantes.AsReadOnly();

    private Reunion() { }

    public static Reunion Crear(string titulo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titulo);
        var r = new Reunion { Titulo = titulo.Trim() };
        r.AddDomainEvent(new ReunionCreatedEvent(r.Id, r.Titulo));
        return r;
    }

    public void EstablecerTitulo(string titulo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titulo);
        Titulo = titulo.Trim();
    }

    public void MarcarActualizada() => AddDomainEvent(new ReunionUpdatedEvent(Id));

    public void LimpiarHijos() { _asistentes.Clear(); _acuerdos.Clear(); _adjuntos.Clear(); }
    public void Agregar(Asistente a)      => _asistentes.Add(a);
    public void Agregar(AcuerdoReunion a) => _acuerdos.Add(a);
    public void Agregar(ReunionAdjunto a) => _adjuntos.Add(a);

    /// <summary>
    /// Reemplaza solo los acuerdos, numerándolos 0..n-1 en el orden recibido. Existe porque
    /// <see cref="LimpiarHijos"/> vacía también asistentes y adjuntos, y llenar la memoria desde una
    /// transcripción no debe llevárselos. Quien llama decide si los acuerdos actuales se pueden
    /// perder (los que tienen seguimiento no).
    /// </summary>
    public void ReemplazarAcuerdos(IEnumerable<AcuerdoReunion> nuevos)
    {
        ArgumentNullException.ThrowIfNull(nuevos);
        var lista = nuevos.ToList();
        _acuerdos.Clear();
        for (var i = 0; i < lista.Count; i++)
        {
            lista[i].Orden = i;
            _acuerdos.Add(lista[i]);
        }
    }

    /// <summary>Vacía la lista de instituciones convocadas (reemplazo en bloque).</summary>
    public void LimpiarInstituciones() => _institucionesParticipantes.Clear();

    /// <summary>Agrega una institución convocada, preservando el orden de captura (ignora duplicados).</summary>
    public void AgregarInstitucion(string institucionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(institucionId);
        if (_institucionesParticipantes.Any(x => x.InstitucionId == institucionId)) return;
        _institucionesParticipantes.Add(new ReunionInstitucion { InstitucionId = institucionId.Trim().ToUpper(), Orden = _institucionesParticipantes.Count });
    }

    /// <summary>Registra un participante desde el formulario público de auto-registro.</summary>
    public Asistente RegistrarAsistente(string nombre, string? cargo, string? institucion,
        string? departamento, string? correo, string? telefono, string? institucionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        var a = new Asistente
        {
            Nombre = nombre.Trim(), Cargo = cargo?.Trim(),
            InstitucionId = institucionId, Institucion = institucion?.Trim(),
            Departamento = departamento?.Trim(), Correo = correo?.Trim().ToLowerInvariant(), Telefono = telefono?.Trim(),
            AutoRegistro = true, RegistradoEl = DateTime.UtcNow
        };
        _asistentes.Add(a);
        NumAsistentes = _asistentes.Count;
        return a;
    }

    /// <summary>Pre-registra un invitado por el organizador antes de la reunión.
    /// El pre-registro queda pendiente hasta que el invitado confirme su asistencia (presencialmente o por QR).</summary>
    public Asistente PreRegistrar(string nombre, string? cargo, string? institucion,
        string? departamento, string? correo, string? telefono, string? institucionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        var a = new Asistente
        {
            Nombre = nombre.Trim(), Cargo = cargo?.Trim(),
            InstitucionId = institucionId, Institucion = institucion?.Trim(),
            Departamento = departamento?.Trim(), Correo = correo?.Trim().ToLowerInvariant(),
            Telefono = telefono?.Trim(), EsPreregistro = true
        };
        _asistentes.Add(a);
        NumAsistentes = _asistentes.Count;
        return a;
    }

    /// <summary>Confirma o marca como ausente un asistente pre-registrado.</summary>
    public void ConfirmarAsistencia(int asistenteId, bool asistio)
    {
        var a = _asistentes.FirstOrDefault(x => x.Id == asistenteId);
        if (a is not null) a.Confirmado = asistio;
    }

    /// <summary>Elimina un asistente puntual (gestión de la lista en vivo) sin tocar el resto.</summary>
    public void EliminarAsistente(int asistenteId)
    {
        var a = _asistentes.FirstOrDefault(x => x.Id == asistenteId);
        if (a is not null) { _asistentes.Remove(a); NumAsistentes = _asistentes.Count; }
    }

    /// <summary>Fija la transacción del envío (la misma en cada reintento) y si lleva Teams.</summary>
    public Guid PrepararEnvioOutlook(bool conTeams)
    {
        if (EstaEnOutlook)
            throw new DomainException("La reunión ya está en Outlook: los cambios se envían al guardar.");
        if (!AdmiteOutlook)
            throw new DomainException("Solo las reuniones virtuales o híbridas se envían a Outlook. " +
                                      "Cambie la modalidad en «Generales» y guarde antes de enviarla.");

        OutlookConTeams = conTeams;
        OutlookTransaccionId ??= Guid.NewGuid();
        return OutlookTransaccionId.Value;
    }

    public void RegistrarEnvioOutlook(string eventId, Guid organizadorId, string? enlaceTeams, string huella, DateTime utcAhora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(huella);
        if (organizadorId == Guid.Empty) throw new ArgumentException("La cita necesita organizador.", nameof(organizadorId));

        OutlookEventId       = eventId;
        OutlookOrganizadorId = organizadorId;
        RegistrarSincronizacionOutlook(enlaceTeams, huella, utcAhora);
    }

    public void RegistrarSincronizacionOutlook(string? enlaceTeams, string huella, DateTime utcAhora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(huella);

        OutlookEnlaceTeams    = enlaceTeams ?? OutlookEnlaceTeams;
        OutlookHuella         = huella;
        OutlookSincronizadaEl = utcAhora;
        OutlookPendiente      = false;
        OutlookError          = null;
    }

    public void MarcarOutlookPendiente(string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);

        OutlookPendiente = true;
        OutlookError     = motivo.Length > 500 ? motivo[..500] : motivo;
    }

    /// <summary>Quita el aviso sin haber mandado nada: la huella y la fecha de sincronización siguen
    /// siendo las de la última vez que la cita se envió de verdad.</summary>
    public void DescartarOutlookPendiente()
    {
        OutlookPendiente = false;
        OutlookError     = null;
    }
}

/// <summary>
/// Archivo o enlace que respalda una reunión: un documento del cierre o evidencia adicional.
///
/// <para><b>Por qué el enlace es una clase y no una carencia.</b> El portal guarda documentos
/// —actas firmadas, presentaciones, hojas de cálculo— porque pesan poco y conviene que vivan con
/// la reunión. El video no: un clip de celular se come en una sola reunión lo que cien documentos,
/// y el disco del servidor es el mismo que sostiene la base. Así que el video —y cualquier archivo
/// pesado— se deja en OneDrive y acá se guarda solo su enlace. De ahí que
/// <see cref="ClaseAdjuntoReunion"/> sea parte del modelo y no un detalle de la pantalla: una fila
/// tiene <see cref="Url"/> apuntando dentro de <c>/uploads/reuniones</c> o apuntando afuera, y el
/// acta las pinta distinto —las primeras pasan por un handler con sesión, las segundas son un
/// <c>href</c> que se abre en otra pestaña.</para>
///
/// <para>Se reemplazan en bloque como el resto de los hijos de <see cref="Reunion"/>:
/// <see cref="Reunion.LimpiarHijos"/> y luego <see cref="Reunion.Agregar(ReunionAdjunto)"/>.</para>
/// </summary>
public sealed class ReunionAdjunto : BaseEntity
{
    public int ReunionId { get; set; }
    public int Orden     { get; set; }

    /// <summary>En qué tarjeta del cierre se captura y se muestra.</summary>
    public SeccionAdjuntoReunion Seccion { get; private set; }
    /// <summary>Archivo guardado en el portal o enlace externo.</summary>
    public ClaseAdjuntoReunion   Clase   { get; private set; }

    /// <summary>Lo que se lee en la lista y en el acta. Obligatorio: una fila sin nombre es una
    /// fila que nadie va a abrir.</summary>
    public string  Titulo        { get; private set; } = default!;
    public string? Descripcion   { get; private set; }

    /// <summary>Nombre original del archivo subido; <c>null</c> en los enlaces.</summary>
    public string? NombreArchivo { get; private set; }

    /// <summary>Ruta relativa dentro de <c>/uploads/reuniones</c> en los archivos, o la dirección
    /// externa en los enlaces.</summary>
    public string  Url           { get; private set; } = default!;

    /// <summary>Tamaño en bytes del archivo subido; 0 en los enlaces (el portal no los guarda).</summary>
    public long    Tamano        { get; private set; }

    private ReunionAdjunto() { }

    /// <summary>Adjunto de archivo. <paramref name="url"/> es la ruta que devolvió el guardado en
    /// disco, no una dirección que haya escrito nadie.</summary>
    public static ReunionAdjunto ParaArchivo(
        SeccionAdjuntoReunion seccion, int orden,
        string titulo, string? descripcion,
        string nombreArchivo, string url, long tamano)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreArchivo);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        return new ReunionAdjunto
        {
            Seccion       = seccion,
            Clase         = ClaseAdjuntoReunion.Archivo,
            Orden         = orden,
            Titulo        = ExigirTitulo(titulo),
            Descripcion   = Limpiar(descripcion),
            NombreArchivo = nombreArchivo.Trim(),
            Url           = url.Trim(),
            Tamano        = tamano < 0 ? 0 : tamano
        };
    }

    /// <summary>Adjunto de enlace. La dirección la escribe una persona, así que se valida acá:
    /// ver <see cref="NormalizarEnlace"/>.</summary>
    public static ReunionAdjunto ParaEnlace(
        SeccionAdjuntoReunion seccion, int orden,
        string titulo, string? descripcion, string? url) =>
        new()
        {
            Seccion     = seccion,
            Clase       = ClaseAdjuntoReunion.Enlace,
            Orden       = orden,
            Titulo      = ExigirTitulo(titulo),
            Descripcion = Limpiar(descripcion),
            Url         = NormalizarEnlace(url),
            Tamano      = 0
        };

    private static string ExigirTitulo(string titulo)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            throw new DomainException("Cada documento o enlace necesita un nombre.");
        return titulo.Trim();
    }

    private static string? Limpiar(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>
    /// Valida y normaliza la dirección de un enlace.
    ///
    /// <para><b>Esto no es cosmética.</b> El acta pinta esta dirección dentro de un <c>href</c>, y
    /// el acta la ven más personas de las que pueden editar la reunión. Un <c>javascript:</c>
    /// guardado acá se ejecuta en el navegador de quien la abra, y un <c>file:</c> o una ruta de red
    /// apuntan al equipo del lector y no al origen que quiso quien lo escribió. Solo pasan
    /// <c>http</c> y <c>https</c> absolutas.</para>
    /// </summary>
    public static string NormalizarEnlace(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new DomainException("El enlace no puede quedar vacío. Pegue la dirección completa, por ejemplo https://…");

        var limpia = url.Trim();

        if (!Uri.TryCreate(limpia, UriKind.Absolute, out var uri))
            throw new DomainException($"«{limpia}» no es una dirección válida. Pegue la dirección completa, incluyendo https://");

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new DomainException($"Solo se aceptan enlaces http o https; «{uri.Scheme}» no se permite.");

        return uri.ToString();
    }
}

/// <summary>Institución convocada a una reunión (lista acumulable). Restringe qué instituciones
/// pueden seleccionarse al pasar asistencia (auto-registro y directorio del organizador).</summary>
public sealed class ReunionInstitucion : BaseEntity
{
    public int    ReunionId     { get; set; }
    /// <summary>FK a Instituciones.Id (string). Coincide con el PK nvarchar(120) de la tabla Instituciones.</summary>
    public string InstitucionId { get; set; } = default!;
    public int    Orden         { get; set; }
}

public sealed class Asistente : BaseEntity
{
    public int       ReunionId    { get; set; }
    public string    Nombre       { get; set; } = default!;
    public string?   Cargo        { get; set; }
    /// <summary>Id del catálogo de instituciones; null cuando la institución es texto libre (legado u "Otra").</summary>
    public string?   InstitucionId { get; set; }
    /// <summary>Snapshot del nombre de la institución para visualización.</summary>
    public string?   Institucion  { get; set; }
    public string?   Departamento { get; set; }
    public string?   Correo       { get; set; }
    public string?   Telefono     { get; set; }

    // ── Trazabilidad del auto-registro ────────────────────────────
    public bool      AutoRegistro  { get; set; }
    public DateTime? RegistradoEl  { get; set; }

    // ── Pre-registro ──────────────────────────────────────────────
    /// <summary>true = fue pre-registrado por el organizador antes de la reunión.</summary>
    public bool  EsPreregistro { get; set; }
    /// <summary>null = pendiente de confirmar; true = asistió; false = ausente.</summary>
    public bool? Confirmado    { get; set; }
}

public sealed class AcuerdoReunion : BaseEntity
{
    public int       ReunionId   { get; set; }
    public int       Orden       { get; set; }
    public string    Compromiso  { get; set; } = default!;
    /// <summary>Contacto del directorio responsable del compromiso; null cuando es texto libre
    /// (institución, persona fuera del directorio o dato legado).</summary>
    public int?      ResponsableContactoId { get; set; }
    /// <summary>Snapshot del nombre del responsable para visualización.</summary>
    public string?   Responsable { get; set; }
    public DateOnly? Plazo       { get; set; }

    // ── Expediente / Trámites vinculados ─────────────────────────
    public int?      ExpedienteId  { get; set; }
    public int?      TramiteIndex  { get; set; }
    public string?   TramiteNombre { get; set; }

    // ── Seguimiento ───────────────────────────────────────────────
    public EstadoCompromiso Estado            { get; set; } = EstadoCompromiso.Pendiente;
    public DateOnly?        FechaCumplimiento { get; set; }
    public string?          NotaSeguimiento   { get; set; }
    public DateTime?        SeguimientoActualizadoEl  { get; set; }
    public string?          SeguimientoActualizadoPor { get; set; }

    public ICollection<ComentarioCompromiso> Comentarios { get; set; } = [];

    /// <summary>True si el plazo ya venció y el compromiso sigue abierto.</summary>
    public bool EstaVencido(DateOnly hoy) =>
        Plazo is { } p && p < hoy &&
        Estado is EstadoCompromiso.Pendiente or EstadoCompromiso.EnProgreso or EstadoCompromiso.Reprogramado;

    /// <summary>True si el plazo está a 3 días o menos de vencer y el compromiso sigue abierto.</summary>
    public bool EstaProximoAVencer(DateOnly hoy) =>
        Plazo is { } p && p >= hoy && p <= hoy.AddDays(3) &&
        Estado is EstadoCompromiso.Pendiente or EstadoCompromiso.EnProgreso or EstadoCompromiso.Reprogramado;

    /// <summary>Aplica un cambio de seguimiento. Al marcar Cumplido sin fecha, registra hoy.</summary>
    public void ActualizarSeguimiento(EstadoCompromiso estado, DateOnly? fechaCumplimiento, string? nota, string? actor)
    {
        Estado = estado;
        FechaCumplimiento = estado == EstadoCompromiso.Cumplido
            ? (fechaCumplimiento ?? DateOnly.FromDateTime(DateTime.Today))
            : fechaCumplimiento;
        NotaSeguimiento = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim();
        SeguimientoActualizadoEl  = DateTime.UtcNow;
        SeguimientoActualizadoPor = actor;
    }
}
