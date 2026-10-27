namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Una estructura de proyecto guardada para volver a usarla.
///
/// <para>Existe por los proyectos CÍCLICOS: los que se repiten cada año o cada
/// convocatoria con los mismos entregables y las mismas actividades, y que hoy
/// obligan a recapturar el árbol entero desde cero.</para>
///
/// <para>Guarda SOLO el esqueleto —qué entregables, qué actividades y en qué
/// orden—. No lleva fechas, ni estados, ni avances, ni responsables: eso es del
/// proyecto concreto y cambia en cada ciclo. Una plantilla que guardara fechas
/// las traería caducadas a la siguiente edición, que es peor que no traer nada.</para>
///
/// <para>El reparto de visibilidad reusa <see cref="Enums.NivelAlcance"/>, el
/// mismo vocabulario con el que el portal reparte permisos y datos, en vez de
/// inventar uno propio. Ver <see cref="Alcanza"/>.</para>
/// </summary>
public sealed class PlantillaProyecto : BaseAuditableEntity
{
    public bool IsDeleted { get; set; }

    public string  Nombre      { get; private set; } = default!;
    public string? Descripcion { get; private set; }
    public bool    Activa      { get; private set; } = true;

    // ── Visibilidad ──────────────────────────────────────────────────
    // El nivel dice HASTA DÓNDE llega; los tres identificadores, DESDE
    // dónde. InstitucionId siempre tiene valor —toda plantilla nace en una
    // institución— y los otros dos solo cuando el nivel los usa.
    public NivelAlcance Nivel { get; private set; } = NivelAlcance.Institucion;
    public string  InstitucionId { get; private set; } = default!;
    public string? AreaId        { get; private set; }
    public string? UnidadId      { get; private set; }

    // ── Clasificación ────────────────────────────────────────────────
    // Los mismos dos catálogos de la ficha de proyecto, para que el alta
    // pueda ofrecer solo las plantillas que encajan con el proyecto que se
    // está creando. Ambos opcionales: el null dice «sirve para cualquiera»,
    // y una plantilla sin clasificar aparece siempre en vez de esconderse.
    public int? CategoriaId { get; private set; }
    public CategoriaProyecto? CategoriaRef { get; private set; }
    public AccionProyecto? Accion { get; private set; }

    private readonly List<PlantillaEntregable> _entregables = [];
    public IReadOnlyCollection<PlantillaEntregable> Entregables => _entregables.AsReadOnly();

    private PlantillaProyecto() { }

    public static PlantillaProyecto Crear(
        string nombre, string institucionId, NivelAlcance nivel,
        string? areaId = null, string? unidadId = null, string? descripcion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(institucionId);

        var p = new PlantillaProyecto
        {
            Nombre        = nombre.Trim(),
            Descripcion   = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim(),
            InstitucionId = institucionId.Trim().ToUpperInvariant()
        };
        p.FijarAlcance(nivel, areaId, unidadId);
        return p;
    }

    public void Renombrar(string nombre, string? descripcion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        Nombre      = nombre.Trim();
        Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
    }

    public void Activar(bool activa) => Activa = activa;

    /// <summary>Para qué categoría y qué acción sirve. Null en cualquiera de los dos = para todas.</summary>
    public void Clasificar(int? categoriaId, AccionProyecto? accion)
    {
        CategoriaId = categoriaId is > 0 ? categoriaId : null;
        Accion      = accion;
    }

    /// <summary>
    /// Fija hasta dónde se ve la plantilla, limpiando lo que el nivel no usa.
    /// </summary>
    /// <remarks>
    /// Limpiar es parte del contrato, no una cortesía: una plantilla de nivel
    /// Institución que conservara un AreaId de cuando fue de área dejaría un
    /// dato que no manda nada pero que al leerlo parece mandar. Y bajar de
    /// nivel sin área o unidad es un error, no un caso por defecto: una
    /// plantilla "de área" sin área no se vería desde ninguna parte.
    /// </remarks>
    public void FijarAlcance(NivelAlcance nivel, string? areaId, string? unidadId)
    {
        if (nivel is not (NivelAlcance.Institucion or NivelAlcance.Area or NivelAlcance.Unidad))
            throw new DomainException(
                $"El alcance de una plantilla es Institución, Área o Unidad; «{nivel}» no.");

        if (nivel == NivelAlcance.Area && string.IsNullOrWhiteSpace(areaId))
            throw new DomainException("Una plantilla de área necesita el área a la que pertenece.");

        if (nivel == NivelAlcance.Unidad && string.IsNullOrWhiteSpace(unidadId))
            throw new DomainException("Una plantilla de unidad necesita la unidad a la que pertenece.");

        Nivel    = nivel;
        AreaId   = nivel is NivelAlcance.Area or NivelAlcance.Unidad ? areaId?.Trim() : null;
        UnidadId = nivel is NivelAlcance.Unidad ? unidadId?.Trim() : null;
    }

    /// <summary>
    /// ¿Esta plantilla alcanza a quien está parado en este ámbito?
    /// </summary>
    /// <remarks>
    /// La regla se lee de arriba abajo: una plantilla de institución la ve toda
    /// la institución; una de área, solo quien esté en esa área; una de unidad,
    /// solo quien esté en esa unidad. Nunca al revés —estar en un área no da
    /// acceso a las plantillas de las unidades de abajo— porque el nivel dice
    /// QUIÉN la necesita, no quién manda sobre ella.
    ///
    /// Vive en la entidad y no en la consulta para que la regla sea una sola:
    /// la consulta la traduce a SQL, y las pruebas la ejercitan acá sin base.
    /// </remarks>
    public bool Alcanza(string? institucionId, string? areaId, string? unidadId)
    {
        if (!string.Equals(InstitucionId, institucionId, StringComparison.OrdinalIgnoreCase))
            return false;

        return Nivel switch
        {
            NivelAlcance.Institucion => true,
            NivelAlcance.Area        => string.Equals(AreaId, areaId, StringComparison.OrdinalIgnoreCase),
            NivelAlcance.Unidad      => string.Equals(UnidadId, unidadId, StringComparison.OrdinalIgnoreCase),
            _                        => false
        };
    }

    // ── Estructura ───────────────────────────────────────────────────
    // Mismo trato que los hijos de Expediente y de Proyecto: se reemplazan en
    // bloque. Limpiar y volver a agregar evita tener que casar por id qué
    // cambió, que es de donde salen los huérfanos.
    public void LimpiarEntregables() => _entregables.Clear();
    public void Agregar(PlantillaEntregable entregable) => _entregables.Add(entregable);
}

/// <summary>Un entregable dentro de una plantilla. Nombre y orden, nada más.</summary>
public sealed class PlantillaEntregable : BaseEntity
{
    public int     PlantillaProyectoId { get; set; }
    public int     Orden               { get; set; }
    public string  Nombre              { get; set; } = default!;
    public string? Descripcion         { get; set; }

    private readonly List<PlantillaActividad> _actividades = [];
    public IReadOnlyCollection<PlantillaActividad> Actividades => _actividades.AsReadOnly();

    public void Agregar(PlantillaActividad actividad) => _actividades.Add(actividad);
}

/// <summary>Una actividad dentro de un entregable de plantilla.</summary>
public sealed class PlantillaActividad : BaseEntity
{
    public int     PlantillaEntregableId { get; set; }
    public int     Orden                 { get; set; }
    public string  Nombre                { get; set; } = default!;
    public string? Descripcion           { get; set; }
}
