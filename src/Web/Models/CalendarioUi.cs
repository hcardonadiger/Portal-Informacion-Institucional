namespace Diger.TramitesEstado.Web.Models;

/// <summary>Icono, color y etiqueta para los tipos de evento del calendario.</summary>
public static class CalendarioUi
{
    public static (string Icono, string Color, string Etiqueta) Evento(TipoEventoCalendario t) => t switch
    {
        TipoEventoCalendario.Reunion            => ("•", "#1455a4", "Reunión"),
        TipoEventoCalendario.TicketCreado       => ("•", "#d85a30", "Ticket creado"),
        TipoEventoCalendario.TicketRespuesta    => ("•", "#185fa5", "Respuesta a ticket"),
        TipoEventoCalendario.TicketCambioEstado => ("•", "#7a3ca8", "Cambio de estado"),
        TipoEventoCalendario.TicketAsignacion   => ("•", "#5f5e5a", "Asignación"),
        TipoEventoCalendario.ExpedienteCreado   => ("•", "#0f8a6a", "Expediente"),
        _                                       => ("•",  "#888780", "Evento"),
    };
}
