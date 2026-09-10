using Microsoft.AspNetCore.Authorization;

namespace Diger.TramitesEstado.Infrastructure.Security;

/// <summary>
/// Resuelve un PermissionRequirement contra el rol activo del usuario (leído en vivo del
/// claim "diger:rol" vía ICurrentUserService — no una cookie horneada al login) y la
/// caché de permisos por rol. Un rol con EsAdministrador aprueba siempre por código, sin
/// depender de filas en RolPermisos — no se puede dejar al portal sin administrador.
/// </summary>
public sealed class PermissionAuthorizationHandler(ICurrentUserService currentUser, IPermissionCache cache)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var rolId = currentUser.Rol;
        if (string.IsNullOrWhiteSpace(rolId))
            return;

        if (currentUser.EsGlobal)
        {
            context.Succeed(requirement);
            return;
        }

        var permisos = await cache.ObtenerAsync(rolId);
        if (permisos.Contains(requirement.Clave))
            context.Succeed(requirement);
    }
}
