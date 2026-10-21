namespace Diger.TramitesEstado.Application.Calendario.Ics;

/// <summary>
/// Un evento listo para escribirse como <c>VEVENT</c>, ya resuelto: instantes absolutos, sin texto
/// que interpretar y sin dependencias del modelo.
///
/// <para>Es un paso intermedio a propósito. El escritor de iCalendar no debería saber qué es una
/// reunión, y quien conoce las reuniones no debería saber de plegado de líneas ni de escapes. En el
/// medio queda esto, que además es lo que hace que el formato se pueda probar solo.</para>
/// </summary>
public sealed record EventoIcs
{
    /// <summary>Identificador estable y único del evento. Es lo que hace que volver a descargar el
    /// archivo <b>actualice</b> la cita en vez de duplicarla, así que no puede cambiar entre
    /// descargas de la misma reunión.</summary>
    public required string Uid { get; init; }

    public required string Titulo { get; init; }

    /// <summary>Inicio y fin de un evento con hora. Nulos en uno de día completo.</summary>
    public DateTimeOffset? Inicio { get; init; }
    public DateTimeOffset? Fin    { get; init; }

    /// <summary>Día de un evento sin hora. <see cref="DiaFin"/> es <b>exclusivo</b>, como manda
    /// iCalendar: un evento de un solo día termina al día siguiente.</summary>
    public DateOnly? Dia    { get; init; }
    public DateOnly? DiaFin { get; init; }

    public string? Descripcion { get; init; }
    public string? Lugar       { get; init; }
    public string? Url         { get; init; }

    public string? OrganizadorNombre { get; init; }
    public string? OrganizadorCorreo { get; init; }

    /// <summary>Solo se listan como <c>ATTENDEE</c> informativos: el archivo se publica
    /// (<c>METHOD:PUBLISH</c>), no invita, así que nadie recibe una convocatoria por aparecer acá.</summary>
    public IReadOnlyList<AsistenteIcs> Asistentes { get; init; } = [];

    /// <summary>Cuándo cambió la reunión por última vez. Es lo que usa el cliente de calendario
    /// para decidir que la cita que ya tenía quedó vieja.</summary>
    public DateTime? UltimaModificacionUtc { get; init; }

    public bool EsDiaCompleto => Dia is not null;
}

public sealed record AsistenteIcs(string? Nombre, string Correo);
