using System.Globalization;
using System.Text;

namespace Diger.TramitesEstado.Application.Common;

/// <summary>Comparación difusa de texto (nombres de expedientes/trámites) para detectar posibles duplicados.</summary>
public static class TextoSimilitud
{
    public static string Normalizar(string s)
    {
        var descompuesto = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }

    /// <summary>Similitud entre 0 (nada en común) y 1 (idénticos), basada en distancia de Levenshtein sobre texto normalizado.</summary>
    public static double Similitud(string a, string b)
    {
        var na = Normalizar(a);
        var nb = Normalizar(b);
        if (na.Length == 0 && nb.Length == 0) return 1;
        if (na.Length == 0 || nb.Length == 0) return 0;
        var distancia = Levenshtein(na, nb);
        return 1.0 - (double)distancia / Math.Max(na.Length, nb.Length);
    }

    private static int Levenshtein(string a, string b)
    {
        var dp = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) dp[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
        for (var j = 1; j <= b.Length; j++)
        {
            var costo = a[i - 1] == b[j - 1] ? 0 : 1;
            dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + costo);
        }
        return dp[a.Length, b.Length];
    }
}
