namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Limita un módulo del portal a un área o una unidad concreta.
///
/// <para><b>Abierto por defecto.</b> Un módulo sin ninguna fila está disponible para todos: la
/// tabla solo declara excepciones. Es lo que permite restringir Expedientes y SIGER sin tener
/// que configurar los veinte módulos restantes, y hace que desplegar esto no cambie nada para
/// nadie hasta que alguien cree la primera fila.</para>
///
/// <para>Se <b>compone</b> con la matriz rol×permiso, no la reemplaza: el módulo se ve cuando la
/// unidad lo tiene habilitado <i>y</i> el rol tiene la clave. Las dos preguntas son distintas
/// —dónde trabaja la persona, y qué puede hacer— y las resuelve un solo punto,
/// <c>PermissionCache</c>. Ese es el error que se cometió con <c>RolModuloAccesos</c>: dos
/// compuertas separadas que podían contradecirse, el menú escondiendo algo que la URL sí dejaba
/// abrir.</para>
///
/// <para>Restringir un módulo padre restringe también a sus submódulos:
/// limitar <c>Siger</c> limita <c>Siger.Conciliacion</c>, sin necesidad de repetir la fila.</para>
/// </summary>
public sealed class ModuloAmbito : BaseAuditableEntity
{
    /// <summary>Clave del módulo tal como la usa el catálogo de permisos ("Expedientes",
    /// "Siger", "Tableros.Digitalizacion").</summary>
    public string Modulo { get; private set; } = default!;

    /// <summary>Institución dueña del ámbito. Siempre presente: es el ancla, igual que en el
    /// resto de los filtros de alcance.</summary>
    public string InstitucionId { get; private set; } = default!;

    /// <summary>Área habilitada. Null = toda la institución.</summary>
    public string? AreaId { get; private set; }

    /// <summary>Unidad habilitada. Null = toda el área.</summary>
    public string? UnidadId { get; private set; }

    private ModuloAmbito() { }   // EF

    public static ModuloAmbito Crear(string modulo, string institucionId, string? areaId, string? unidadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modulo);
        ArgumentException.ThrowIfNullOrWhiteSpace(institucionId);

        // Una unidad sin área no se puede resolver: la unidad cuelga del área, y sin ella no hay
        // forma de saber a qué rama de la jerarquía pertenece la concesión.
        var area   = string.IsNullOrWhiteSpace(areaId)   ? null : areaId.Trim();
        var unidad = string.IsNullOrWhiteSpace(unidadId) ? null : unidadId.Trim();
        if (unidad is not null && area is null)
            throw new DomainException("Para habilitar una unidad hay que indicar también su área.");

        return new ModuloAmbito
        {
            Modulo        = modulo.Trim(),
            InstitucionId = institucionId.Trim(),
            AreaId        = area,
            UnidadId      = unidad
        };
    }

    /// <summary>¿Esta concesión alcanza al ámbito del usuario? De lo más general a lo más
    /// específico: una fila sin área cubre toda la institución; una con área y sin unidad cubre
    /// toda el área.</summary>
    public bool Alcanza(string? institucionId, string? areaId, string? unidadId) =>
        string.Equals(InstitucionId, institucionId, StringComparison.OrdinalIgnoreCase)
        && (AreaId   is null || string.Equals(AreaId,   areaId,   StringComparison.OrdinalIgnoreCase))
        && (UnidadId is null || string.Equals(UnidadId, unidadId, StringComparison.OrdinalIgnoreCase));

    public const int MaxModulo = 120;
}
