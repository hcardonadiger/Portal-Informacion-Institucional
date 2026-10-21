using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Application.Common.Tiempo;

/// <summary>
/// Convierte las horas de pared del portal («la reunión es el 12/09 a las 9:00») en instantes
/// absolutos, usando la zona configurada en <see cref="InstitucionOptions.ZonaHoraria"/>.
///
/// <para><b>Por qué existe</b>: la entidad <c>Reunion</c> guarda fecha y hora locales sin zona, que
/// es como las piensa quien convoca. Un calendario —un archivo .ics, Microsoft Graph, Google—
/// necesita en cambio un instante con desfase. Ese paso es el que se hace acá, una sola vez, en vez
/// de repetirlo en cada exportador con un <c>AddHours(-6)</c> suelto.</para>
///
/// <para>Es singleton: resolver la zona toca el sistema operativo y el resultado no cambia durante
/// la vida del proceso.</para>
/// </summary>
public sealed class RelojInstitucional
{
    /// <summary>Identificador de Windows equivalente a <c>America/Tegucigalpa</c>. Se usa como
    /// respaldo: en un Windows sin ICU, los identificadores IANA no resuelven.</summary>
    private const string RespaldoWindows = "Central America Standard Time";

    public TimeZoneInfo Zona { get; }

    /// <summary>Qué identificador se logró resolver. Distinto del configurado significa que se cayó
    /// al respaldo; se expone para poder diagnosticarlo sin adivinar.</summary>
    public string ZonaResuelta { get; }

    public RelojInstitucional(IOptions<InstitucionOptions> opciones)
    {
        var configurada = opciones.Value.ZonaHoraria;

        // .NET 6+ acepta identificadores IANA en Windows y de Windows en Linux, pero solo con ICU
        // disponible. La cadena de respaldo evita que el portal muera al arrancar por eso: una hora
        // mal rotulada es un problema; no arrancar es uno peor.
        foreach (var id in new[] { configurada, RespaldoWindows })
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            try
            {
                Zona = TimeZoneInfo.FindSystemTimeZoneById(id);
                ZonaResuelta = id;
                return;
            }
            catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // Se intenta el siguiente.
            }
        }

        Zona = TimeZoneInfo.Utc;
        ZonaResuelta = TimeZoneInfo.Utc.Id;
    }

    /// <summary>
    /// Toma una hora de pared y devuelve el instante que le corresponde en la zona institucional.
    /// </summary>
    /// <remarks>
    /// Honduras no aplica horario de verano, así que hoy no hay horas inexistentes ni ambiguas. Aun
    /// así se resuelven las dos: el método debe seguir siendo correcto si algún día el portal se
    /// configura para otra zona que sí lo tenga.
    /// </remarks>
    public DateTimeOffset AInstante(DateTime horaDePared)
    {
        var local = DateTime.SpecifyKind(horaDePared, DateTimeKind.Unspecified);

        // Hora inexistente (el reloj saltó hacia adelante): se corre el salto, que es lo que hacen
        // los calendarios en vez de rechazar el evento.
        if (Zona.IsInvalidTime(local))
            return new DateTimeOffset(local.Add(Zona.GetAdjustmentRules()
                .FirstOrDefault()?.DaylightDelta ?? TimeSpan.Zero), Zona.BaseUtcOffset);

        // Hora ambigua (el reloj se atrasó y ocurre dos veces): se toma la primera, la del desfase
        // mayor, que es la interpretación habitual.
        var desfase = Zona.IsAmbiguousTime(local)
            ? Zona.GetAmbiguousTimeOffsets(local).Max()
            : Zona.GetUtcOffset(local);

        return new DateTimeOffset(local, desfase);
    }

    /// <summary>El día de hoy según la zona institucional, no la del servidor.</summary>
    public DateOnly Hoy() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zona).DateTime);
}
