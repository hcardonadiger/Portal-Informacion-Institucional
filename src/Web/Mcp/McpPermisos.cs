using Microsoft.AspNetCore.Authorization;

namespace Diger.TramitesEstado.Web.Mcp;

/// <summary>
/// Lanzada cuando una herramienta MCP se pide sin el permiso que exige. El SDK la convierte
/// en un resultado de error legible para el modelo, en vez de un 500 sin explicación.
/// </summary>
public sealed class McpPermisoDenegadoException(string mensaje) : Exception(mensaje);

/// <summary>
/// La puerta de los permisos para las herramientas MCP.
///
/// Es el espejo de <c>PermissionPageFilter</c>, que no puede servir acá porque es un filtro
/// de Razor Pages y estas herramientas no son páginas. Consulta exactamente la misma matriz
/// —la policy dinámica que resuelve <c>PermissionPolicyProvider</c> contra
/// <c>IPermissionCache</c>—, así que quitar un permiso en <c>/Accesos/Permisos</c> surte
/// efecto sobre los tokens vivos igual que sobre las sesiones vivas: la concesión no va
/// horneada en la credencial.
///
/// Lo que NO se verifica acá es el alcance institucional, y es a propósito: lo aplican los
/// query filters del <c>AppDbContext</c>, porque los claims del token son los mismos que los
/// de la cookie. Esa es la columna vertebral del diseño.
/// </summary>
public sealed class McpPermisos(
    IHttpContextAccessor accessor,
    IAuthorizationService authz,
    ICurrentUserService currentUser)
{
    /// <summary>Exige una clave concreta (ej. «Proyectos.Editar»).</summary>
    public async Task ExigirAsync(string clave)
    {
        var user = accessor.HttpContext?.User
            ?? throw new McpPermisoDenegadoException("La petición no tiene una identidad asociada.");

        var resultado = await authz.AuthorizeAsync(user, clave);
        if (!resultado.Succeeded)
            throw new McpPermisoDenegadoException(
                $"Su cuenta no tiene el permiso «{clave}», que es el que hace falta para esta acción. " +
                "Pídaselo a un administrador del portal en Accesos → Permisos.");
    }

    /// <summary>Igual que <see cref="ExigirAsync"/>, más el bloqueo de solo lectura.
    /// El <c>AppDbContext</c> también lo frena en SaveChanges como última línea, pero acá
    /// el mensaje se puede explicar; allá sale como excepción.</summary>
    public async Task ExigirEscrituraAsync(string clave)
    {
        if (currentUser.EsSoloLectura)
            throw new McpPermisoDenegadoException(
                "Su rol es de solo lectura: puede consultar el portal, pero no modificarlo.");

        await ExigirAsync(clave);
    }

    /// <summary>Sin excepción: para decidir qué herramientas mostrar en el listado.</summary>
    public async Task<bool> PuedeAsync(string clave)
    {
        var user = accessor.HttpContext?.User;
        if (user is null) return false;

        return (await authz.AuthorizeAsync(user, clave)).Succeeded;
    }
}
