using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Diger.TramitesEstado.Application.Siger;

/// <summary>Grado de certeza con que un trámite de expediente corresponde a una ficha SIGER.</summary>
public enum CubetaConciliacion
{
    /// <summary>Nombre idéntico, misma institución y un solo candidato. Se propone marcado.</summary>
    AltaConfianza  = 1,
    /// <summary>Nombre idéntico y un solo candidato, pero la sigla de institución no coincide.</summary>
    MediaConfianza = 2,
    /// <summary>Nombre idéntico contra varias fichas SIGER. Hay que elegir cuál.</summary>
    Ambiguo        = 3,
    /// <summary>Sin coincidencia exacta, pero un nombre contiene al otro.</summary>
    Parcial        = 4,
    /// <summary>Ninguna coincidencia. Candidato a ficha nueva en SIGER.</summary>
    SinCandidato   = 5
}

/// <summary>
/// Cruce por nombre entre los trámites de expedientes y el inventario SIGER.
/// </summary>
/// <remarks>
/// La comparación se hace en memoria y no en SQL a propósito. Las columnas de PortalDigital
/// usan una collation sensible a tildes, así que el cruce en base de datos obliga a forzar
/// <c>COLLATE Latin1_General_CI_AI</c> en cada consulta — fácil de olvidar y difícil de probar.
/// Con 1.057 fichas y unos cientos de trámites el costo en memoria es despreciable, y así la
/// regla queda en un solo lugar, cubierta por pruebas.
/// </remarks>
public static partial class ConciliacionMatcher
{
    /// <summary>
    /// Deja el nombre comparable: colapsa espacios, quita tildes y pasa a mayúsculas.
    /// Equivale a lo que hace <c>COLLATE Latin1_General_CI_AI</c> sobre el texto ya compactado.
    /// </summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var compacto = EspaciosRepetidos().Replace(texto, " ").Trim();

        // FormD separa la letra de su tilde; descartando las marcas queda la letra base.
        var descompuesto = compacto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }

    /// <summary>Clasifica un trámite según cuántos candidatos SIGER le calzan y de qué forma.</summary>
    public static CubetaConciliacion Clasificar(int exactos, int exactosMismaSigla, int parciales)
        => (exactos, exactosMismaSigla, parciales) switch
        {
            (1, 1, _)             => CubetaConciliacion.AltaConfianza,
            (1, _, _)             => CubetaConciliacion.MediaConfianza,
            ( > 1, _, _)          => CubetaConciliacion.Ambiguo,
            (0, _, > 0)           => CubetaConciliacion.Parcial,
            _                     => CubetaConciliacion.SinCandidato
        };

    /// <summary>Etiqueta corta para mostrar en pantalla.</summary>
    public static string Etiqueta(this CubetaConciliacion c) => c switch
    {
        CubetaConciliacion.AltaConfianza  => "Alta confianza",
        CubetaConciliacion.MediaConfianza => "Otra institución",
        CubetaConciliacion.Ambiguo        => "Ambiguo",
        CubetaConciliacion.Parcial        => "Parcial",
        _                                 => "Sin candidato"
    };

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspaciosRepetidos();
}
