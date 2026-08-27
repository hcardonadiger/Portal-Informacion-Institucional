namespace Diger.TramitesEstado.Application.Proyectos.Common;

/// <summary>Cómo se pinta una barra. Es estado visual, no el estado del dominio: una actividad
/// «EnProceso» cuya ventana venció se dibuja como vencida.</summary>
public enum EstadoBarra { Pendiente, EnProceso, Completada, Vencida, Cancelada }

/// <summary>
/// Una fila del cronograma. Las posiciones vienen en porcentaje del ancho total para que la vista
/// no tenga que hacer aritmética de fechas: solo aplica <c>left</c> y <c>width</c>.
/// </summary>
public sealed record BarraCronograma(
    int         Nivel,           // 0 = entregable, 1 = actividad
    int         Id,
    string      Nombre,
    string?     Responsable,
    DateOnly?   Inicio,
    DateOnly?   Fin,
    double      OffsetPct,
    double      AnchoPct,
    int         AvancePct,
    EstadoBarra Estado,
    bool        Bloqueada,
    IReadOnlyList<string> Espera,

    /// <summary>Fecha comprometida del entregable: se dibuja como hito, no como barra. Null en
    /// las actividades, que no tienen compromiso propio.</summary>
    DateOnly?   Compromiso,
    double?     CompromisoPct)
{
    public bool EsEntregable => Nivel == 0;
}

/// <summary>Una división del eje de tiempo. El ancho no es fijo: los meses tienen 28 a 31 días y
/// dibujarlos iguales corre las barras respecto de la escala.</summary>
public sealed record MesCronograma(string Etiqueta, string Anio, double OffsetPct, double AnchoPct);

public sealed record CronogramaDto(
    DateOnly? Desde,
    DateOnly? Hasta,
    IReadOnlyList<MesCronograma>   Meses,
    IReadOnlyList<BarraCronograma> Barras,

    /// <summary>Lo que no se puede dibujar por no tener fechas. Va aparte y a la vista, no se
    /// omite: en el portafolio real son 178 de 191 actividades, y esconderlas daría a entender
    /// que el cronograma está completo.</summary>
    IReadOnlyList<BarraCronograma> SinFechas,

    /// <summary>Posición de hoy en la escala, o null si hoy cae fuera del rango dibujado.</summary>
    double? HoyPct)
{
    public bool TieneQueDibujar => Barras.Count > 0;
    public int  TotalDias => Desde is { } d && Hasta is { } h ? h.DayNumber - d.DayNumber + 1 : 0;
}

/// <summary>
/// Arma el cronograma de un proyecto a partir de su ficha.
///
/// <para>Vive en la capa de aplicación y no en la vista para poder probar la aritmética: los
/// desplazamientos en porcentaje son fáciles de equivocar por un día y el error solo se ve
/// mirando la pantalla con mucha atención.</para>
///
/// <para><b>La escala sale de los datos, no del proyecto.</b> Si una actividad se pasa de la fecha
/// de cierre planificada del proyecto, el eje se estira para incluirla: dibujar hasta la fecha
/// comprometida recortaría justamente lo que hay que ver.</para>
/// </summary>
public static class CronogramaProyecto
{
    public static CronogramaDto Construir(ProyectoDetailDto p, DateOnly hoy)
    {
        var conFechas  = new List<BarraCronograma>();
        var sinFechas  = new List<BarraCronograma>();

        // 1. Rango. Se toma de todo lo que tenga fecha —actividades, compromisos de entregable y
        //    la ventana del proyecto— y se redondea a mes completo para que el eje empiece y
        //    termine en un borde legible.
        var fechas = new List<DateOnly>();
        foreach (var e in p.Entregables)
        {
            if (e.FechaPlan is { } fp) fechas.Add(fp);
            foreach (var a in e.Actividades)
            {
                if (a.FechaInicioPlan is { } ini) fechas.Add(ini);
                if (a.FechaFinPlan    is { } fin) fechas.Add(fin);
            }
        }
        if (p.FechaInicioPlan is { } pi) fechas.Add(pi);
        if (p.FechaFinPlan    is { } pf) fechas.Add(pf);

        if (fechas.Count == 0)
            return new CronogramaDto(null, null, [], [], TodasSinFecha(p), null);

        var desde = PrimerDiaDelMes(fechas.Min());
        var hasta = UltimoDiaDelMes(fechas.Max());
        var total = (double)(hasta.DayNumber - desde.DayNumber + 1);

        double Pos(DateOnly f) => (f.DayNumber - desde.DayNumber) / total * 100d;

        // 2. Meses del eje, cada uno con su ancho real.
        var meses = new List<MesCronograma>();
        for (var m = desde; m <= hasta; m = m.AddMonths(1))
        {
            var finMes = UltimoDiaDelMes(m);
            if (finMes > hasta) finMes = hasta;

            meses.Add(new MesCronograma(
                Etiqueta:  Cultura.MesCorto(m.Month),
                Anio:      m.Year.ToString(),
                OffsetPct: Pos(m),
                AnchoPct:  (finMes.DayNumber - m.DayNumber + 1) / total * 100d));
        }

        // 3. Filas. El entregable va primero y después sus actividades, en el orden de la ficha.
        foreach (var e in p.Entregables)
        {
            var (ini, fin) = RangoDe(e);

            conFechas.Add(new BarraCronograma(
                Nivel: 0, Id: e.Id, Nombre: e.Nombre, Responsable: e.Responsable,
                Inicio: ini, Fin: fin,
                OffsetPct: ini is { } i ? Pos(i) : 0,
                AnchoPct:  ini is { } i2 && fin is { } f2 ? Ancho(Pos(i2), Pos(f2), total) : 0,
                AvancePct: e.AvancePct,
                Estado: EstadoDe(e, hoy),
                Bloqueada: false,
                Espera: [],
                Compromiso:    e.FechaPlan,
                CompromisoPct: e.FechaPlan is { } fp ? Pos(fp) : null));

            foreach (var a in e.Actividades)
            {
                var barra = new BarraCronograma(
                    Nivel: 1, Id: a.Id, Nombre: a.Nombre, Responsable: a.Responsable,
                    Inicio: a.FechaInicioPlan, Fin: a.FechaFinPlan,
                    OffsetPct: a.FechaInicioPlan is { } ai ? Pos(ai) : 0,
                    AnchoPct:  a.FechaInicioPlan is { } ai2 && a.FechaFinPlan is { } af
                               ? Ancho(Pos(ai2), Pos(af), total) : 0,
                    AvancePct: a.AvancePct,
                    Estado: EstadoDe(a, hoy),
                    Bloqueada: a.Bloqueada,
                    Espera: a.PredecesorasPendientes.Select(x => x.Nombre).ToList(),
                    Compromiso: null, CompromisoPct: null);

                // Sin las dos fechas no hay barra que dibujar. Se muestra en la lista de abajo.
                if (a.FechaInicioPlan is null || a.FechaFinPlan is null) sinFechas.Add(barra);
                else conFechas.Add(barra);
            }
        }

        // Un entregable cuyas actividades no tienen fecha tampoco tiene barra: se queda como
        // encabezado sin dibujo, que es honesto — no hay nada que mostrar de él.
        double? hoyPct = hoy >= desde && hoy <= hasta ? Pos(hoy) : null;

        return new CronogramaDto(desde, hasta, meses, conFechas, sinFechas, hoyPct);
    }

