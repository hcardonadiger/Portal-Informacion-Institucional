namespace Diger.TramitesEstado.Application.Common.Extensions;

public static class FechaExtensions
{
    public static string ToFechaCorta(this DateOnly f) => f.ToString("dd-MM-yyyy");
    public static string? ToFechaCorta(this DateOnly? f) => f?.ToString("dd-MM-yyyy");
    public static string ToFechaCorta(this DateTime f) => f.ToString("dd-MM-yyyy");
    public static string? ToFechaCorta(this DateTime? f) => f?.ToString("dd-MM-yyyy");

    public static string ToFechaHoraCorta(this DateTime f) => f.ToString("dd-MM-yyyy HH:mm");
    public static string? ToFechaHoraCorta(this DateTime? f) => f?.ToString("dd-MM-yyyy HH:mm");
}
