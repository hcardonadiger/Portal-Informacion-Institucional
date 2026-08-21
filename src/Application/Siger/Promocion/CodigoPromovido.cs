using System.Globalization;

namespace Diger.TramitesEstado.Application.Siger.Promocion;

/// <summary>
/// Genera el código de una ficha que nació en el portal y no en SIGER.
/// </summary>
/// <remarks>
/// El correlativo es por institución, no global: así el código sigue leyéndose como los de
/// SIGER (prefijo de institución + número) y no delata cuántas fichas ha promovido DIGER en
/// total. La «P» es la marca visible de que la ficha no viene del inventario.
/// </remarks>
public static class CodigoPromovido
{
    public const string PrefijoPorDefecto = "DGR";
    private const string Marca = "-P";

    public static string PrefijoDe(string codigoSiger)
    {
        if (string.IsNullOrWhiteSpace(codigoSiger)) return PrefijoPorDefecto;
        var guion = codigoSiger.IndexOf('-');
        return guion < 0 ? codigoSiger.Trim() : codigoSiger[..guion].Trim();
    }

    public static string Siguiente(string? prefijo, IEnumerable<string> codigosExistentes)
    {
        var p = string.IsNullOrWhiteSpace(prefijo) ? PrefijoPorDefecto : prefijo.Trim();
        var inicio = p + Marca;

        var mayor = codigosExistentes
            .Where(c => c is not null && c.StartsWith(inicio, StringComparison.OrdinalIgnoreCase))
            .Select(c => int.TryParse(c[inicio.Length..], NumberStyles.None,
                                      CultureInfo.InvariantCulture, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{p}{Marca}{mayor + 1:00}";
    }
}
