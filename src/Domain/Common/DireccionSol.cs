using System.Text.RegularExpressions;

namespace Diger.TramitesEstado.Domain.Common;

/// <summary>
/// El único lugar donde una dirección de SOL se arma. Si esta clase no existiera, la
/// concatenación viviría repetida en la API pública, en el editor, en la ficha de detalle y en
/// la captura en lote, y bastaría con que una de las cuatro olvidara una barra para que unos
/// enlaces salieran con doble barra y otros sin ninguna, en el portal que ve el ciudadano.
///
/// La regla completa, de <c>D-13</c>, <c>D-14</c> y <c>D-20</c> del plan:
///
/// <list type="bullet">
/// <item>Si el trámite tiene <b>tramo</b>, la dirección se compone:
///   <c>{base}/{ruta de la institución}/{tramo}</c>. La ruta de la institución sale de su llave
///   primaria salvo que alguien la haya corregido.</item>
/// <item>Si no tiene tramo pero arrastra una <b>URL completa heredada</b>, se devuelve tal cual,
///   sin tocarla (D-14).</item>
/// <item>Si no tiene ninguna de las dos, no hay dirección.</item>
/// </list>
///
/// La salida es <b>siempre absoluta</b>. Es lo que la API pública viene emitiendo desde la Fase 4
/// y lo que HondurasÁgil ya consume: cambiar la forma de ese campo rompería los enlaces del
/// portal ciudadano sin que nadie se entere hasta que alguien haga clic.
/// </summary>
public static class DireccionSol
{
    /// <summary>
    /// Qué acepta una ruta o un tramo: letras, números, guiones y guiones bajos, en uno o varios
    /// segmentos separados por barra.
    ///
    /// Se rechaza todo lo demás en vez de escaparlo, y la razón es que un espacio o un acento
    /// acá casi nunca es una dirección exótica: es un descuido de captura. Escaparlo produciría
    /// un enlace que existe, se ve bien y lleva a un 404 —el peor de los resultados, porque nadie
    /// lo nota—. Rechazarlo hace que la persona lo corrija en el momento.
    /// </summary>
    private static readonly Regex SegmentosValidos =
        new(@"^[A-Za-z0-9\-_]+(/[A-Za-z0-9\-_]+)*$", RegexOptions.Compiled);

    /// <summary>Arma la dirección pública del trámite en SOL, o null si no tiene ninguna.</summary>
    /// <param name="urlBase">El host de SOL, de configuración (<c>Sol:UrlBase</c>). No se pone en
    /// código: el día que SOL cambie de dirección, mil enlaces cambian con una línea de
    /// configuración y no con un despliegue.</param>
    /// <param name="rutaInstitucion">La ruta de la institución — normalmente su llave primaria.
    /// Use <c>Institucion.RutaSolEfectiva</c>, que ya resuelve el «salvo que la hayan
    /// corregido».</param>
    /// <param name="tramo">El tramo final que captura el trámite.</param>
    /// <param name="urlHeredada">La URL completa que la ficha traía de antes (D-14).</param>
    public static string? Componer(string? urlBase, string? rutaInstitucion, string? tramo, string? urlHeredada)
    {
        var tramoLimpio = Normalizar(tramo);

        if (tramoLimpio is null)
            return string.IsNullOrWhiteSpace(urlHeredada) ? null : urlHeredada.Trim();

        var rutaLimpia = Normalizar(rutaInstitucion);
        var baseLimpia = urlBase?.Trim().TrimEnd('/');

        // Sin base o sin ruta no se puede componer nada honesto. Antes que devolver una dirección
        // a medias —«/CONSUCOOP/licencia», que el navegador resolvería contra el portal
        // equivocado— se cae a la heredada, y si tampoco hay, a nada.
        if (string.IsNullOrWhiteSpace(baseLimpia) || rutaLimpia is null)
            return string.IsNullOrWhiteSpace(urlHeredada) ? null : urlHeredada.Trim();

        return $"{baseLimpia}/{rutaLimpia}/{tramoLimpio}";
    }

    /// <summary>
    /// Deja un tramo o una ruta en su forma canónica: sin espacios, sin barras sobrantes al
    /// principio ni al final, y sin barras dobles en medio. Devuelve null si no queda nada.
    ///
    /// Se normaliza acá y no en cada pantalla a propósito: quien escribe «/licencia/» y quien
    /// escribe «licencia» están diciendo lo mismo, y obligar a cada formulario a saberlo es
    /// obligar a que los cuatro acierten.
    /// </summary>
    public static string? Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;

        var partes = valor.Trim().Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (partes.Length == 0) return null;

        return string.Join('/', partes);
    }

    /// <summary>Si un tramo o una ruta tiene forma de poder ir en una dirección. Un valor vacío
    /// se considera válido: significa «no hay», no «hay algo mal escrito».</summary>
    public static bool EsSegmentoValido(string? valor)
    {
        var limpio = Normalizar(valor);
        return limpio is null || SegmentosValidos.IsMatch(limpio);
    }

    /// <summary>Cómo se le enseña a la persona el prefijo que no puede editar, para que vea la
    /// dirección completa que va a producir lo que escriba (D-13).</summary>
    public static string Prefijo(string? urlBase, string? rutaInstitucion)
    {
        var baseLimpia = urlBase?.Trim().TrimEnd('/');
        var rutaLimpia = Normalizar(rutaInstitucion);

        if (string.IsNullOrWhiteSpace(baseLimpia)) return string.Empty;

        return rutaLimpia is null ? $"{baseLimpia}/" : $"{baseLimpia}/{rutaLimpia}/";
    }
}
