namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Concesión de acceso de un rol a un módulo/opción del portal. La presencia de la fila
/// significa "permitido". El Administrador siempre tiene acceso (no se almacena ni se puede quitar).
/// </summary>
public sealed class RolModuloAcceso : BaseEntity
{
    public string RolId  { get; set; } = default!;
    public string Modulo { get; set; } = default!;

    private RolModuloAcceso() { }

    public static RolModuloAcceso Crear(string rolId, string modulo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rolId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modulo);
        return new RolModuloAcceso { RolId = rolId.Trim(), Modulo = modulo.Trim() };
    }
}
