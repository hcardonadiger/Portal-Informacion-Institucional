using System.Globalization;
using System.Text;

namespace Diger.TramitesEstado.Application.Siger.Llenado;

/// <summary>
/// Comparación de texto sin tildes y sin mayúsculas, para buscar palabras dentro de fichas
/// escritas por sesenta y siete instituciones distintas.
/// </summary>
/// <remarks>
/// No es un lujo: en el inventario conviven «Migración» y «migracion», «Licencia Ambiental» y
/// «licencia ambiental». Una regla que busque la palabra tal cual acierta en unas fichas y falla
/// en otras <i>por cómo alguien escribió el título</i>, y ese fallo es invisible —simplemente no
/// se propone nada— así que nadie lo notaría.
/// </remarks>
public static class TextoNormalizado
{
    /// <summary>Minúsculas, sin tildes y con los espacios colapsados.</summary>
    public static string De(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var descompuesto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        var espacioPendiente = false;

        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsWhiteSpace(c))
            {
                espacioPendiente = sb.Length > 0;
                continue;
            }

            if (espacioPendiente) { sb.Append(' '); espacioPendiente = false; }
            sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>La primera frase de <paramref name="frases"/> que aparezca en el texto ya
    /// normalizado, o null. Devuelve cuál acertó y no solo que acertó: la justificación tiene que
    /// poder decir <i>qué</i> se encontró, porque de eso depende que alguien pueda aprobar por
    /// tandas sin firmar a ciegas.</summary>
    public static string? PrimeraQueAparece(string textoNormalizado, IReadOnlyList<string> frases)
    {
        foreach (var frase in frases)
            if (textoNormalizado.Contains(frase, StringComparison.Ordinal))
                return frase;

        return null;
    }
}
