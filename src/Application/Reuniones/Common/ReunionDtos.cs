namespace Diger.TramitesEstado.Application.Reuniones.Common;

// Clases mutables para enlace de modelo (Razor) y transporte al comando.
public sealed class AsistenteInput
{
    public string  Nombre       { get; set; } = string.Empty;
    public string? Cargo        { get; set; }
    /// <summary>Id del catálogo de instituciones; null = texto libre (legado).</summary>
    public string? InstitucionId { get; set; }
    public string? Institucion  { get; set; }
    public string? Departamento { get; set; }
    public string? Correo       { get; set; }
    public string? Telefono     { get; set; }

    // Origen y asistencia: viajan como campos ocultos del editor para sobrevivir al reemplazo en
    // bloque. Sin ellos, cada guardado convertía a todos en «manuales» y borraba la asistencia marcada.
    public bool      AutoRegistro  { get; set; }
    public DateTime? RegistradoEl  { get; set; }
    public bool      EsPreregistro { get; set; }
    public bool?     Confirmado    { get; set; }
    public bool      DeOutlook     { get; set; }
}

public sealed class AcuerdoInput
{
    public string    Compromiso  { get; set; } = string.Empty;
    /// <summary>Contacto del directorio responsable; null = texto libre.</summary>
    public int?      ResponsableContactoId { get; set; }
    public string?   Responsable { get; set; }
    public DateOnly? Plazo       { get; set; }

    // ── Expediente y Trámite vinculado ────────────────────────────
    public int?      ExpedienteId  { get; set; }
    public int?      TramiteIndex  { get; set; }
    public string?   TramiteNombre { get; set; }

    // ── Seguimiento (se transporta de ida y vuelta para conservarlo al reemplazar los hijos en bloque) ──
    public EstadoCompromiso Estado            { get; set; } = EstadoCompromiso.Pendiente;
    public DateOnly?        FechaCumplimiento { get; set; }
    public string?          NotaSeguimiento   { get; set; }
}

/// <summary>Una fila de la lista de documentos o de evidencia adicional del cierre.
///
/// <para>Viaja de ida y vuelta: al cargar el editor se llena desde <see cref="ReunionAdjunto"/>,
/// y al guardar vuelve con lo que haya quedado en pantalla. En los archivos ya subidos,
/// <see cref="Url"/> y <see cref="NombreArchivo"/> regresan tal cual para que reemplazar los hijos
/// en bloque no pierda el archivo que ya está en disco.</para></summary>
public sealed class ReunionAdjuntoInput
{
    /// <summary>Id de la fila guardada; 0 en una fila que todavía no se ha guardado.
    ///
    /// <para>Es por acá que el acta pide un archivo —<c>?handler=Adjunto&amp;adjuntoId=…</c>— y no
    /// por su ruta. La diferencia importa: con la ruta en la URL, cualquiera con acceso a una
    /// reunión podría pedir el archivo de otra con solo cambiarla, porque la única defensa sería
    /// que la ruta no se salga de la carpeta de subidas. Con el Id, el handler exige que el
    /// adjunto sea de <b>esa</b> reunión, que ya pasó por el filtro de alcance.</para>
    ///
    /// <para>Al guardar no se usa: los hijos se reemplazan en bloque y las filas nacen con Id
    /// nuevo.</para></summary>
    public int Id { get; set; }

    public SeccionAdjuntoReunion Seccion { get; set; } = SeccionAdjuntoReunion.Documento;
    public ClaseAdjuntoReunion   Clase   { get; set; } = ClaseAdjuntoReunion.Enlace;

    public string  Titulo        { get; set; } = string.Empty;
    public string? Descripcion   { get; set; }
    public string? NombreArchivo { get; set; }
    public string? Url           { get; set; }
    public long    Tamano        { get; set; }
}

public sealed class ReunionFormDto
{
    public string    Titulo    { get; set; } = string.Empty;
    public DateOnly? Fecha     { get; set; }
    public string?   Hora      { get; set; }
    public string?   Duracion  { get; set; }
    public string?   Modalidad { get; set; }
    public string?   Lugar     { get; set; }

    /// <summary>Solo lectura para la vista: la regla vive en ReunionMapper.Aplicar, que mira la
    /// reunión y no este campo (el formulario no lo envía y no se puede falsificar).</summary>
    public bool      EsImportadaDeOutlook { get; set; }
    public DateTime? CanceladaEnOutlookEl { get; set; }

    // ── Expediente vinculado ──────────────────────────────────────
    public int?      ExpedienteId     { get; set; }
    public string?   ExpedienteCodigo { get; set; }

    public string?   InstitucionId { get; set; }
    /// <summary>Instituciones convocadas a la reunión (acumulable). La primera queda como
    /// institución principal (alcance, acta, tablero).</summary>
    public List<string> InstitucionesIds { get; set; } = [];
    public string?   Tipo      { get; set; }
    public bool      EsCapacitacionPlataforma { get; set; }
    public VisibilidadReunion Visibilidad { get; set; } = VisibilidadReunion.Publica;

    public string? ObjetivoAgenda { get; set; }
    public string? Desarrollo     { get; set; }

    /// <summary>Tema de la capacitación. Solo lo llena el importador de Supabase —ninguna
    /// pantalla lo captura— y se muestra en la memoria del acta.</summary>
    public string? Tema        { get; set; }

    public string? EpNombre { get; set; }
    public string? EpCargo  { get; set; }
    public string? EpCorreo { get; set; }
    public string? EpTel    { get; set; }

    public string? FacNombre { get; set; }
    public string? FacCargo  { get; set; }
    public string? FacCorreo { get; set; }

    public int?    Convocados    { get; set; }
    public int?    NumAsistentes { get; set; }
    public int?    PctAsistencia { get; set; }
    public int?    SatisfaccionCalificacion { get; set; }
    public string? Satisfaccion  { get; set; }
    public string? Compromisos   { get; set; }

    public bool    EncuestaActiva { get; set; }
    public string? ValDiger     { get; set; }
    public string? ValInst      { get; set; }
    public string? DocsRecursos { get; set; }
    public string? Foto1Url  { get; set; }
    public string? Foto1Desc { get; set; }
    public string? Foto2Url  { get; set; }
    public string? Foto2Desc { get; set; }

    /// <summary>Documentos y enlaces del cierre, de las dos tarjetas. Va adentro del formulario
    /// —y no como parámetro aparte del comando, al estilo de Asistentes y Acuerdos— porque la
    /// capa Web ya resolvió lo suyo antes de llegar acá: subió los archivos a disco y acá llegan
    /// como ruta. Así ningún comando cambió de firma.</summary>
    public List<ReunionAdjuntoInput> Adjuntos { get; set; } = [];
}

/// <summary>Referencia liviana (id + nombre) a una institución convocada a una reunión.</summary>
public sealed record InstitucionRefDto(string Id, string Nombre);
