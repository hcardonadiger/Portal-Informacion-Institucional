namespace Diger.TramitesEstado.Application.Common.Models;

/// <summary>
/// Dónde vive SOL, configurable vía appsettings (sección «Sol»).
///
/// **No está en código a propósito, y no es una preferencia de estilo.** Desde la Fase 7 la
/// dirección de cada trámite en SOL se compone en vez de escribirse: un host equivocado no
/// produce un enlace roto, produce <b>mil</b>, todos con la misma apariencia correcta y todos
/// llevando a ninguna parte. En configuración se arregla con una línea; en código, con un
/// despliegue.
///
/// El valor de abajo lo confirmó DIGER el 25 de agosto de 2026. Antes convivían dos: el plan
/// escribía <c>sol.gob.hn</c> y el editor de fichas llevaba desde el 14 de agosto un marcador de
/// posición con <c>sol.pdihonduras.gob.hn</c>. Ganó el segundo.
/// </summary>
public sealed class SolOptions
{
    /// <summary>Host de SOL, sin barra final. Se le antepone la ruta de la institución y el
    /// tramo del trámite.</summary>
    public string UrlBase { get; init; } = "https://sol.pdihonduras.gob.hn";
}
