namespace Diger.TramitesEstado.Application.Notificaciones;

/// <summary>Cadencia del job de recordatorios/notificaciones, configurable vía
/// appsettings (sección "Notificaciones").</summary>
public sealed class NotificacionesOptions
{
    public int DelayInicialMinutos { get; init; } = 2;
    public int IntervaloHoras { get; init; } = 4;

    /// <summary>Zona horaria con la que se decide qué es «hoy» y «mañana» para los recordatorios
    /// automáticos y el cronograma. Acepta el nombre IANA («America/Tegucigalpa») o el de Windows
    /// («Central America Standard Time»). Si el nombre no existe en el servidor se usa UTC-6 fijo y
    /// se avisa en el registro (ver <see cref="CalendarioLocal.Resolver"/>).</summary>
    public string ZonaHoraria { get; init; } = CalendarioLocal.ZonaPorDefecto;
}
