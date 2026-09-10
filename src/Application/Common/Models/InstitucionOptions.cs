namespace Diger.TramitesEstado.Application.Common.Models;

/// <summary>Identidad institucional del sistema, configurable vía appsettings (sección "Institucion").</summary>
public sealed class InstitucionOptions
{
    public string Nombre { get; init; } = "Dirección General de Gobierno Digital";
    public string NombreCorto { get; init; } = "DIGER";
    public string Logo { get; init; } = "/img/logo_diger.png";
    public string Direccion { get; init; } = "";
    public string Telefono { get; init; } = "";
    public string Email { get; init; } = "";
    public string SitioWeb { get; init; } = "https://www.diger.gob.hn";
    public string Eslogan { get; init; } = "Gobierno de la República de Honduras";

    /// <summary>
    /// Zona horaria en la que se leen las fechas y horas del portal.
    ///
    /// <para>Se declara explícitamente en vez de confiar en la del servidor: la hora de una reunión
    /// es un dato institucional, y si el portal se mueve a un servidor o a un contenedor con otra
    /// zona, las horas no deben correrse. Honduras es UTC−6 todo el año, sin horario de verano.</para>
    ///
    /// <para>Se admite tanto el identificador IANA (<c>America/Tegucigalpa</c>) como el de Windows
    /// (<c>Central America Standard Time</c>); ver <c>RelojInstitucional</c>.</para>
    /// </summary>
    public string ZonaHoraria { get; init; } = "America/Tegucigalpa";
}
