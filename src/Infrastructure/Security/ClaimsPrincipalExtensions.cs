using System.Security.Claims;
using Diger.TramitesEstado.Domain.Enums;

namespace Diger.TramitesEstado.Infrastructure.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Determina si el usuario activo tiene permisos para mutar datos (Crear, Editar, Eliminar).
    /// El rol "Consultor" es de solo lectura y no puede mutar datos.
    /// </summary>
    public static bool CanMutate(this ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true) return false;
        
        var rol = user.FindFirstValue(AppClaims.ActiveRol);
        
        // Si el rol es explícitamente Consultor, denegar mutación.
        return !string.Equals(rol, nameof(RolUsuario.Consultor), StringComparison.OrdinalIgnoreCase);
    }
}
