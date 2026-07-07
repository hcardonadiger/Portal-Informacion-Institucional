using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.Calendario;

[Authorize]
public sealed class IndexModel(ISender sender) : PageModel
{
    public CalendarioDto Data { get; private set; } = default!;

    public DateOnly PrimerDia { get; private set; }
    public DateOnly Hoy { get; private set; } = DateOnly.FromDateTime(DateTime.Today);

    public ILookup<DateOnly, ReunionCalendarioDto> ReunionesPorDia { get; private set; } = default!;
    public ILookup<DateOnly, EventoCalendarioDto>  ActividadPorDia { get; private set; } = default!;

    public bool PuedeGestionar => User.CanMutate();

    public DateOnly MesAnterior  => PrimerDia.AddMonths(-1);
    public DateOnly MesSiguiente => PrimerDia.AddMonths(1);

    public async Task OnGetAsync(int? anio, int? mes, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var a = anio ?? hoy.Year;
        var m = mes is >= 1 and <= 12 ? mes.Value : hoy.Month;

        Data = await sender.Send(new GetCalendarioQuery(a, m), ct);
        PrimerDia = new DateOnly(a, m, 1);
        ReunionesPorDia = Data.Reuniones.ToLookup(r => r.Fecha);
        ActividadPorDia = Data.Actividad.ToLookup(e => e.Fecha);
    }
}