    /// <summary>El ancho mínimo es de un día: una actividad que empieza y termina el mismo día
    /// tiene que verse, y con 0 % desaparecería.</summary>
    private static double Ancho(double desde, double hasta, double total)
    {
        var unDia = 100d / total;
        return Math.Max(hasta - desde + unDia, unDia);
    }

    /// <summary>Ventana que ocupan las actividades del entregable. Es lo que se dibuja como barra
    /// resumen; su fecha comprometida va aparte, como hito.</summary>
    private static (DateOnly? Inicio, DateOnly? Fin) RangoDe(EntregableProyectoDto e)
    {
        var inicios = e.Actividades.Where(a => a.FechaInicioPlan.HasValue).Select(a => a.FechaInicioPlan!.Value).ToList();
        var fines   = e.Actividades.Where(a => a.FechaFinPlan.HasValue).Select(a => a.FechaFinPlan!.Value).ToList();

        if (inicios.Count == 0 || fines.Count == 0) return (null, null);
        return (inicios.Min(), fines.Max());
    }

    private static EstadoBarra EstadoDe(ActividadProyectoDto a, DateOnly hoy) =>
        a.Estado switch
        {
            EstadoActividad.Cancelada  => EstadoBarra.Cancelada,
            EstadoActividad.Completada => EstadoBarra.Completada,
            _ when a.FechaFinPlan is { } f && f < hoy => EstadoBarra.Vencida,
            EstadoActividad.EnProceso  => EstadoBarra.EnProceso,
            _                          => EstadoBarra.Pendiente
        };

    private static EstadoBarra EstadoDe(EntregableProyectoDto e, DateOnly hoy) =>
        e.Estado switch
        {
            EstadoEntregable.Cancelado  => EstadoBarra.Cancelada,
            EstadoEntregable.Completado => EstadoBarra.Completada,
            _ when e.FechaPlan is { } f && f < hoy => EstadoBarra.Vencida,
            EstadoEntregable.EnProceso  => EstadoBarra.EnProceso,
            _                           => EstadoBarra.Pendiente
        };

    private static IReadOnlyList<BarraCronograma> TodasSinFecha(ProyectoDetailDto p) =>
        p.Entregables
            .SelectMany(e => e.Actividades.Select(a => new BarraCronograma(
                1, a.Id, a.Nombre, a.Responsable, null, null, 0, 0, a.AvancePct,
                a.EstaCancelada ? EstadoBarra.Cancelada : EstadoBarra.Pendiente,
                a.Bloqueada, a.PredecesorasPendientes.Select(x => x.Nombre).ToList(), null, null)))
            .ToList();

    private static DateOnly PrimerDiaDelMes(DateOnly f) => new(f.Year, f.Month, 1);
    private static DateOnly UltimoDiaDelMes(DateOnly f) => new(f.Year, f.Month, DateTime.DaysInMonth(f.Year, f.Month));

    /// <summary>Los meses en español, sin depender de la cultura del servidor — que en este
    /// entorno no siempre es es-HN.</summary>
    private static class Cultura
    {
        private static readonly string[] Meses =
            ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];

        public static string MesCorto(int mes) => Meses[mes - 1];
    }
}
