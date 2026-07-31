using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.Calendario;

[Authorize]
public sealed class IndexModel(ISender sender) : PageModel
{
    public CalendarioDto Data { get; private set; } = default!;

    public VistaCalendario Vista { get; private set; }

    /// <summary>Inicio del rango consultado. En vista de mes es el día 1.</summary>
    public DateOnly Desde { get; private set; }

    /// <summary>Fin del rango consultado, exclusivo.</summary>
    public DateOnly Hasta { get; private set; }

    public DateOnly Hoy { get; private set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Paso de las flechas: ±1 día, ±7 días o ±1 mes según la vista.</summary>
    public DateOnly Anterior { get; private set; }
    public DateOnly Siguiente { get; private set; }

    public ILookup<DateOnly, ReunionCalendarioDto> ReunionesPorDia { get; private set; } = default!;
    public ILookup<DateOnly, EventoCalendarioDto>  ActividadPorDia { get; private set; } = default!;

    public bool PuedeGestionar => User.CanMutate();

    /// <summary>Domingo de la semana que contiene <paramref name="d"/> (la grilla arranca en domingo).</summary>
    public static DateOnly InicioSemana(DateOnly d) => d.AddDays(-(int)d.DayOfWeek);

    public async Task OnGetAsync(string? vista, DateOnly? fecha, CancellationToken ct)
    {
        Vista = Enum.TryParse<VistaCalendario>(vista, ignoreCase: true, out var v) ? v : VistaCalendario.Mes;

        // La fecha ancla puede venir de cualquier vista; cada una la recorta a su rango.
        var referencia = fecha ?? Hoy;

        (Desde, Hasta) = Vista switch
        {
            VistaCalendario.Dia    => (referencia, referencia.AddDays(1)),
            VistaCalendario.Semana => (InicioSemana(referencia), InicioSemana(referencia).AddDays(7)),
            _                      => (new DateOnly(referencia.Year, referencia.Month, 1),
                                       new DateOnly(referencia.Year, referencia.Month, 1).AddMonths(1)),
        };

        // Se navega desde el inicio del rango, no desde la fecha ancla: así pasar
        // de mes no arrastra el día (31 de enero → 28 de febrero → 28 de marzo).
        (Anterior, Siguiente) = Vista switch
        {
            VistaCalendario.Dia    => (Desde.AddDays(-1),   Desde.AddDays(1)),
            VistaCalendario.Semana => (Desde.AddDays(-7),   Desde.AddDays(7)),
            _                      => (Desde.AddMonths(-1), Desde.AddMonths(1)),
        };

        Data = await sender.Send(new GetCalendarioQuery(Desde, Hasta), ct);
        ReunionesPorDia = Data.Reuniones.ToLookup(r => r.Fecha);
        ActividadPorDia = Data.Actividad.ToLookup(e => e.Fecha);
    }
}
