namespace Diger.TramitesEstado.Application.Common.Models;

/// <summary>Identidad institucional del sistema, configurable vía appsettings (sección "Institucion").</summary>
public sealed class InstitucionOptions
{
    /// <summary>Id de la fila de <c>Instituciones</c> que representa a la institución que opera este
    /// despliegue: la casa. No es decorativo — el filtro de alcance de <c>Expediente</c> lo usa para
    /// distinguir a quien *hace* la racionalización de quien la *recibe*. Un expediente documenta los
    /// trámites de otra institución, así que anclarlo solo en <c>InstitucionId</c> dejaba al personal
    /// de la casa sin ver ninguno.</summary>
    public string Id { get; init; } = "DIGER";

    public string Nombre { get; init; } = "Dirección General de Gobierno Digital";
    public string NombreCorto { get; init; } = "DIGER";
    public string Logo { get; init; } = "/img/logo_diger.png";
    public string Direccion { get; init; } = "";
    public string Telefono { get; init; } = "";
    public string Email { get; init; } = "";
    public string SitioWeb { get; init; } = "https://www.diger.gob.hn";
    public string Eslogan { get; init; } = "Gobierno de la República de Honduras";
}
