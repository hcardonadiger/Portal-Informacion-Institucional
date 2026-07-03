namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Concesión de acceso de un rol a un módulo/opción del portal. La presencia de la fila
/// significa "permitido". El Administrador siempre tiene acceso (no se almacena ni se puede quitar).
/// </summary>
public sealed class RolModuloAcceso : BaseEntity
{
    public RolUsuario Rol    { get; set; }
    public string     Modulo { get; set; } = default!;

    private RolModuloAcceso() { }

    public static RolModuloAcceso Crear(RolUsuario rol, string modulo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modulo);
        return new RolModuloAcceso { Rol = rol, Modulo = modulo.Trim() };
    }
}
