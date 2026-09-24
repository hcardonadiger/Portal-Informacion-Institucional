using System.Globalization;

namespace Diger.TramitesEstado.Application.Common.Extensions;

public static class FechaExtensions
{
    /// <summary>Cultura fija para las fechas en palabras. El host no configura localización, así que
    /// sin esto el mismo afiche saldría en inglés o en español según el idioma del servidor.</summary>
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-HN");

    public static string ToFechaCorta(this DateOnly f) => f.ToString("dd-MM-yyyy");
    public static string? ToFechaCorta(this DateOnly? f) => f?.ToString("dd-MM-yyyy");
    public static string ToFechaCorta(this DateTime f) => f.ToString("dd-MM-yyyy");
    public static string? ToFechaCorta(this DateTime? f) => f?.ToString("dd-MM-yyyy");

    public static string ToFechaHoraCorta(this DateTime f) => f.ToString("dd-MM-yyyy HH:mm");
    public static string? ToFechaHoraCorta(this DateTime? f) => f?.ToString("dd-MM-yyyy HH:mm");

    /// <summary>Fecha en palabras: "24 de septiembre de 2026". Para textos que se leen, no se escanean.</summary>
    public static string ToFechaLarga(this DateOnly f) => f.ToString("d 'de' MMMM 'de' yyyy", Es);
    public static string? ToFechaLarga(this DateOnly? f) => f?.ToFechaLarga();

    /// <summary>Fecha en palabras con el día de la semana: "Jueves 24 de septiembre de 2026".</summary>
    public static string ToFechaLargaConDia(this DateOnly f)
    {
        var dia = f.ToString("dddd", Es);
        return string.Concat(Es.TextInfo.ToUpper(dia[..1]), dia[1..], " ", f.ToFechaLarga());
    }

    public static string? ToFechaLargaConDia(this DateOnly? f) => f?.ToFechaLargaConDia();
}
