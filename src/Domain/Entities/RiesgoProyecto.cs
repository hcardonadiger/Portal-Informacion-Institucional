namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Escala cualitativa de tres puntos. La comparten probabilidad, impacto e
/// <see cref="InteresadoProyecto.Influencia"/>: es la misma pregunta —¿cuánto?— y tener tres enums
/// idénticos solo obligaría a convertir entre ellos.
/// </summary>
public enum NivelCualitativo { Baja = 1, Media = 2, Alta = 3 }

/// <summary>De dónde viene el riesgo. Sale de los riesgos reales del portafolio, no de un manual.</summary>
public enum CategoriaRiesgo
{
    Tecnico,        // integraciones, infraestructura, calidad del dato
    Institucional,  // capacidad, rotación, designaciones pendientes
    Normativo,      // reglamentos que exigen papel, vacíos legales
    Financiero,     // presupuesto, sostenibilidad
    Operativo,      // procesos, reprocesos, adopción
    Externo         // depende de un tercero: otra institución, un proveedor, el Congreso
}

/// <summary>Qué se decidió hacer con el riesgo. Vocabulario estándar de gestión de riesgos.</summary>
public enum EstrategiaRiesgo { Evitar, Mitigar, Transferir, Aceptar }

/// <summary>
/// Ciclo de vida del riesgo.
///
/// <para><b>Materializado</b> es el estado que conecta este registro con la bitácora: un
/// <see cref="AvanceProyecto.Bloqueo"/> es un riesgo que ya ocurrió. Sin este estado habría dos
/// listas paralelas —riesgos por un lado, bloqueos por otro— sin forma de saber si el bloqueo de
/// hoy es el riesgo que alguien anticipó hace dos meses.</para>
/// </summary>
public enum EstadoRiesgo { Abierto, EnTratamiento, Materializado, Cerrado }

/// <summary>
/// Registro de riesgos del proyecto. Tabla independiente, no navegación del agregado
/// <see cref="Proyecto"/>, por la misma razón que <see cref="AvanceProyecto"/>: lo que cuelga del
/// agregado se arrastra en sus operaciones de colección.
/// </summary>
public sealed class RiesgoProyecto : BaseEntity
{
    public int    ProyectoId  { get; private set; }
    public string Descripcion { get; private set; } = default!;

    public CategoriaRiesgo  Categoria    { get; private set; }
    public NivelCualitativo Probabilidad { get; private set; }
    public NivelCualitativo Impacto      { get; private set; }
    public EstrategiaRiesgo Estrategia   { get; private set; }
    public EstadoRiesgo     Estado       { get; private set; } = EstadoRiesgo.Abierto;

    /// <summary>Qué se va a hacer. Null mientras el riesgo solo está identificado.</summary>
    public string? Mitigacion { get; private set; }

    public Guid?   ResponsableId { get; private set; }
    /// <summary>Snapshot del nombre, mismo criterio que en el resto del módulo.</summary>
    public string? Responsable   { get; private set; }

    public DateOnly  FechaDeteccion { get; private set; }
    /// <summary>Cuándo toca volver a mirarlo. Es lo que evita que un riesgo abierto se olvide.</summary>
    public DateOnly? FechaRevision  { get; private set; }
    public DateOnly? FechaCierre    { get; private set; }

    /// <summary>Quién lo registró y cuándo, para no perder el origen de la entrada.</summary>
    public string   RegistradoPor { get; private set; } = default!;
    public DateTime RegistradoEn  { get; private set; }

    /// <summary>
    /// Severidad = probabilidad × impacto, de 1 a 9. Se calcula, no se guarda: guardarla permitiría
    /// que quedara desincronizada de sus dos factores, que es justo el error que hace inútil una
    /// matriz de riesgos.
    /// </summary>
    public int Severidad => (int)Probabilidad * (int)Impacto;

