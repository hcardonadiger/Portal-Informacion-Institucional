namespace Diger.TramitesEstado.Infrastructure.Security;

/// <summary>
/// Marca un PageModel (nivel página) o un handler OnGet/OnPost específico (nivel acción)
/// con el permiso requerido: un módulo más una de las cuatro acciones del vocabulario fijo
/// (Ver/Crear/Editar/Eliminar). La clave resultante es "Modulo.Accion" — por ejemplo
/// ("Expedientes", Crear) ⇒ "Expedientes.Crear".
///
/// Cuando hace falta granularidad más fina que las 4 acciones, se usa un módulo más
/// específico en vez de inventar verbos: p.ej. "Usuarios.Contrasenas" con Editar, que es
/// distinto de "Usuarios" con Editar. Así la matriz de administración se mantiene legible
/// como módulo × CRUD.
///
/// PermissionCatalogSyncService lo descubre por reflexión al arrancar; PermissionPageFilter
/// y las policies dinámicas de PermissionPolicyProvider lo exigen en cada request.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PermissionAttribute(string modulo, AccionModulo accion, string? descripcion = null) : Attribute
{
    public string       Modulo { get; } = modulo;
    public AccionModulo Accion { get; } = accion;

    /// <summary>Clave estable del permiso, tal como se guarda en Permisos y RolPermisos.</summary>
    public string Clave => $"{Modulo}.{Accion}";

    /// <summary>Texto mostrado en la matriz de administración.</summary>
    public string Nombre { get; } = descripcion ?? $"{accion} en {modulo}";
}
