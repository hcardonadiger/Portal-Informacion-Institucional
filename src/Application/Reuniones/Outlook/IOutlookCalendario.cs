namespace Diger.TramitesEstado.Application.Reuniones.Outlook;

public sealed record ResultadoOutlook(string EventId, string? EnlaceTeams, string? ICalUId = null);

/// <summary>
/// Lo único que el portal sabe de Microsoft Graph. Siempre actúa en el calendario de
/// <paramref name="organizadorId"/>: es su conexión la que se usa, no la de quien llama.
/// Los fallos llegan como <see cref="OutlookConexionVencidaException"/> o
/// <see cref="OutlookNoDisponibleException"/>; ningún tipo de Graph cruza esta interfaz.
/// </summary>
public interface IOutlookCalendario
{
    Task<ResultadoOutlook> CrearAsync(Guid organizadorId, EventoOutlook evento, Guid transaccionId, CancellationToken ct);
    Task<ResultadoOutlook> ActualizarAsync(Guid organizadorId, string eventId, EventoOutlook evento, CancellationToken ct);
    Task CancelarAsync(Guid organizadorId, string eventId, string comentario, CancellationToken ct);
}

/// <summary>Microsoft ya no acepta la conexión de ese usuario (token revocado, contraseña cambiada,
/// inactividad, llaves de cifrado perdidas): tiene que reconectarse desde su perfil.</summary>
public sealed class OutlookConexionVencidaException(string message) : Exception(message);

/// <summary>Graph no respondió o rechazó la operación por algo que no es la conexión.</summary>
public sealed class OutlookNoDisponibleException(string message) : Exception(message);
