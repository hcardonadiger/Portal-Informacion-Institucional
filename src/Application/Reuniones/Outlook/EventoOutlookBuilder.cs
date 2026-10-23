using System.Net;
using System.Net.Mail;

namespace Diger.TramitesEstado.Application.Reuniones.Outlook;

/// <summary>
/// Reunion → EventoOutlook. Puro: no lee la base ni llama a Graph, así que se prueba entero.
/// Invitados = pre-registrados con correo + enlace institucional + facilitador. El auto-registro
/// por QR NO invita: es asistencia ya ocurrida.
/// </summary>
public static class EventoOutlookBuilder
{
    public static EventoOutlook Construir(Reunion r, string? correoOrganizador, string urlFicha)
    {
        var horario = HorarioReunion.Resolver(r.Fecha, r.Hora, r.Duracion);

        var organizador = Normalizar(correoOrganizador);
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var invitados = new List<InvitadoOutlook>();

        void Agregar(string? correo, string? nombre)
        {
            var c = Normalizar(correo);
            if (c is null || c == organizador || !vistos.Add(c)) return;
            invitados.Add(new InvitadoOutlook(c, string.IsNullOrWhiteSpace(nombre) ? null : nombre.Trim()));
        }

        foreach (var a in r.Asistentes.Where(a => a.EsPreregistro))
            Agregar(a.Correo, a.Nombre);
        Agregar(r.EpCorreo, r.EpNombre);
        Agregar(r.FacCorreo, r.FacNombre);

        var cuerpo = string.IsNullOrWhiteSpace(r.ObjetivoAgenda)
            ? string.Empty
            : $"<p>{WebUtility.HtmlEncode(r.ObjetivoAgenda.Trim())}</p>";
        cuerpo += $"<p>Ficha en {WebUtility.HtmlEncode(Marca.Nombre)}: <a href=\"{WebUtility.HtmlEncode(urlFicha)}\">{WebUtility.HtmlEncode(urlFicha)}</a></p>";

        return new EventoOutlook(
            Asunto:     r.Titulo,
            Inicio:     horario.Inicio,
            Fin:        horario.Fin,
            Ubicacion:  string.IsNullOrWhiteSpace(r.Lugar) ? null : r.Lugar.Trim(),
            CuerpoHtml: cuerpo,
            Invitados:  invitados,
            EsTeams:    r.OutlookConTeams);
    }

    /// <summary>Correo en minúsculas, o null si no es una dirección.</summary>
    private static string? Normalizar(string? correo)
    {
        if (string.IsNullOrWhiteSpace(correo)) return null;
        var c = correo.Trim().ToLowerInvariant();
        return MailAddress.TryCreate(c, out var m) && m.Address == c && c.Contains('.') ? c : null;
    }
}
