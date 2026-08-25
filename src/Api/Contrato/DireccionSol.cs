namespace Diger.TramitesEstado.Api.Contrato;

/// <summary>
/// Cómo se arma la dirección pública de un trámite en SOL.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué esto vive acá y no se pide a PortalDigital.</b> Lo que se guarda en la base es un
/// <i>tramo</i> —la parte final— y la ruta de la institución; la dirección completa no está
/// escrita en ninguna columna. Alguien tiene que juntarlas con el host, y el host es
/// configuración de cada ambiente, no un dato.
/// </para>
/// <para>
/// Lo que se junta no es una regla de negocio de PortalDigital: es <b>la forma que tienen las
/// direcciones de SOL</b>, un sistema de terceros. PortalDigital la conoce porque le enseña al
/// técnico una vista previa mientras escribe; esta API la conoce porque publica el enlace que el
/// ciudadano va a pulsar. Son dos consumidores de la misma convención externa, no una regla
/// compartida: si SOL cambiara la forma de sus direcciones, los dos tendrían que cambiar, y eso
/// es correcto.
/// </para>
/// <para>
/// De la versión de PortalDigital solo se trae lo que esta API usa: componer. Validar tramos y
/// pintar prefijos son cosas del editor, y acá no hay editor.
/// </para>
/// </remarks>
public static class DireccionSol
{
    /// <summary>La dirección absoluta del trámite en SOL, o null si no tiene ninguna.</summary>
    /// <remarks>
    /// Absoluta siempre: es lo que HondurasÁgil consume desde la v1. Devolver una relativa
    /// rompería los enlaces del portal ciudadano sin que nadie se entere hasta que alguien pulse.
    /// </remarks>
    /// <param name="urlBase">El host de SOL, de configuración (<c>Sol:UrlBase</c>).</param>
    /// <param name="rutaInstitucion">La ruta de la institución — su sigla, salvo que alguien la
    /// haya corregido en <c>Instituciones.RutaSol</c>.</param>
    /// <param name="tramo">El tramo final que guarda la ficha.</param>
    /// <param name="urlHeredada">La dirección completa que traían las fichas anteriores.</param>
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

    /// <summary>Deja un tramo o una ruta sin espacios ni barras sobrantes. Null si no queda nada.</summary>
    private static string? Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;

        var partes = valor.Trim().Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return partes.Length == 0 ? null : string.Join('/', partes);
    }
}
