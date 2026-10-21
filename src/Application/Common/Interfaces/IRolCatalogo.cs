namespace Diger.TramitesEstado.Application.Common.Interfaces;

/// <summary>Capacidades efectivas de un rol, resueltas desde la tabla Roles.</summary>
public sealed record RolInfo(
    string       Codigo,
    string       Nombre,
    NivelAlcance NivelAlcance,
    bool         EsAdministrador,
    bool         EsSoloLectura,
    bool         EsSupervisor,
    bool         EsTecnicoSoporte,
    bool         EsJefeDeArea,
    bool         EsPmo,
    string?      Color);

/// <summary>
/// Catálogo de roles en memoria, con lectura SÍNCRONA. Es síncrono por necesidad, no por
/// optimización: AppDbContext necesita el nivel de alcance en su constructor para armar los
/// filtros RLS, donde no se puede hacer una consulta async — y consultarlo con el propio
/// AppDbContext sería circular.
///
/// Se carga al arrancar (RolCatalogoLoader) y se recarga cuando cambia la administración de
/// roles, así que ajustar un rol aplica sin necesidad de que los usuarios vuelvan a entrar
/// (el nivel de alcance NO se hornea en la cookie).
/// </summary>
public interface IRolCatalogo
{
    /// <summary>Capacidades del rol, o null si el código no existe o está inactivo.</summary>
    RolInfo? Obtener(string? codigo);

    /// <summary>Todos los roles activos, ordenados por nivel de alcance y nombre.</summary>
    IReadOnlyList<RolInfo> Activos();

    /// <summary>Recarga desde la base. Llamar tras crear/editar/eliminar un rol.</summary>
    Task RecargarAsync(CancellationToken ct = default);
}

public static class RolCatalogoExtensions
{
    /// <summary>
    /// Roles cuyas concesiones son configurables desde las matrices de administración.
    /// Los roles con EsAdministrador quedan fuera deliberadamente: aprueban por código
    /// (ver PermissionAuthorizationHandler / AccesoModulosService), nunca dependen de una
    /// fila en RolPermisos ni en RolModuloAccesos, así que no se puede dejar al portal sin
    /// nadie capaz de deshacer un cambio de matriz.
    /// </summary>
    public static IReadOnlyList<RolInfo> Configurables(this IRolCatalogo catalogo) =>
        catalogo.Activos().Where(r => !r.EsAdministrador).ToList();
}
