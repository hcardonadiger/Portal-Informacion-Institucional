using System.Globalization;
using System.Text;
using Diger.TramitesEstado.Application.Siger.Publico;

namespace Diger.TramitesEstado.Application.Siger.Promocion;

/// <summary>
/// Convierte la modalidad de texto libre del expediente al catálogo cerrado de SIGER.
/// </summary>
/// <remarks>
/// Compara sin tildes y en minúsculas a propósito: en la base conviven «En línea» y
/// «En linea», y tratarlas distinto dejaría una ficha sin modalidad por una tilde.
/// Cuando el texto no dice nada reconocible devuelve null en vez de adivinar — una ficha
/// sin modalidad se declara incompleta y alguien la revisa, que es mejor que publicar
/// una modalidad equivocada.
/// </remarks>
public static class ModalidadNormalizador
{
    public static string? Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;

        // Un valor que ya es del catálogo sale intacto. Sin esta salida temprana, «Hibrido»
        // caería a null —no contiene ni «linea» ni «presencial»— y como este método corre en
        // cada guardado, borraría la modalidad de los híbridos cada vez que alguien editara.
        var exacto = texto.Trim();
        if (exacto is ModalidadPublica.Virtual or ModalidadPublica.Presencial or ModalidadPublica.Hibrido)
            return exacto;

        var t = SinTildes(texto).ToLowerInvariant();
        var enLinea    = t.Contains("linea") || t.Contains("virtual") || t.Contains("online");
        var presencial = t.Contains("presencial");

        return (enLinea, presencial) switch
        {
            (true,  true)  => ModalidadPublica.Hibrido,
            (true,  false) => ModalidadPublica.Virtual,
            (false, true)  => ModalidadPublica.Presencial,
            _              => null
        };
    }

    private static string SinTildes(string s) =>
        new(s.Normalize(NormalizationForm.FormD)
             .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
             .ToArray());
}
