namespace Diger.TramitesEstado.Application.Notificaciones;

/// <summary>Qué es cada tipo de aviso para el Panel de control.</summary>
/// <param name="Etiqueta">Cómo se nombra el tipo en el panel y como encabezado del correo resumen.</param>
/// <param name="Plural">Título de un aviso agrupado con N elementos.</param>
/// <param name="Diario">Lo genera el revisor automático y se avisa una vez por día y elemento.</param>
/// <param name="CorreoInmediato">Recordatorio manual: manda su correo en el acto, sin bandeja.</param>
public sealed record DefinicionNotificacion(
    TipoNotificacion   Tipo,
    ModuloNotificacion Modulo,
    string             Etiqueta,
    Func<int, string>  Plural,
    bool               Diario          = false,
    bool               CorreoInmediato = false)
{
    public bool AdmiteCorreo => CatalogoNotificaciones.ModuloAdmiteCorreo(Modulo);
}

/// <summary>
/// A qué módulo pertenece cada <see cref="TipoNotificacion"/>. Vive en el código y no en la base:
/// un tipo es siempre del mismo módulo, y en la base alguien podría descuadrarlo. Un tipo nuevo se
/// declara acá; <c>CatalogoNotificacionesTests</c> falla si se olvida.
/// </summary>
public static class CatalogoNotificaciones
{
    private static readonly DefinicionNotificacion[] Definiciones =
    [
        new(TipoNotificacion.TicketAsignado,               ModuloNotificacion.Tickets,     "Tickets asignados",                n => $"Tiene {n} tickets asignados"),
        new(TipoNotificacion.TicketResuelto,               ModuloNotificacion.Tickets,     "Tickets resueltos",                n => $"{n} tickets resueltos"),
        new(TipoNotificacion.RecordatorioManualTicket,     ModuloNotificacion.Tickets,     "Recordatorios de tickets",         n => $"{n} recordatorios de tickets", CorreoInmediato: true),
        new(TipoNotificacion.ReuniónMañana,                ModuloNotificacion.Reuniones,   "Reuniones de mañana",              n => $"{n} reuniones mañana", Diario: true),
        new(TipoNotificacion.CompromisoVencido,            ModuloNotificacion.Reuniones,   "Compromisos vencidos",             n => $"Tiene {n} compromisos vencidos", Diario: true),
        new(TipoNotificacion.CompromisoProximo,            ModuloNotificacion.Reuniones,   "Compromisos por vencer",           n => $"{n} compromisos vencen en los próximos 3 días", Diario: true),
        new(TipoNotificacion.RecordatorioManualReunion,    ModuloNotificacion.Reuniones,   "Recordatorios de reuniones",       n => $"{n} recordatorios de reuniones", CorreoInmediato: true),
        new(TipoNotificacion.RecordatorioManualCompromiso, ModuloNotificacion.Reuniones,   "Recordatorios de compromisos",     n => $"{n} recordatorios de compromisos", CorreoInmediato: true),
        new(TipoNotificacion.EtapaCronogramaVencida,       ModuloNotificacion.Expedientes, "Etapas de cronograma vencidas",    n => $"{n} etapas de cronograma vencidas", Diario: true),
        new(TipoNotificacion.EtapaCronogramaProxima,       ModuloNotificacion.Expedientes, "Etapas de cronograma por vencer",  n => $"{n} etapas de cronograma vencen pronto", Diario: true),
        new(TipoNotificacion.RecordatorioManualExpediente, ModuloNotificacion.Expedientes, "Recordatorios de expedientes",     n => $"{n} recordatorios de expedientes", CorreoInmediato: true),
        new(TipoNotificacion.ProyectoCambioEstado,         ModuloNotificacion.Proyectos,   "Proyectos que cambiaron de estado", n => $"{n} proyectos cambiaron de estado"),
        new(TipoNotificacion.ChatRecibido,                 ModuloNotificacion.Soporte,     "Chats de soporte",                 n => $"{n} chats nuevos"),
    ];

    private static readonly Dictionary<TipoNotificacion, DefinicionNotificacion> PorTipo =
        Definiciones.ToDictionary(d => d.Tipo);

    public static IReadOnlyCollection<DefinicionNotificacion> Todas => Definiciones;

    public static DefinicionNotificacion De(TipoNotificacion tipo) =>
        PorTipo.TryGetValue(tipo, out var d)
            ? d
            : throw new InvalidOperationException($"El tipo de notificación {tipo} no tiene módulo en CatalogoNotificaciones.");

    public static IReadOnlyList<DefinicionNotificacion> DelModulo(ModuloNotificacion modulo) =>
        Definiciones.Where(d => d.Modulo == modulo).ToList();

    /// <summary>El chat de soporte va solo a la campana: un correo por cada chat sería ruido.</summary>
    public static bool ModuloAdmiteCorreo(ModuloNotificacion modulo) => modulo != ModuloNotificacion.Soporte;

    public static string Nombre(ModuloNotificacion modulo) => modulo switch
    {
        ModuloNotificacion.Tickets     => "Tickets",
        ModuloNotificacion.Reuniones   => "Reuniones",
        ModuloNotificacion.Expedientes => "Expedientes",
        ModuloNotificacion.Proyectos   => "Proyectos",
        ModuloNotificacion.Soporte     => "Soporte (chat)",
        _                              => modulo.ToString(),
    };
}
