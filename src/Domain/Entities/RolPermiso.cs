namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Concesión de un permiso de acción a un rol. La presencia de la fila significa
/// "permitido" — mismo principio que RolModuloAcceso, pero a nivel de acción
/// específica en vez de módulo completo. El Administrador siempre tiene acceso total
/// por código (ver PermissionAuthorizationHandler); nunca depende de una fila aquí.
/// </summary>
public sealed class RolPermiso : BaseEntity
{
    public string RolId        { get; set; } = default!;
    public string PermisoClave { get; set; } = default!;

    private RolPermiso() { }

    public static RolPermiso Crear(string rolId, string permisoClave)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rolId);
        ArgumentException.ThrowIfNullOrWhiteSpace(permisoClave);
        return new RolPermiso { RolId = rolId.Trim(), PermisoClave = permisoClave.Trim() };
    }
}
