namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>Un invitado de la cita, ya sin recursos ni el dueño del calendario, y recortado a los
/// largos de Asistentes (Nombre 150, Correo 200). Lo arma CitaOutlookTraductor.</summary>
public sealed record InvitadoImportado(string Nombre, string Correo);

/// <summary>
/// Lo que una cita de Outlook decide sobre su reunión del portal (spec 2026-10-29): Outlook manda
/// solo sobre estos campos. Fecha y Hora en hora de Honduras; Hora y Duracion nulas en una cita de
/// todo el día. <see cref="EsOrganizador"/> dice si el dueño del calendario la organizó: solo
/// entonces su conexión puede bajar la transcripción de Teams.
/// </summary>
public sealed record GeneralesOutlook(
    string Titulo, DateOnly Fecha, string? Hora, string? Duracion, string? Lugar,
    string? EnlaceTeams, bool EsOrganizador, string? ICalUId, DateTime? ModificadaElUtc);
