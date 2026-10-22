namespace Diger.TramitesEstado.Application.Notificaciones;

/// <summary>
/// El día de la institución, no el de UTC. Plazos, fechas de reunión y fechas de cronograma se
/// escriben en hora de Honduras; compararlos contra la fecha UTC adelantaba el día seis horas, desde
/// las 6:00 p. m. (corregido el 2026-10-22).
/// </summary>
public static class CalendarioLocal
{
    public const string ZonaPorDefecto = "America/Tegucigalpa";

    /// <summary>Respaldo si el nombre configurado no existe en el servidor: Honduras no tiene
    /// horario de verano, así que UTC-6 fijo da el mismo resultado todo el año.</summary>
    public static readonly TimeZoneInfo Respaldo =
        TimeZoneInfo.CreateCustomTimeZone("UTC-06 (Honduras)", TimeSpan.FromHours(-6), "UTC-06 (Honduras)", "UTC-06 (Honduras)");

    /// <summary>Busca la zona por su nombre, IANA («America/Tegucigalpa») o de Windows («Central
    /// America Standard Time»). Devuelve <c>Encontrada = false</c> —y el respaldo— cuando el nombre
    /// no existe en el servidor, para que quien llama lo deje escrito en el registro.</summary>
    public static (TimeZoneInfo Zona, bool Encontrada) Resolver(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return (Respaldo, false);
        var limpio = nombre.Trim();

        if (Buscar(limpio) is { } zona) return (zona, true);

        // Un Windows sin ICU no reconoce los nombres IANA: se prueba con su equivalente de Windows.
        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(limpio, out var deWindows) && Buscar(deWindows) is { } equivalente)
            return (equivalente, true);

        return (Respaldo, false);
    }

    private static TimeZoneInfo? Buscar(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return null; }
    }

    /// <summary>Hoy y mañana en la zona, y el instante UTC en que empezó hoy (para comparar con
    /// fechas guardadas en UTC, como la de creación de una notificación).</summary>
    public static (DateOnly Hoy, DateOnly Manana, DateTime InicioDeHoyUtc) Dia(DateTimeOffset ahora, TimeZoneInfo zona)
    {
        var local = TimeZoneInfo.ConvertTime(ahora, zona);
        var hoy   = DateOnly.FromDateTime(local.DateTime);
        var inicioLocal = new DateTimeOffset(local.Date, zona.GetUtcOffset(local.Date));
        return (hoy, hoy.AddDays(1), inicioLocal.UtcDateTime);
    }
}