    /// <summary>Corte para el semáforo: 6+ es alto (alta×media en adelante), 3–4 medio, 1–2 bajo.</summary>
    public NivelCualitativo NivelSeveridad => Severidad >= 6 ? NivelCualitativo.Alta
                                            : Severidad >= 3 ? NivelCualitativo.Media
                                            : NivelCualitativo.Baja;

    /// <summary>Abierto o en tratamiento y con la fecha de revisión vencida.</summary>
    public bool RevisionVencida(DateOnly hoy) =>
        FechaRevision is { } f && f < hoy && Estado is EstadoRiesgo.Abierto or EstadoRiesgo.EnTratamiento;

    private RiesgoProyecto() { }   // EF

    public static RiesgoProyecto Crear(
        int              proyectoId,
        string           descripcion,
        CategoriaRiesgo  categoria,
        NivelCualitativo probabilidad,
        NivelCualitativo impacto,
        EstrategiaRiesgo estrategia,
        string           registradoPor,
        string?          mitigacion    = null,
        Guid?            responsableId = null,
        string?          responsable   = null,
        DateOnly?        fechaRevision = null)
    {
        if (proyectoId <= 0)
            throw new DomainException("El riesgo debe pertenecer a un proyecto.");

        var limpio = Validar(descripcion);
        var hoy    = DateOnly.FromDateTime(DateTime.UtcNow);

        if (fechaRevision is { } fr && fr < hoy)
            throw new DomainException("La fecha de revisión no puede ser anterior a hoy.");

        return new RiesgoProyecto
        {
            ProyectoId     = proyectoId,
            Descripcion    = limpio,
            Categoria      = categoria,
            Probabilidad   = probabilidad,
            Impacto        = impacto,
            Estrategia     = estrategia,
            Estado         = EstadoRiesgo.Abierto,
            Mitigacion     = Limpiar(mitigacion),
            ResponsableId  = responsableId,
            Responsable    = Limpiar(responsable),
            FechaDeteccion = hoy,
            FechaRevision  = fechaRevision,
            RegistradoPor  = string.IsNullOrWhiteSpace(registradoPor) ? "—" : registradoPor.Trim(),
            RegistradoEn   = DateTime.UtcNow
        };
    }

    public void Actualizar(
        string           descripcion,
        CategoriaRiesgo  categoria,
        NivelCualitativo probabilidad,
        NivelCualitativo impacto,
        EstrategiaRiesgo estrategia,
        string?          mitigacion,
        Guid?            responsableId,
        string?          responsable,
        DateOnly?        fechaRevision)
    {
        if (Estado == EstadoRiesgo.Cerrado)
            throw new DomainException("El riesgo está cerrado. Reábralo antes de modificarlo.");

        Descripcion   = Validar(descripcion);
        Categoria     = categoria;
        Probabilidad  = probabilidad;
        Impacto       = impacto;
        Estrategia    = estrategia;
        Mitigacion    = Limpiar(mitigacion);
        ResponsableId = responsableId;
        Responsable   = Limpiar(responsable);
        FechaRevision = fechaRevision;
    }

    /// <summary>
    /// Mueve el estado. Sin máquina de transiciones a propósito: un riesgo puede volver a abrirse
    /// si reaparece, y uno materializado puede cerrarse cuando se resuelve. Lo único que se fija
    /// es la fecha de cierre, que es el hecho.
    /// </summary>
    public void CambiarEstado(EstadoRiesgo nuevo)
    {
        if (nuevo == Estado) return;
        Estado      = nuevo;
        FechaCierre = nuevo == EstadoRiesgo.Cerrado ? DateOnly.FromDateTime(DateTime.UtcNow) : null;
    }

    private static string Validar(string descripcion)
    {
        var limpio = (descripcion ?? "").Trim();
        if (limpio.Length == 0)
            throw new DomainException("Hay que describir el riesgo.");
        if (limpio.Length > MaxDescripcion)
            throw new DomainException($"La descripción no puede superar los {MaxDescripcion} caracteres.");
        return limpio;
    }

    private static string? Limpiar(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public const int MaxDescripcion = 500;
    public const int MaxMitigacion  = 1000;
}
