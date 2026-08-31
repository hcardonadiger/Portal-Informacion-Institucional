namespace Diger.TramitesEstado.Application.Proyectos.Queries;

/// <summary>Una reunión vinculada, tal como la ve la ficha del proyecto.</summary>
public sealed record ReunionVinculadaDto(
    int       VinculoId,
    int       ReunionId,
    string    Titulo,
    DateOnly? Fecha,
    string?   Institucion,
    string?   Tipo,
    int       Asistentes,
    string?   Nota,
    string    VinculadoPor,
    DateTime  VinculadoEn);

/// <summary>Un expediente vinculado.</summary>
public sealed record ExpedienteVinculadoDto(
    int       VinculoId,
    int       ExpedienteId,
    string    Codigo,
    string    Institucion,
    string    Estado,
    DateOnly? FechaApertura,
    string?   Nota,
    string    VinculadoPor,
    DateTime  VinculadoEn);

/// <summary>Un ticket de soporte vinculado.</summary>
public sealed record TicketVinculadoDto(
    int       VinculoId,
    int       TicketId,
    string    Numero,
    string    Titulo,
    string    Estado,
    string    Prioridad,
    string?   AsignadoA,
    DateTime  Creado,
    string?   Nota,
    string    VinculadoPor,
    DateTime  VinculadoEn);

/// <summary>
/// Lo que la ficha muestra en su pestaña de vínculos.
/// </summary>
/// <param name="FueraDeAlcance">Vínculos que existen pero cuyo destino esta persona no puede abrir.
/// <b>Se cuentan, no se esconden.</b> Ver la nota de la consulta.</param>
public sealed record VinculosProyectoDto(
    IReadOnlyList<ReunionVinculadaDto>    Reuniones,
    IReadOnlyList<ExpedienteVinculadoDto> Expedientes,
    IReadOnlyList<TicketVinculadoDto>     Tickets,
    int ReunionesFueraDeAlcance,
    int ExpedientesFueraDeAlcance,
    int TicketsFueraDeAlcance)
{
    public bool HayAlguno => Reuniones.Count > 0 || Expedientes.Count > 0 || Tickets.Count > 0
                          || ReunionesFueraDeAlcance > 0 || ExpedientesFueraDeAlcance > 0
                          || TicketsFueraDeAlcance > 0;
}

/// <summary>
/// Las reuniones y expedientes vinculados a un proyecto.
///
/// <para><b>El desajuste de alcance, y qué se hace con él.</b> El vínculo se ancla en el proyecto,
/// pero la reunión y el expediente llevan su propio filtro —y no es el mismo—. El de
/// <see cref="Proyecto"/> deja ver el proyecto al responsable y a los interesados aunque caigan
/// fuera de su institución; el de <see cref="Reunion"/> no tiene esa salida y además esconde las
/// privadas de otros. Sumado a que la institución del proyecto es la EJECUTORA y la de la reunión
/// la beneficiaria, el resultado es que alguien puede abrir el proyecto y no poder abrir la mitad
/// de lo que tiene vinculado.</para>
///
/// <para>Había tres salidas y las tres tienen coste: respetar los filtros y que la pestaña mienta
/// por omisión; saltarlos con <c>IgnoreQueryFilters</c> y fugar reuniones privadas; o mostrar lo
/// que se alcanza <b>y decir cuántos quedan fuera</b>. Se eligió la tercera: un conteo no revela
/// nada del contenido —ni título, ni institución, ni fecha— y evita que una ficha con seis
/// reuniones vinculadas se lea como si tuviera dos.</para>
/// </summary>
public sealed record GetVinculosProyectoQuery(int ProyectoId) : IRequest<VinculosProyectoDto>;

