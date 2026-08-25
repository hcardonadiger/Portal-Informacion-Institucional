namespace Diger.TramitesEstado.Application.Common.Models;

/// <summary>
/// Dónde vive SOL, configurable vía appsettings (sección «Sol»).
///
/// **No está en código a propósito, y no es una preferencia de estilo.** Con la Fase 7 la
/// dirección de cada trámite en SOL se compone en vez de escribirse: un host equivocado no
/// produce un enlace roto, produce <b>mil</b>, todos con la misma apariencia correcta y todos
/// llevando a ninguna parte. En configuración se arregla con una línea; en código, con un
/// despliegue.
///
/// **El valor por omisión está sin confirmar.** El plan acordó el ejemplo
/// <c>sol.gob.hn/CONSUCOOP/…</c>, pero el editor de fichas llevaba desde agosto un marcador de
/// posición que decía <c>sol.pdihonduras.gob.hn</c>. Son dos hosts distintos y ninguno de los dos
/// está verificado contra SOL. Se toma el del plan por ser la decisión acordada y más reciente;
/// confirmarlo con quien opere SOL es requisito antes de publicar el primer enlace compuesto.
/// </summary>
public sealed class SolOptions
{
    /// <summary>Host de SOL, sin barra final. Se le antepone la ruta de la institución y el
    /// tramo del trámite.</summary>
    public string UrlBase { get; init; } = "https://sol.gob.hn";
}
