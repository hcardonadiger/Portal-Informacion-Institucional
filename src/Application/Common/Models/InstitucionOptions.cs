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
}
