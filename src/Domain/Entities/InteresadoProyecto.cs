namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Qué papel juega el interesado. Sale de las dos columnas del cuadro de portafolio que no tenían
/// dónde ir: «Instituciones Responsables» —que son ejecutores y contrapartes— y «Población
/// Objetivo», que son beneficiarios.
/// </summary>
public enum RolInteresado
{
    Patrocinador,        // respalda y decide; sin él el proyecto no avanza
    Ejecutor,            // hace el trabajo
    ContraparteTecnica,  // la institución del otro lado de la mesa
    Beneficiario,        // a quién sirve el resultado
    Regulador            // autoriza, norma o fiscaliza
}

/// <summary>
/// Interesado en el proyecto: <b>siempre un usuario del portal</b>, con su rol e influencia.
///
/// <para>Resuelve dos límites del modelo anterior. Uno: <see cref="Proyecto.Responsable"/> es un
/// campo único, y ya hubo que meter dos nombres separados por barra cuando un proyecto lo llevaban
/// dos personas. Dos: la institución del proyecto es una sola —el ancla del filtro de alcance— pero
/// en la práctica intervienen varias, y perderlas dejaba sin registrar quién más está sentado en
/// la mesa.</para>
///
/// <para><b>Ser interesado da acceso al proyecto</b> (2026-08-24). El filtro de alcance de
/// <see cref="Proyecto"/> tiene una rama para esto, igual que la del responsable: un interesado ve
/// el proyecto completo aunque caiga fuera de su institución, área o unidad. Esa es la razón de que
/// <see cref="UsuarioId"/> sea obligatorio y no un vínculo opcional como antes — un interesado sin
/// cuenta no podría ver nada, así que registrarlo como interesado no significaría nada.</para>
///
/// <para>La consecuencia a tener presente: <b>no se puede registrar como interesado a alguien que
/// no tenga usuario</b>. Para un organismo sin cuenta —BID, PNUD, una cámara— o una contraparte de
/// una institución que todavía no está en el portal, primero hay que crear el usuario. Es
/// deliberado: la alternativa era una lista de nombres sueltos que no abre ninguna puerta.</para>
///
/// <para>Tabla independiente, no navegación del agregado, igual que el resto de los hijos de
/// <see cref="Proyecto"/>.</para>
/// </summary>
public sealed class InteresadoProyecto : BaseEntity
{
    public int ProyectoId { get; private set; }

    /// <summary>Usuario del portal. Obligatorio: es lo que convierte al interesado en alguien que
    /// efectivamente puede abrir el proyecto.</summary>
    public Guid UsuarioId { get; private set; }

    /// <summary>Snapshot del nombre del usuario al momento de registrarlo, con el mismo criterio
    /// que <see cref="Proyecto.Responsable"/>: el listado y el histórico no dependen de un join, y
    /// si la cuenta se renombra o se da de baja el registro sigue siendo legible.</summary>
    public string Nombre { get; private set; } = default!;

    /// <summary>Snapshot del correo del usuario. Misma razón que <see cref="Nombre"/>.</summary>
    public string? Correo { get; private set; }

    /// <summary>Institución u organización por la que participa. Se propone desde la asignación del
    /// usuario pero queda editable: alguien puede sentarse en la mesa representando a otra parte.</summary>
    public string? Institucion { get; private set; }

    /// <summary>Cargo con el que participa. Manual: el portal no lo conoce.</summary>
    public string? Cargo { get; private set; }

    public RolInteresado    Rol        { get; private set; }
    public NivelCualitativo Influencia { get; private set; } = NivelCualitativo.Media;

    public string? Notas { get; private set; }

    public string   RegistradoPor { get; private set; } = default!;
    public DateTime RegistradoEn  { get; private set; }

    /// <summary>Interesado de alta influencia que además decide o autoriza: el que hay que tener
    /// del lado propio antes de que el proyecto se trabe.</summary>
    public bool EsClave =>
        Influencia == NivelCualitativo.Alta
        && Rol is RolInteresado.Patrocinador or RolInteresado.Regulador or RolInteresado.ContraparteTecnica;

    private InteresadoProyecto() { }   // EF

    public static InteresadoProyecto Crear(
        int              proyectoId,
        Guid             usuarioId,
        string           nombre,
        RolInteresado    rol,
        string           registradoPor,
        NivelCualitativo influencia  = NivelCualitativo.Media,
        string?          correo      = null,
        string?          institucion = null,
        string?          cargo       = null,
        string?          notas       = null)
    {
        if (proyectoId <= 0)
            throw new DomainException("El interesado debe pertenecer a un proyecto.");

        if (usuarioId == Guid.Empty)
            throw new DomainException("El interesado tiene que ser un usuario del portal.");

        return new InteresadoProyecto
        {
            ProyectoId    = proyectoId,
            UsuarioId     = usuarioId,
            Nombre        = Validar(nombre),
            Rol           = rol,
            Influencia    = influencia,
            Correo        = Limpiar(correo)?.ToLowerInvariant(),
            Institucion   = Limpiar(institucion),
            Cargo         = Limpiar(cargo),
            Notas         = Limpiar(notas),
            RegistradoPor = string.IsNullOrWhiteSpace(registradoPor) ? "—" : registradoPor.Trim(),
            RegistradoEn  = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Cambia el papel del interesado, no quién es.
    ///
    /// <para>El usuario no se edita a propósito: cambiar la persona es quitar a una y poner a otra,
    /// y hacerlo por edición saltearía la verificación de que no quede repetida en el proyecto
    /// —además de mover en silencio a quién le da acceso este registro.</para>
    /// </summary>
    public void Actualizar(
        RolInteresado    rol,
        NivelCualitativo influencia,
        string?          institucion,
        string?          cargo,
        string?          notas)
    {
        Rol         = rol;
        Influencia  = influencia;
        Institucion = Limpiar(institucion);
        Cargo       = Limpiar(cargo);
        Notas       = Limpiar(notas);
    }

    private static string Validar(string nombre)
    {
        var limpio = (nombre ?? "").Trim();
        if (limpio.Length == 0)
            throw new DomainException("El interesado necesita un nombre.");
        if (limpio.Length > MaxNombre)
            throw new DomainException($"El nombre no puede superar los {MaxNombre} caracteres.");
        return limpio;
    }

    private static string? Limpiar(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public const int MaxNombre = 200;
    public const int MaxNotas  = 1000;
}
