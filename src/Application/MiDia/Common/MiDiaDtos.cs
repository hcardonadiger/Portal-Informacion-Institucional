namespace Diger.TramitesEstado.Application.MiDia.Common;

/// <summary>
/// De qué módulo salió el pendiente.
///
/// <para>Va en cada fila porque una bandeja que mezcla orígenes solo se entiende si cada línea dice
/// de dónde viene: un compromiso se acuerda en una reunión, una actividad cuelga de un proyecto,
/// una meta la fija el plan de trabajo del año y un ticket lo reporta alguien más. El verbo con el
/// que se cierra cada uno es distinto, y esconderlo obliga a abrirlos para saber qué son.</para>
/// </summary>
public enum OrigenPendiente { Compromiso, Actividad, Meta, Ticket }

/// <summary>
/// La franja en la que cae un pendiente respecto de hoy. Es el único criterio con el que se ordena
/// la bandeja: la pregunta que contesta esta pantalla es «¿qué me toca hoy?», no «¿qué tengo en el
/// módulo X?» —para eso ya está el tablero de cada módulo—.
/// </summary>
public enum FranjaDia { Atrasado, Hoy, Manana, EstaSemana, MasAdelante, SinFecha }

/// <summary>
/// Una cosa que la persona conectada tiene que cerrar, con el contexto mínimo para decidir si la
/// atiende ahora y el enlace a donde efectivamente se resuelve.
/// </summary>
/// <param name="Pagina">Ruta Razor de destino, en el mismo formato que usa el calendario
/// (<c>/Tickets/Detalle</c>). La página arma el enlace con <paramref name="RefId"/>.</param>
/// <param name="Fecha">La fecha contra la cual se mide el atraso, ya normalizada a día local. Cada
/// origen aporta la suya: el plazo del compromiso, el fin planificado de la actividad, el cierre
/// estimado de la meta, y en los tickets el vencimiento que resulta del SLA del tema.</param>
/// <param name="PermiteCierreRapido">Si esta fila puede avanzar desde la propia bandeja. Solo los
/// compromisos: son el único origen con un comando liviano de «ya lo hice» que no exige escribir
/// nada más. Las actividades y los tickets se resuelven en su módulo a propósito —cerrarlos pide
/// una bitácora o una nota de resolución, y saltárselas desde acá vaciaría de contenido justo lo
/// que después se lee en el informe—.</param>
public sealed record PendienteDto(
    OrigenPendiente Origen,
    int             RefId,
    string          Titulo,
    string          Contexto,
    string          Pagina,
    DateOnly?       Fecha,
    string          Estado,
    string?         Detalle = null,
    bool            PermiteCierreRapido = false)
{
    /// <summary>
    /// El día de la persona es el día local, no el UTC. Con UTC−6 todo lo registrado después de las
    /// 18:00 caería en «mañana» mientras el usuario sigue en su jornada de hoy.
    /// </summary>
    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>Hasta dónde llega «esta semana». Siete días corridos, no hasta el domingo: lo que
    /// importa es cuánto falta, y un jueves no debería vaciar la bandeja solo porque el fin de
    /// semana está cerca.</summary>
    public const int DiasSemana = 7;

    public FranjaDia Franja => Fecha switch
    {
        null                                          => FranjaDia.SinFecha,
        { } f when f <  Hoy                           => FranjaDia.Atrasado,
        { } f when f == Hoy                           => FranjaDia.Hoy,
        { } f when f == Hoy.AddDays(1)                => FranjaDia.Manana,
        { } f when f <= Hoy.AddDays(DiasSemana)       => FranjaDia.EstaSemana,
        _                                             => FranjaDia.MasAdelante
    };

    /// <summary>Negativo si ya venció. Null cuando no hay fecha contra la cual medir.</summary>
    public int? DiasParaVencer => Fecha is { } f ? f.DayNumber - Hoy.DayNumber : null;

    public bool Atrasado => Franja == FranjaDia.Atrasado;

    /// <summary>
    /// Cae dentro de la ventana que la bandeja muestra por omisión: todo menos lo que vence más
    /// allá de la semana.
    ///
    /// <para>Lo que no tiene fecha <b>sí</b> entra. Es trabajo abierto y asignado —un ticket cuyo
    /// tema no define SLA, una actividad sin cierre planificado—, y esconderlo detrás del mismo
    /// interruptor que lo lejano dejaría a la persona con un contador que dice «3» sobre una lista
    /// que no los muestra: el peor de los dos mundos.</para>
    /// </summary>
    public bool EnHorizonte => Franja != FranjaDia.MasAdelante;
}

/// <summary>
/// Una reunión de la agenda propia. Va aparte de los pendientes y no se cuenta entre ellos: una
/// reunión no se «cierra» ni se atrasa —o se asistió o no—, y sumarla a los atrasados inflaría el
/// número que la persona usa para decidir por dónde empezar.
/// </summary>
public sealed record CitaDto(
    int       ReunionId,
    string    Titulo,
    DateOnly  Fecha,
    string?   Hora,
    string?   Lugar,
    string?   Institucion,
    bool      EsOrganizador)
{
    public bool EsHoy => Fecha == DateOnly.FromDateTime(DateTime.Now);
}

/// <summary>La bandeja completa: lo que hay que cerrar, lo que hay agendado, y los contadores que
/// encabezan la pantalla.</summary>
/// <param name="Ocultos">Cuántos pendientes quedaron fuera de la vista por caer más allá del
/// horizonte. Se informa en vez de callarse: un cero en la lista no significa lo mismo que un cero
/// en el total.</param>
public sealed record MiDiaDto(
    IReadOnlyList<PendienteDto> Pendientes,
    IReadOnlyList<CitaDto>      Agenda,
    int Atrasados,
    int ParaHoy,
    int ParaManana,
    int EstaSemana,
    int MasAdelante,
    int SinFecha,
    int TotalAbiertos,
    int Ocultos)
{
    public static MiDiaDto Vacio => new([], [], 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Nada atrasado y nada para hoy. Lo usa la pantalla para felicitar en vez de mostrar
    /// una lista vacía sin explicación.</summary>
    public bool AlDia => Atrasados == 0 && ParaHoy == 0;
}
