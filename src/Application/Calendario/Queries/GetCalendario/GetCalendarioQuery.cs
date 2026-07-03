namespace Diger.TramitesEstado.Application.Calendario.Queries.GetCalendario;

public enum TipoEventoCalendario
{
    Reunion, TicketCreado, TicketRespuesta, TicketCambioEstado, TicketAsignacion, ExpedienteCreado
}

/// <summary>Reunión programada (chip editable en la grilla del calendario).</summary>
public sealed record ReunionCalendarioDto(
    int Id, string Titulo, DateOnly Fecha, string? Hora, string? Tipo, string? Institucion, bool Privada);

/// <summary>Acción registrada en el portal (feed de actividad).</summary>
public sealed record EventoCalendarioDto(
    DateOnly Fecha, DateTime Cuando, TipoEventoCalendario Tipo,
    string Titulo, string? Detalle, string? Etiqueta, string Pagina, int RefId);

public sealed record CalendarioDto(
    int Anio, int Mes,
    IReadOnlyList<ReunionCalendarioDto> Reuniones,
    IReadOnlyList<EventoCalendarioDto>  Actividad);

public sealed record GetCalendarioQuery(int Anio, int Mes) : IRequest<CalendarioDto>;

public sealed class GetCalendarioQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetCalendarioQuery, CalendarioDto>
{
    public async Task<CalendarioDto> Handle(GetCalendarioQuery q, CancellationToken ct)
    {
        var desdeD = new DateOnly(q.Anio, q.Mes, 1);
        var hastaD = desdeD.AddMonths(1);

        // Ventana UTC ampliada ±1 día para no cortar eventos por la diferencia horaria;
        // luego se filtra por fecha local.
        var desdeUtc = new DateTime(q.Anio, q.Mes, 1, 0, 0, 0, DateTimeKind.Unspecified).AddDays(-1);
        var hastaUtc = desdeD.AddMonths(1).ToDateTime(TimeOnly.MinValue).AddDays(1);

        // ── Reuniones del mes (por su fecha programada) ──
        // Se selecciona el enum Visibilidad y se calcula "Privada" en memoria: proyectar la igualdad
        // del enum value-converted en SQL hace que EF genere un XOR ('^') inválido sobre nvarchar.
        var reunionesRaw = await ctx.Reuniones.AsNoTracking()
            .Where(r => r.Fecha != null && r.Fecha >= desdeD && r.Fecha < hastaD)
            .OrderBy(r => r.Fecha).ThenBy(r => r.Hora)
            .Select(r => new { r.Id, r.Titulo, Fecha = r.Fecha!.Value, r.Hora, r.Tipo, r.Institucion, r.Visibilidad })
            .ToListAsync(ct);
        var reuniones = reunionesRaw
            .Select(r => new ReunionCalendarioDto(r.Id, r.Titulo, r.Fecha, r.Hora, r.Tipo, r.Institucion,
                r.Visibilidad == VisibilidadReunion.Privada))
            .ToList();

        // ── Actividad: tickets creados, comentarios/respuestas, expedientes ──
        var ticketsCreados = await ctx.Tickets.AsNoTracking()
            .Where(t => t.CreatedAt >= desdeUtc && t.CreatedAt < hastaUtc)
            .Select(t => new { t.Id, t.Numero, t.Titulo, t.Institucion, t.CreatedAt })
            .ToListAsync(ct);

        // El join contra Tickets (filtrado por alcance) limita los comentarios a tickets visibles.
        var comentarios = await (
            from c in ctx.TicketComentarios.AsNoTracking()
            join t in ctx.Tickets on c.TicketId equals t.Id
            where c.Fecha >= desdeUtc && c.Fecha < hastaUtc
            select new { c.Id, c.Tipo, c.Fecha, c.Texto, t.Numero, TId = t.Id, t.Titulo })
            .ToListAsync(ct);

        var expedientes = await ctx.Expedientes.AsNoTracking()
            .Where(e => e.CreatedAt >= desdeUtc && e.CreatedAt < hastaUtc)
            .Select(e => new { e.Id, e.Codigo, e.Institucion, e.CreatedAt })
            .ToListAsync(ct);

        var eventos = new List<EventoCalendarioDto>();

        void Agregar(DateTime cuandoUtc, TipoEventoCalendario tipo, string titulo,
                     string? detalle, string? etiqueta, string pagina, int refId)
        {
            var local = DateTime.SpecifyKind(cuandoUtc, DateTimeKind.Utc).ToLocalTime();
            var f = DateOnly.FromDateTime(local);
            if (f < desdeD || f >= hastaD) return;
            eventos.Add(new EventoCalendarioDto(f, local, tipo, titulo, detalle, etiqueta, pagina, refId));
        }

        foreach (var t in ticketsCreados)
            Agregar(t.CreatedAt, TipoEventoCalendario.TicketCreado, t.Titulo,
                t.Institucion, t.Numero, "/Tickets/Detalle", t.Id);

        foreach (var c in comentarios)
        {
            var tipo = c.Tipo switch
            {
                TipoComentarioTicket.CambioEstado => TipoEventoCalendario.TicketCambioEstado,
                TipoComentarioTicket.Asignacion   => TipoEventoCalendario.TicketAsignacion,
                _                                 => TipoEventoCalendario.TicketRespuesta
            };
            Agregar(c.Fecha, tipo, c.Titulo, c.Texto, c.Numero, "/Tickets/Detalle", c.TId);
        }

        foreach (var e in expedientes)
            Agregar(e.CreatedAt, TipoEventoCalendario.ExpedienteCreado, e.Institucion,
                "Expediente abierto", e.Codigo, "/Expedientes/Editor", e.Id);

        var actividad = eventos.OrderByDescending(e => e.Cuando).ToList();
        return new CalendarioDto(q.Anio, q.Mes, reuniones, actividad);
    }
}
