using System.Globalization;
using System.Text.RegularExpressions;

namespace Diger.TramitesEstado.Application.Reuniones.Outlook;

/// <summary>Inicio y fin en hora local de Honduras (Kind = Unspecified): Graph recibe la zona
/// aparte, como nombre, y no hay que convertir nada.</summary>
public sealed record HorarioResuelto(DateTime Inicio, DateTime Fin);

/// <summary>
/// Convierte Fecha + Hora + Duracion —texto libre en la base— en un horario de cita, o rechaza
/// nombrando el valor que no pudo leer. No adivina: una cita a la hora equivocada llega igual a la
/// bandeja de todos los invitados.
/// </summary>
public static partial class HorarioReunion
{
    private const int DuracionPorDefectoMin = 60;
    private const int DuracionMaximaMin     = 24 * 60;

    public static HorarioResuelto Resolver(DateOnly? fecha, string? hora, string? duracion)
    {
        if (fecha is null || string.IsNullOrWhiteSpace(hora))
            throw new DomainException("La reunión necesita fecha y hora para ir a Outlook.");

        var h = hora.Trim();
        var mh = HoraRegex().Match(h);
        if (!mh.Success
            || !int.TryParse(mh.Groups[1].Value, out var horas)   || horas   > 23
            || !int.TryParse(mh.Groups[2].Value, out var minutos) || minutos > 59)
            throw new DomainException($"No se entiende la hora «{h}». Debe ir como 09:30.");

        var inicio = fecha.Value.ToDateTime(new TimeOnly(horas, minutos));
        var duracionMin = LeerDuracion(duracion);
        return new HorarioResuelto(inicio, inicio.AddMinutes(duracionMin));
    }

    private static int LeerDuracion(string? duracion)
    {
        if (string.IsNullOrWhiteSpace(duracion)) return DuracionPorDefectoMin;

        var d = duracion.Trim();
        var t = d.ToLowerInvariant();
        double minutos;

        if (HoraRegex().Match(t) is { Success: true } hm)   // «1:30» = una hora y media
            minutos = int.Parse(hm.Groups[1].Value) * 60 + int.Parse(hm.Groups[2].Value);
        else if (NumeroUnidadRegex().Match(t) is { Success: true } nu
                 && double.TryParse(nu.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
            minutos = nu.Groups[2].Value.StartsWith("m") ? n : n * 60;
        else
            throw new DomainException($"No se entiende la duración «{d}». Use, por ejemplo, «1 hora», «90 min» o «1:30».");

        if (minutos <= 0 || minutos > DuracionMaximaMin)
            throw new DomainException($"La duración «{d}» no es razonable para una reunión.");

        return (int)Math.Round(minutos);
    }

    [GeneratedRegex(@"^(\d{1,2}):(\d{2})$")]
    private static partial Regex HoraRegex();

    // «1», «2 horas», «1,5 h», «90 min», «45 minutos». Sin unidad = horas.
    [GeneratedRegex(@"^(\d+(?:[.,]\d+)?)\s*(h|hr|hrs|hora|horas|m|min|mins|minuto|minutos)?$")]
    private static partial Regex NumeroUnidadRegex();
}
