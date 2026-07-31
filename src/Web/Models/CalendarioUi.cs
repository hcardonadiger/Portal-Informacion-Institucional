namespace Diger.TramitesEstado.Web.Models;

/// <summary>Presentación de un tipo de evento del calendario.</summary>
/// <param name="IconoId">Id del símbolo en <c>_IconSprite.cshtml</c> (sin el <c>#</c>).</param>
/// <param name="Color">Color sólido — puntos, barra de actividad, muestra del filtro.</param>
/// <param name="Fondo">Fondo tenue del cuadro de ícono en el feed.</param>
/// <param name="Texto">Color del ícono sobre <paramref name="Fondo"/>.</param>
public sealed record EventoUi(string IconoId, string Color, string Fondo, string Texto, string Etiqueta);

/// <summary>Icono, color y etiqueta para los tipos de evento del calendario.</summary>
public static class CalendarioUi
{
    // Cada tipo sale de una rampa distinta y suma un ícono propio: antes Reunión
    // (#1455a4) y Respuesta a ticket (#185fa5) eran el mismo azul a simple vista, y
    // el color era la única codificación — a 8px los puntos no se distinguían.
    public static EventoUi Evento(TipoEventoCalendario t) => t switch
    {
        TipoEventoCalendario.Reunion            => new("i-reunion",     "#185fa5", "#e6f1fb", "#0c447c", "Reunión"),
        TipoEventoCalendario.TicketCreado       => new("i-ticket-mas",  "#993c1d", "#faece7", "#712b13", "Ticket creado"),
        TipoEventoCalendario.TicketRespuesta    => new("i-respuesta",   "#0f6e56", "#e1f5ee", "#085041", "Respuesta a ticket"),
        TipoEventoCalendario.TicketCambioEstado => new("i-estado",      "#534ab7", "#eeedfe", "#3c3489", "Cambio de estado"),
        TipoEventoCalendario.TicketAsignacion   => new("i-asignacion",  "#5f5e5a", "#f1efe8", "#444441", "Asignación"),
        TipoEventoCalendario.ExpedienteCreado   => new("i-expediente",  "#854f0b", "#faeeda", "#633806", "Expediente"),
        _                                       => new("i-calendario",  "#888780", "#f1efe8", "#444441", "Evento"),
    };
}
