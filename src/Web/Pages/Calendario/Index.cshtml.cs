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

    /// <summary>Paso de las flechas. En vista de mes avanza de semana en semana.</summary>
    public DateOnly Anterior { get; private set; }
    public DateOnly Siguiente { get; private set; }

    /// <summary>Salto de mes completo. Solo se usa en vista de mes.</summary>
    public DateOnly MesAnterior { get; private set; }
    public DateOnly MesSiguiente { get; private set; }

    // ── Paginación por semana dentro del mes ────────────────────────────
    /// <summary>Domingo de la semana visible. Solo aplica en vista de mes.</summary>
    public DateOnly SemanaVisible { get; private set; }
    public int SemanaNumero { get; private set; }   // 1..TotalSemanas
    public int TotalSemanas { get; private set; }

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

        if (Vista == VistaCalendario.Mes)
        {
            // El rango consultado sigue siendo el mes completo: los contadores y el
            // feed hablan del mes aunque la grilla muestre una sola semana.
            Desde = new DateOnly(referencia.Year, referencia.Month, 1);
            Hasta = Desde.AddMonths(1);

            var inicioGrid = InicioSemana(Desde);
            TotalSemanas = (int)Math.Ceiling(((int)Desde.DayOfWeek + DateTime.DaysInMonth(Desde.Year, Desde.Month)) / 7.0);

            // Semana que contiene la fecha ancla, acotada al mes por si la referencia
            // vino de otra vista y cae fuera de la grilla.
            var indice = Math.Clamp((InicioSemana(referencia).DayNumber - inicioGrid.DayNumber) / 7, 0, TotalSemanas - 1);
            SemanaVisible = inicioGrid.AddDays(indice * 7);
            SemanaNumero = indice + 1;

            // Al pasar del borde, la semana siguiente ya cae en el mes contiguo y el
            // recálculo la ubica sola: no hace falta tratar el salto como caso aparte.
            Anterior = SemanaVisible.AddDays(-7);
            Siguiente = SemanaVisible.AddDays(7);
            MesAnterior = Desde.AddMonths(-1);
            MesSiguiente = Desde.AddMonths(1);
        }
        else
        {
            (Desde, Hasta) = Vista == VistaCalendario.Dia
                ? (referencia, referencia.AddDays(1))
                : (InicioSemana(referencia), InicioSemana(referencia).AddDays(7));

            // Se navega desde el inicio del rango, no desde la fecha ancla.
            var paso = Vista == VistaCalendario.Dia ? 1 : 7;
            Anterior = Desde.AddDays(-paso);
            Siguiente = Desde.AddDays(paso);
        }

        Data = await sender.Send(new GetCalendarioQuery(Desde, Hasta), ct);
        ReunionesPorDia = Data.Reuniones.ToLookup(r => r.Fecha);
        ActividadPorDia = Data.Actividad.ToLookup(e => e.Fecha);
    }
}
