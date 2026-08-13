using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.Calendario;

[Authorize]
[Permission("Calendario", AccionModulo.Ver, "Ver el calendario")]
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

    public bool PuedeGestionar => HttpContext.CanMutate();

    /// <summary>Domingo de la semana que contiene <paramref name="d"/> (la grilla arranca en domingo).</summary>
    public static DateOnly InicioSemana(DateOnly d) => d.AddDays(-(int)d.DayOfWeek);

    public async Task OnGetAsync(string? vista, DateOnly? fecha, CancellationToken ct)
    {
        Vista = Enum.TryParse<VistaCalendario>(vista, ignoreCase: true, out var v) ? v : VistaCalendario.Mes;

        var referencia = fecha ?? Hoy;

        (Desde, Hasta) = Vista switch
        {
            VistaCalendario.Dia    => (referencia, referencia.AddDays(1)),
            VistaCalendario.Semana => (InicioSemana(referencia), InicioSemana(referencia).AddDays(7)),
            _                      => (new DateOnly(referencia.Year, referencia.Month, 1),
                                       new DateOnly(referencia.Year, referencia.Month, 1).AddMonths(1)),
        };

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

    public async Task<IActionResult> OnGetDataAsync(string? vista, DateOnly? fecha, CancellationToken ct)
    {
        var v = Enum.TryParse<VistaCalendario>(vista, ignoreCase: true, out var vp) ? vp : VistaCalendario.Mes;
        var referencia = fecha ?? DateOnly.FromDateTime(DateTime.Today);

        var (desde, hasta) = v switch
        {
            VistaCalendario.Dia    => (referencia, referencia.AddDays(1)),
            VistaCalendario.Semana => (InicioSemana(referencia), InicioSemana(referencia).AddDays(7)),
            _                      => (new DateOnly(referencia.Year, referencia.Month, 1),
                                       new DateOnly(referencia.Year, referencia.Month, 1).AddMonths(1)),
        };

        var data = await sender.Send(new GetCalendarioQuery(desde, hasta), ct);

        var eventosUi = Enum.GetValues<TipoEventoCalendario>()
            .Select(t => { var ui = CalendarioUi.Evento(t); return new { tipo = t.ToString(), ui.Etiqueta, ui.Color, ui.Fondo, ui.Texto, ui.IconoId }; })
            .ToList();

        return new JsonResult(new
        {
            desde = desde.ToString("yyyy-MM-dd"),
            hasta = hasta.ToString("yyyy-MM-dd"),
            reuniones = data.Reuniones.Select(r => new
            {
                r.Id, r.Titulo, fecha = r.Fecha.ToString("yyyy-MM-dd"), r.Hora, r.Tipo, r.Institucion, r.Privada
            }),
            actividad = data.Actividad.Select(e => new
            {
                fecha = e.Fecha.ToString("yyyy-MM-dd"),
                cuando = e.Cuando.ToString("yyyy-MM-ddTHH:mm"),
                tipo = e.Tipo.ToString(),
                e.Titulo, e.Detalle, e.Etiqueta, e.Pagina, e.RefId
            }),
            tiposUi = eventosUi,
            puedeGestionar = HttpContext.CanMutate()
        });
    }
}
