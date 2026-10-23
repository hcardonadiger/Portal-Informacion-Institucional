namespace Diger.TramitesEstado.Application.Reuniones.Outlook;

/// <summary>
/// «Ahora» en hora local de Honduras (Kind = Unspecified), para compararlo con el Inicio/Fin de la
/// cita, que <see cref="HorarioReunion"/> arma en esa misma hora y sin zona. El servidor puede estar
/// en cualquier zona: se convierte desde el reloj en UTC, nunca se usa DateTime.Now.
/// </summary>
public static class HoraHonduras
{
    // Windows la llama «Central America Standard Time»; Linux/ICU, «America/Tegucigalpa». Si
    // ninguna existe, desfase fijo: Honduras no tiene horario de verano.
    private static readonly TimeZoneInfo Zona = ResolverZona();

    public static DateTime Ahora(TimeProvider reloj) =>
        DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), Zona).DateTime, DateTimeKind.Unspecified);

    private static TimeZoneInfo ResolverZona()
    {
        foreach (var id in new[] { "Central America Standard Time", "America/Tegucigalpa" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("Honduras", TimeSpan.FromHours(-6), "Honduras (UTC−6)", "Honduras (UTC−6)");
    }
}
