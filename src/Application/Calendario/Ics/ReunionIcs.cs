using System.Text;
using Diger.TramitesEstado.Application.Common.Tiempo;

namespace Diger.TramitesEstado.Application.Calendario.Ics;

/// <summary>
/// Traduce una reunión del portal al evento que se publica en un calendario. Es el punto donde se
/// paga la Fase 0: la ventana <c>InicioLocal</c>/<c>FinLocal</c> ya viene resuelta por la entidad y
/// acá solo se le aplica la zona institucional.
/// </summary>
public static class ReunionIcs
{
    /// <summary>
    /// El UID que identifica la reunión ante cualquier calendario. Se arma con el Id y el dominio
    /// institucional, y <b>no</b> con el token de registro: ese se puede regenerar desde la pantalla
    /// de asistencia, y un UID que cambia hace que la cita se duplique en vez de actualizarse.
    /// </summary>
    public static string Uid(int reunionId, string dominio) =>
        $"reunion-{reunionId}@{dominio}";

    public static EventoIcs Mapear(
        Reunion r,
        RelojInstitucional reloj,
        string dominio,
        string? urlPortal = null,
        bool incluirAsistentes = true)
    {
        var evento = new EventoIcs
        {
            Uid         = Uid(r.Id, dominio),
            Titulo      = r.Titulo,
            Descripcion = Descripcion(r),
            Lugar       = Lugar(r),
            Url         = urlPortal,
            OrganizadorNombre = string.IsNullOrWhiteSpace(r.FacNombre) ? null : r.FacNombre,
            OrganizadorCorreo = string.IsNullOrWhiteSpace(r.FacCorreo) ? null : r.FacCorreo,
            Asistentes  = incluirAsistentes
                ? r.Asistentes
                    .Where(a => !string.IsNullOrWhiteSpace(a.Correo))
                    .Select(a => new AsistenteIcs(a.Nombre, a.Correo!))
                    .ToList()
                : [],
            UltimaModificacionUtc = r.UpdatedAt ?? (r.CreatedAt == default ? null : r.CreatedAt)
        };

        // Sin hora se publica como evento de día completo; con hora, como instantes absolutos.
        if (r.EsTodoElDia)
            return evento with { Dia = r.Fecha, DiaFin = r.Fecha!.Value.AddDays(1) };

        if (r.InicioLocal is { } ini && r.FinLocal is { } fin)
            return evento with { Inicio = reloj.AInstante(ini), Fin = reloj.AInstante(fin) };

        // Sin fecha no hay nada que publicar; el escritor lo descarta.
        return evento;
    }

    /// <summary>
    /// El cuerpo de la cita. Se arma con lo que sirve estando en la reunión —objetivo, tipo,
    /// institución— y no con todo lo que la ficha guarda: el desarrollo y los acuerdos se escriben
    /// <i>después</i>, así que en la invitación estarían vacíos.
    /// </summary>
    private static string? Descripcion(Reunion r)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(r.ObjetivoAgenda)) sb.AppendLine(r.ObjetivoAgenda.Trim()).AppendLine();
        if (!string.IsNullOrWhiteSpace(r.Tipo))          sb.AppendLine($"Tipo: {r.Tipo.Trim()}");
        if (!string.IsNullOrWhiteSpace(r.Institucion))   sb.AppendLine($"Institución: {r.Institucion.Trim()}");
        if (!string.IsNullOrWhiteSpace(r.Modalidad))     sb.AppendLine($"Modalidad: {r.Modalidad.Trim()}");

        var texto = sb.ToString().TrimEnd();
        return texto.Length == 0 ? null : texto;
    }

    /// <summary>En una reunión virtual el «lugar» suele ser el enlace de la videollamada, que es
    /// justo lo que se quiere poder tocar desde la cita del celular.</summary>
    private static string? Lugar(Reunion r)
    {
        if (!string.IsNullOrWhiteSpace(r.Lugar)) return r.Lugar.Trim();
        return string.IsNullOrWhiteSpace(r.Modalidad) ? null : r.Modalidad.Trim();
    }
}
