namespace Diger.TramitesEstado.Application.Common.Models;

/// <summary>
/// Nombre con el que el sistema se presenta al usuario: pestaña del navegador,
/// login, cabecera, correos y encabezados de los informes.
///
/// Es una constante y no una opción de configuración a propósito. El nombre del
/// producto no cambia por ambiente —cambiarlo en pruebas y no en producción daría
/// dos sistemas distintos a los ojos del usuario— y como constante hay un solo
/// lugar que tocar el día que vuelva a cambiar.
///
/// No sustituye a <see cref="InstitucionOptions"/>: la institución (DIGER, su
/// logo, su sitio) sigue siendo configurable y se muestra al lado de este nombre.
/// </summary>
public static class Marca
{
    public const string Nombre = "GestionGD";
}
