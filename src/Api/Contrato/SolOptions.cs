namespace Diger.TramitesEstado.Api.Contrato;

/// <summary>
/// Dónde vive SOL, configurable vía appsettings (sección «Sol»).
/// </summary>
/// <remarks>
/// <b>No está en código a propósito, y no es una preferencia de estilo.</b> La dirección de cada
/// trámite en SOL se compone en vez de escribirse: un host equivocado no produce un enlace roto,
/// produce <b>mil</b>, todos con la misma apariencia correcta y todos llevando a ninguna parte.
/// En configuración se arregla con una línea; en código, con un despliegue.
/// <para>
/// El valor de abajo lo confirmó DIGER el 25 de agosto de 2026. PortalDigital tiene el mismo
/// valor en su propia configuración: los dos publican enlaces a SOL y los dos tienen que
/// apuntar al mismo sitio. Es configuración de ambiente duplicada, no una regla compartida.
/// </para>
/// </remarks>
public sealed class SolOptions
{
    /// <summary>Host de SOL, sin barra final.</summary>
    public string UrlBase { get; init; } = "https://sol.pdihonduras.gob.hn";
}