public sealed class GetVinculosProyectoQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetVinculosProyectoQuery, VinculosProyectoDto>
{
    public async Task<VinculosProyectoDto> Handle(GetVinculosProyectoQuery q, CancellationToken ct)
    {
        // Los vínculos van filtrados por el proyecto: pedir los de un proyecto ajeno da vacío.
        var vReuniones = await ctx.ProyectoReuniones.AsNoTracking()
            .Where(x => x.ProyectoId == q.ProyectoId)
            .Select(x => new { x.Id, x.ReunionId, x.Nota, x.VinculadoPor, x.VinculadoEn })
            .ToListAsync(ct);

        var vExpedientes = await ctx.ProyectoExpedientes.AsNoTracking()
            .Where(x => x.ProyectoId == q.ProyectoId)
            .Select(x => new { x.Id, x.ExpedienteId, x.Nota, x.VinculadoPor, x.VinculadoEn })
            .ToListAsync(ct);

        // Y los destinos van por SU propio filtro, sin excepción: lo que no se alcance no aparece.
        var reunionIds = vReuniones.Select(x => x.ReunionId).ToList();
        var reuniones = await ctx.Reuniones.AsNoTracking()
            .Where(r => reunionIds.Contains(r.Id))
            .Select(r => new
            {
                r.Id, r.Titulo, r.Fecha, r.Institucion, r.Tipo,
                Asistentes = ctx.Asistentes.Count(a => a.ReunionId == r.Id)
            })
            .ToDictionaryAsync(r => r.Id, ct);

        var vTickets = await ctx.ProyectoTickets.AsNoTracking()
            .Where(x => x.ProyectoId == q.ProyectoId)
            .Select(x => new { x.Id, x.TicketId, x.Nota, x.VinculadoPor, x.VinculadoEn })
            .ToListAsync(ct);

        var expedienteIds = vExpedientes.Select(x => x.ExpedienteId).ToList();
        var expedientes = await ctx.Expedientes.AsNoTracking()
            .Where(e => expedienteIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Codigo, e.Institucion, e.EstadoExpediente, e.FechaApertura })
            .ToDictionaryAsync(e => e.Id, ct);

        var ticketIds = vTickets.Select(x => x.TicketId).ToList();
        var tickets = await ctx.Tickets.AsNoTracking()
            .Where(t => ticketIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Numero, t.Titulo, t.Estado, t.Prioridad, t.AsignadoA, t.CreatedAt })
            .ToDictionaryAsync(t => t.Id, ct);

        var filasReuniones = vReuniones
            .Where(v => reuniones.ContainsKey(v.ReunionId))
            .Select(v =>
            {
                var r = reuniones[v.ReunionId];
                return new ReunionVinculadaDto(
                    v.Id, r.Id, r.Titulo, r.Fecha, r.Institucion, r.Tipo, r.Asistentes,
                    v.Nota, v.VinculadoPor, v.VinculadoEn);
            })
            .OrderByDescending(r => r.Fecha ?? DateOnly.MinValue)
            .ThenBy(r => r.Titulo)
            .ToList();

        var filasExpedientes = vExpedientes
            .Where(v => expedientes.ContainsKey(v.ExpedienteId))
            .Select(v =>
            {
                var e = expedientes[v.ExpedienteId];
                return new ExpedienteVinculadoDto(
                    v.Id, e.Id, e.Codigo, e.Institucion, e.EstadoExpediente.ToString(),
                    e.FechaApertura, v.Nota, v.VinculadoPor, v.VinculadoEn);
            })
            .OrderBy(e => e.Codigo)
            .ToList();

        var filasTickets = vTickets
            .Where(v => tickets.ContainsKey(v.TicketId))
            .Select(v =>
            {
                var t = tickets[v.TicketId];
                return new TicketVinculadoDto(
                    v.Id, t.Id, t.Numero, t.Titulo, t.Estado.ToString(), t.Prioridad.ToString(),
                    t.AsignadoA, t.CreatedAt, v.Nota, v.VinculadoPor, v.VinculadoEn);
            })
            // Los abiertos primero: en un ticket cerrado ya no hay nada que hacer y su lugar es
            // el histórico, no el encabezado de la lista.
            .OrderByDescending(t => t.Estado != "Cerrado" && t.Estado != "Resuelto")
            .ThenByDescending(t => t.Creado)
            .ToList();

        return new VinculosProyectoDto(
            filasReuniones, filasExpedientes, filasTickets,
            vReuniones.Count   - filasReuniones.Count,
            vExpedientes.Count - filasExpedientes.Count,
            vTickets.Count     - filasTickets.Count);
    }
}

