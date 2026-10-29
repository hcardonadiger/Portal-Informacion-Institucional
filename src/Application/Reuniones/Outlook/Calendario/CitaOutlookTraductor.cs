using System.Globalization;

namespace Diger.TramitesEstado.Application.Reuniones.Outlook.Calendario;

/// <summary>De cita de Outlook a lo que Outlook manda sobre la reunión: los Generales y los
/// invitados. Puro: sin base, sin reloj. Recorta a los largos de columna en vez de dejar que falle
/// el guardado.</summary>
public static class CitaOutlookTraductor
{
    private const int LargoTitulo = 250;   // Reuniones.Titulo
    private const int LargoLugar  = 250;   // Reuniones.Lugar
    private const int LargoNombre = 150;   // Asistentes.Nombre
    private const int LargoCorreo = 200;   // Asistentes.Correo

    public static GeneralesOutlook Traducir(CitaOutlook c)
    {
        var titulo = string.IsNullOrWhiteSpace(c.Asunto) ? "(Sin asunto)" : Recortar(c.Asunto.Trim(), LargoTitulo);
        var lugar  = string.IsNullOrWhiteSpace(c.Ubicacion) ? null : Recortar(c.Ubicacion.Trim(), LargoLugar);
        var teams  = string.IsNullOrWhiteSpace(c.EnlaceTeams) ? null : c.EnlaceTeams.Trim();

        return new GeneralesOutlook(
            Titulo:          titulo,
            Fecha:           DateOnly.FromDateTime(c.Inicio),
            Hora:            c.TodoElDia ? null : c.Inicio.ToString("HH:mm", CultureInfo.InvariantCulture),
            Duracion:        c.TodoElDia ? null : Duracion(c.Fin - c.Inicio),
            Lugar:           lugar,
            EnlaceTeams:     teams,
            EsOrganizador:   c.EsOrganizador,
            ICalUId:         c.ICalUId,
            ModificadaElUtc: c.ModificadaElUtc);
    }

    /// <summary>Los invitados que se pre-registran: sin recursos, sin el dueño del calendario
    /// (<paramref name="correoDueno"/>, su Upn) y sin los que no traen correo. El orden es el de Outlook.</summary>
    public static IReadOnlyList<InvitadoImportado> Invitados(CitaOutlook c, string? correoDueno)
    {
        var dueno = correoDueno?.Trim();
        return (c.Invitados ?? [])
            .Where(i => !i.EsRecurso && !string.IsNullOrWhiteSpace(i.Correo))
            .Select(i => (Nombre: i.Nombre?.Trim(), Correo: i.Correo!.Trim()))
            .Where(i => !string.Equals(i.Correo, dueno, StringComparison.OrdinalIgnoreCase))
            .Select(i => new InvitadoImportado(
                Recortar(string.IsNullOrWhiteSpace(i.Nombre) ? i.Correo : i.Nombre, LargoNombre),
                Recortar(i.Correo, LargoCorreo)))
            .ToList();
    }

    /// <summary>«H:mm», el formato que HorarioReunion lee sin ambigüedad.</summary>
    private static string? Duracion(TimeSpan d)
    {
        var minutos = (int)Math.Round(d.TotalMinutes);
        return minutos <= 0 ? null : $"{minutos / 60}:{minutos % 60:00}";
    }

    private static string Recortar(string s, int largo) => s.Length <= largo ? s : s[..largo];
}
