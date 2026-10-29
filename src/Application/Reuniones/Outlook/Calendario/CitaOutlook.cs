namespace Diger.TramitesEstado.Application.Reuniones.Outlook.Calendario;

/// <summary>Una cita del calendario de Outlook tal como la entrega Graph, sin tipos de Graph.
/// Inicio y Fin en hora de Honduras (Kind = Unspecified): Graph las manda ya en esa zona.</summary>
public sealed record CitaOutlook(
    string Id, string? ICalUId, string? Asunto, DateTime Inicio, DateTime Fin, bool TodoElDia,
    string? Ubicacion, string? EnlaceTeams, bool EsOrganizador, bool Cancelada, DateTime? ModificadaElUtc,
    IReadOnlyList<InvitadoCita>? Invitados = null);

/// <summary>Un invitado tal como lo trae Graph. <see cref="EsRecurso"/> = sala o equipo
/// (attendee type «resource»): no es una persona y no se pre-registra.</summary>
public sealed record InvitadoCita(string? Nombre, string? Correo, bool EsRecurso);

/// <summary>
/// Lectura del calendario de un usuario con SU conexión. Los fallos llegan como
/// <see cref="OutlookConexionVencidaException"/> o <see cref="OutlookNoDisponibleException"/>.
/// </summary>
public interface ICalendarioOutlookLectura
{
    /// <summary>Citas entre las 00:00 de <paramref name="desde"/> y las 23:59 de <paramref name="hasta"/>,
    /// con las recurrencias ya expandidas.</summary>
    Task<IReadOnlyList<CitaOutlook>> ListarAsync(Guid usuarioId, DateOnly desde, DateOnly hasta, CancellationToken ct);

    /// <summary>Una cita por su id, o null si ya no existe (borrada en Outlook).</summary>
    Task<CitaOutlook?> ObtenerAsync(Guid usuarioId, string eventId, CancellationToken ct);
}