/// <summary>Una opción del selector para vincular. Solo lo que la persona puede ver.</summary>
public sealed record OpcionVinculoDto(int Id, string Etiqueta);

/// <summary>
/// Lo que se puede vincular a este proyecto: reuniones y expedientes dentro del alcance de quien
/// pregunta, <b>menos los ya vinculados</b>. Ofrecer algo que el comando va a rechazar por
/// duplicado sería prometer una acción que no se puede hacer.
/// </summary>
public sealed record GetVinculablesQuery(int ProyectoId) : IRequest<(IReadOnlyList<OpcionVinculoDto> Reuniones,
                                                                    IReadOnlyList<OpcionVinculoDto> Expedientes,
                                                                    IReadOnlyList<OpcionVinculoDto> Tickets)>;

public sealed class GetVinculablesQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetVinculablesQuery, (IReadOnlyList<OpcionVinculoDto>,
                                            IReadOnlyList<OpcionVinculoDto>,
                                            IReadOnlyList<OpcionVinculoDto>)>
{
    public async Task<(IReadOnlyList<OpcionVinculoDto>, IReadOnlyList<OpcionVinculoDto>,
                       IReadOnlyList<OpcionVinculoDto>)> Handle(
        GetVinculablesQuery q, CancellationToken ct)
    {
        var yaReuniones = await ctx.ProyectoReuniones.AsNoTracking()
            .Where(x => x.ProyectoId == q.ProyectoId).Select(x => x.ReunionId).ToListAsync(ct);

        var yaExpedientes = await ctx.ProyectoExpedientes.AsNoTracking()
            .Where(x => x.ProyectoId == q.ProyectoId).Select(x => x.ExpedienteId).ToListAsync(ct);

        var reuniones = await ctx.Reuniones.AsNoTracking()
            .Where(r => !yaReuniones.Contains(r.Id))
            .OrderByDescending(r => r.Fecha)
            .Take(300)
            .Select(r => new OpcionVinculoDto(
                r.Id,
                (r.Fecha != null ? r.Fecha.Value.ToString("yyyy-MM-dd") + " · " : "") + r.Titulo))
            .ToListAsync(ct);

        var expedientes = await ctx.Expedientes.AsNoTracking()
            .Where(e => !yaExpedientes.Contains(e.Id))
            .OrderBy(e => e.Codigo)
            .Take(300)
            .Select(e => new OpcionVinculoDto(e.Id, e.Codigo + " · " + e.Institucion))
            .ToListAsync(ct);

        var yaTickets = await ctx.ProyectoTickets.AsNoTracking()
            .Where(x => x.ProyectoId == q.ProyectoId).Select(x => x.TicketId).ToListAsync(ct);

        // Se ofrecen también los cerrados, a diferencia de lo que hace el selector de proyectos del
        // otro extremo: un ticket ya resuelto es exactamente lo que alguien quiere adjuntar cuando
        // documenta a posteriori de dónde salió un requerimiento del proyecto.
        var tickets = await ctx.Tickets.AsNoTracking()
            .Where(t => !yaTickets.Contains(t.Id))
            .OrderByDescending(t => t.CreatedAt)
            .Take(300)
            .Select(t => new OpcionVinculoDto(t.Id, t.Numero + " · " + t.Titulo))
            .ToListAsync(ct);

        return (reuniones, expedientes, tickets);
    }
}

