namespace Diger.TramitesEstado.Application.Notificaciones;

/// <summary>Cadencia del job de recordatorios/notificaciones, configurable vía
/// appsettings (sección "Notificaciones").</summary>
public sealed class NotificacionesOptions
{
    public int DelayInicialMinutos { get; init; } = 2;
    public int IntervaloHoras { get; init; } = 4;
}
