using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Diger.TramitesEstado.Infrastructure.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Determina si el usuario activo puede mutar datos (Crear, Editar, Eliminar).
    /// Lo decide la capacidad EsSoloLectura del rol en la tabla Roles — antes era el rol
    /// "Consultor" hardcodeado. Se resuelve por request (no se hornea en la cookie), así
    /// que marcar un rol como solo lectura aplica sin necesidad de volver a entrar.
    /// Es solo una ayuda de UI: los bloqueos duros están en ConsultorReadOnlyPageFilter
    /// y en AppDbContext.SaveChangesAsync.
    /// </summary>
    public static bool CanMutate(this HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true) return false;
        return !context.RequestServices.GetRequiredService<ICurrentUserService>().EsSoloLectura;
    }
}