// ── El mismo vínculo, visto desde el otro extremo ─────────────────────────
/// <summary>Un proyecto al que está vinculada esta reunión o este expediente.</summary>
public sealed record ProyectoVinculadoDto(
    int      VinculoId,
    int      ProyectoId,
    string   Codigo,
    string   Nombre,
    string   Estado,
    int      AvancePct,
    string?  Responsable,
    string?  Nota,
    string   VinculadoPor,
    DateTime VinculadoEn);

/// <param name="FueraDeAlcance">Vínculos cuyo proyecto esta persona no puede abrir. Se cuentan,
/// igual que en la ficha del proyecto y por el mismo motivo.</param>
public sealed record ProyectosVinculadosDto(
    IReadOnlyList<ProyectoVinculadoDto> Proyectos,
    int FueraDeAlcance,
    IReadOnlyList<OpcionVinculoDto> Vinculables);

/// <summary>
/// Los proyectos de una reunión, y los que se le podrían vincular.
///
/// <para>Es la misma relación de <see cref="GetVinculosProyectoQuery"/> leída al revés, y arrastra
/// el mismo desajuste: el vínculo se ancla en el proyecto, así que desde acá solo se ven los
/// proyectos que la persona alcanza. Los que no, se cuentan.</para>
/// </summary>
public sealed record GetProyectosDeReunionQuery(int ReunionId) : IRequest<ProyectosVinculadosDto>;

public sealed class GetProyectosDeReunionQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetProyectosDeReunionQuery, ProyectosVinculadosDto>
{
    public async Task<ProyectosVinculadosDto> Handle(GetProyectosDeReunionQuery q, CancellationToken ct)
    {
        // Sin filtro: hace falta el total para poder decir cuántos quedan fuera. No se expone
        // ningún dato de ellos, solo el número.
        var todos = await ctx.ProyectoReuniones.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.ReunionId == q.ReunionId)
            .Select(x => new { x.Id, x.ProyectoId, x.Nota, x.VinculadoPor, x.VinculadoEn })
            .ToListAsync(ct);

        var ids = todos.Select(x => x.ProyectoId).ToList();

        var proyectos = await ctx.Proyectos.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Codigo, p.Nombre, p.Estado, p.AvancePct, p.Responsable })
            .ToDictionaryAsync(p => p.Id, ct);

        var filas = todos
            .Where(x => proyectos.ContainsKey(x.ProyectoId))
            .Select(x =>
            {
                var p = proyectos[x.ProyectoId];
                return new ProyectoVinculadoDto(
                    x.Id, p.Id, p.Codigo, p.Nombre, p.Estado.ToString(), p.AvancePct,
                    p.Responsable, x.Nota, x.VinculadoPor, x.VinculadoEn);
            })
            .OrderBy(p => p.Codigo)
            .ToList();

        // Vinculables: proyectos abiertos a su alcance que todavía no están.
        var yaVinculados = filas.Select(f => f.ProyectoId).ToList();
        var vinculables = await ctx.Proyectos.AsNoTracking()
            .Where(p => !yaVinculados.Contains(p.Id)
                     && p.Estado != EstadoProyecto.Cerrado
                     && p.Estado != EstadoProyecto.Cancelado)
            .OrderBy(p => p.Codigo)
            .Select(p => new OpcionVinculoDto(p.Id, p.Codigo + " · " + p.Nombre))
            .ToListAsync(ct);

        return new ProyectosVinculadosDto(filas, todos.Count - filas.Count, vinculables);
    }
}

/// <summary>Los proyectos de un expediente. Mismas reglas que la reunión.</summary>
public sealed record GetProyectosDeExpedienteQuery(int ExpedienteId) : IRequest<ProyectosVinculadosDto>;

public sealed class GetProyectosDeExpedienteQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetProyectosDeExpedienteQuery, ProyectosVinculadosDto>
{
    public async Task<ProyectosVinculadosDto> Handle(GetProyectosDeExpedienteQuery q, CancellationToken ct)
    {
        var todos = await ctx.ProyectoExpedientes.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.ExpedienteId == q.ExpedienteId)
            .Select(x => new { x.Id, x.ProyectoId, x.Nota, x.VinculadoPor, x.VinculadoEn })
            .ToListAsync(ct);

        var ids = todos.Select(x => x.ProyectoId).ToList();

        var proyectos = await ctx.Proyectos.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Codigo, p.Nombre, p.Estado, p.AvancePct, p.Responsable })
            .ToDictionaryAsync(p => p.Id, ct);

        var filas = todos
            .Where(x => proyectos.ContainsKey(x.ProyectoId))
            .Select(x =>
            {
                var p = proyectos[x.ProyectoId];
                return new ProyectoVinculadoDto(
                    x.Id, p.Id, p.Codigo, p.Nombre, p.Estado.ToString(), p.AvancePct,
                    p.Responsable, x.Nota, x.VinculadoPor, x.VinculadoEn);
            })
            .OrderBy(p => p.Codigo)
            .ToList();

        var yaVinculados = filas.Select(f => f.ProyectoId).ToList();
        var vinculables = await ctx.Proyectos.AsNoTracking()
            .Where(p => !yaVinculados.Contains(p.Id)
                     && p.Estado != EstadoProyecto.Cerrado
                     && p.Estado != EstadoProyecto.Cancelado)
            .OrderBy(p => p.Codigo)
            .Select(p => new OpcionVinculoDto(p.Id, p.Codigo + " · " + p.Nombre))
            .ToListAsync(ct);

        return new ProyectosVinculadosDto(filas, todos.Count - filas.Count, vinculables);
    }
}

/// <summary>Los proyectos de un ticket. Mismas reglas que la reunión y el expediente.</summary>
public sealed record GetProyectosDeTicketQuery(int TicketId) : IRequest<ProyectosVinculadosDto>;

public sealed class GetProyectosDeTicketQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetProyectosDeTicketQuery, ProyectosVinculadosDto>
{
    public async Task<ProyectosVinculadosDto> Handle(GetProyectosDeTicketQuery q, CancellationToken ct)
    {
        var todos = await ctx.ProyectoTickets.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.TicketId == q.TicketId)
            .Select(x => new { x.Id, x.ProyectoId, x.Nota, x.VinculadoPor, x.VinculadoEn })
            .ToListAsync(ct);

        var ids = todos.Select(x => x.ProyectoId).ToList();

        var proyectos = await ctx.Proyectos.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Codigo, p.Nombre, p.Estado, p.AvancePct, p.Responsable })
            .ToDictionaryAsync(p => p.Id, ct);

        var filas = todos
            .Where(x => proyectos.ContainsKey(x.ProyectoId))
            .Select(x =>
            {
                var p = proyectos[x.ProyectoId];
                return new ProyectoVinculadoDto(
                    x.Id, p.Id, p.Codigo, p.Nombre, p.Estado.ToString(), p.AvancePct,
                    p.Responsable, x.Nota, x.VinculadoPor, x.VinculadoEn);
            })
            .OrderBy(p => p.Codigo)
            .ToList();

        var yaVinculados = filas.Select(f => f.ProyectoId).ToList();
        var vinculables = await ctx.Proyectos.AsNoTracking()
            .Where(p => !yaVinculados.Contains(p.Id)
                     && p.Estado != EstadoProyecto.Cerrado
                     && p.Estado != EstadoProyecto.Cancelado)
            .OrderBy(p => p.Codigo)
            .Select(p => new OpcionVinculoDto(p.Id, p.Codigo + " · " + p.Nombre))
            .ToListAsync(ct);

        return new ProyectosVinculadosDto(filas, todos.Count - filas.Count, vinculables);
    }
}
