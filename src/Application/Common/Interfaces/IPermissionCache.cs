namespace Diger.TramitesEstado.Application.Common.Interfaces;

/// <summary>
/// Caché de permisos por rol. Deliberadamente NO es por usuario ni se hornea en la cookie
/// de login, a diferencia de la implementación de referencia que se revisó (SGSEC): así la
/// revocación de un permiso aplica a todas las sesiones activas de ese rol en cuanto se
/// invalida la entrada, no cuando la sesión expire o el usuario vuelva a entrar.
/// Implementada en Infrastructure (PermissionCache); la interfaz vive en Application para
/// que los handlers puedan invalidarla sin depender de Infrastructure.
/// </summary>
public interface IPermissionCache
{
    /// <summary>
    /// Claves efectivas del rol dentro de un ámbito: las de la matriz rol×permiso, menos las de
    /// los módulos que <c>ModuloAmbitos</c> haya limitado a otras áreas o unidades.
    ///
    /// <para>El ámbito es parte de la pregunta y no un filtro posterior a propósito: el navbar y
    /// <c>PermissionAuthorizationHandler</c> llaman los dos acá, así que componer en un solo
    /// lugar es lo que impide que el menú y el bloqueo real se contradigan.</para>
    /// </summary>
    Task<HashSet<string>> ObtenerAsync(
        string rolId, string? institucionId, string? areaId, string? unidadId,
        CancellationToken ct = default);

    /// <summary>Invalida la entrada de un rol — llamar justo después de guardar cambios
    /// en la matriz de permisos de ese rol.</summary>
    void Invalidar(string rolId);

    /// <summary>Invalida todas las entradas — usar tras cambios en el catálogo de roles
    /// o de permisos que puedan afectar a varios roles a la vez.</summary>
    void InvalidarTodo();
}
