namespace Diger.TramitesEstado.Infrastructure.Security;

/// <summary>
/// Declara que un handler requiere sesión iniciada pero NO un permiso concreto de la matriz.
///
/// Es el tercer caso, distinto de [Permission] y de [AllowAnonymous]: las páginas de
/// autoservicio (mi perfil, mi contraseña, mi certificado, mis notificaciones, cambiar de
/// contexto) las tiene que poder usar cualquier usuario autenticado, sea cual sea su rol.
/// Gatearlas con un permiso sería un pie en la puerta: bastaría desmarcar una casilla en la
/// matriz para dejar a un rol sin poder cambiar su propia contraseña.
///
/// Existe además para que PermissionCatalogSyncService pueda distinguir "decidimos que acá
/// no va permiso" de "a alguien se le olvidó declararlo" — sin este marcador las dos cosas
/// se ven igual en el log de advertencias, que es justo lo que hace que las advertencias
/// dejen de leerse.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PermisoNoRequeridoAttribute(string razon) : Attribute
{
    /// <summary>Por qué esta página no lleva permiso. Obligatorio: obliga a justificar la excepción.</summary>
    public string Razon { get; } = razon;
}
