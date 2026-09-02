namespace Diger.TramitesEstado.Web.Common;

/// <summary>
/// Nombres de los módulos que el navbar y la página de ayuda consultan para decidir qué
/// enlaces mostrar. Cada uno se traduce a la clave "<c>Modulo.Ver</c>" de la matriz de
/// permisos (ver <see cref="AccesoModulosService.PuedeAsync"/>).
///
/// Vivía en Application.Accesos junto a la matriz rol×módulo, que se retiró: ya no es un
/// catálogo de negocio con su propia tabla y su propia pantalla, solo la lista de enlaces
/// que el menú sabe pintar. Por eso ahora es del Web y no de Application.
/// </summary>
public static class ModulosPortal
{
    public const string Tableros    = "Tableros";
    public const string Calendario  = "Calendario";
    public const string Expedientes = "Expedientes";
    public const string Reuniones   = "Reuniones";
    public const string Tickets     = "Tickets";
    public const string Contactos   = "Contactos";
    public const string Proyectos   = "Proyectos";
}
