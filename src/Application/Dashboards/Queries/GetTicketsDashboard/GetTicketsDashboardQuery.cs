using Diger.TramitesEstado.Application.Dashboards.Common;

namespace Diger.TramitesEstado.Application.Dashboards.Queries.GetTicketsDashboard;

public sealed record GetTicketsDashboardQuery(
    string? InstitucionId = null, DateOnly? Desde = null, DateOnly? Hasta = null,
    // Alcance del técnico: cuando TecnicoUserId != null, se limita a sus temas o sus tickets asignados.
    IReadOnlyList<int>? TecnicoTemaIds = null, Guid? TecnicoUserId = null)
    : IRequest<TicketsDashboardDto>;

public sealed class GetTicketsDashboardQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetTicketsDashboardQuery, TicketsDashboardDto>
{
    public async Task<TicketsDashboardDto> Handle(GetTicketsDashboardQuery q, CancellationToken ct)
    {
        // Base: alcance (filtro global) + institución elegida. Las tendencias usan esta base (12 meses).
        var baseq = ctx.Tickets.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.InstitucionId)) baseq = baseq.Where(t => t.InstitucionId == q.InstitucionId);

        // Alcance del técnico: solo sus temas (que atiende) o los tickets asignados a él.
        if (q.TecnicoUserId is Guid tuid)
        {
            var temaIds = q.TecnicoTemaIds ?? [];
            baseq = baseq.Where(t => (t.TemaId != null && temaIds.Contains(t.TemaId.Value)) || t.AsignadoAId == tuid);
        }

        // Filtrado adicional por rango de fechas (afecta conteos/distribuciones, no las tendencias).
        var filt = baseq;
        if (q.Desde is { } dd) { var d = dd.ToDateTime(TimeOnly.MinValue); filt = filt.Where(t => t.CreatedAt >= d); }
        if (q.Hasta is { } hh) { var h = hh.AddDays(1).ToDateTime(TimeOnly.MinValue); filt = filt.Where(t => t.CreatedAt < h); }

        var total = await filt.CountAsync(ct);
        var abiertos = await filt.CountAsync(x => x.Estado == EstadoTicket.Abierto || x.Estado == EstadoTicket.EnProgreso, ct);
        var criticos = await filt.CountAsync(x => x.Prioridad == PrioridadTicket.Critica &&
            (x.Estado == EstadoTicket.Abierto || x.Estado == EstadoTicket.EnProgreso), ct);
        var resueltos = await filt.CountAsync(x => x.Estado == EstadoTicket.Resuelto || x.Estado == EstadoTicket.Cerrado, ct);
        var pctResueltos = total == 0 ? 0 : (int)Math.Round(resueltos * 100.0 / total);

        // Tiempo promedio de resolución (días) — en memoria sobre los resueltos con fecha.
        var resData = await filt
            .Where(x => x.FechaResolucion != null)
            .Select(x => new { x.CreatedAt, F = x.FechaResolucion!.Value })
            .ToListAsync(ct);
        var diasProm = resData.Count == 0 ? 0 : (int)Math.Round(resData.Average(r => (r.F - r.CreatedAt).TotalDays));

        // SLA vencido: tickets abiertos cuyo tema tiene tiempo máximo (>0) ya superado.
        var ahoraSla = DateTime.UtcNow;
        var slaVencidos = await filt.CountAsync(x =>
            (x.Estado == EstadoTicket.Abierto || x.Estado == EstadoTicket.EnProgreso) &&
            x.TemaRef != null && x.TemaRef.HorasResolucion > 0 &&
            x.CreatedAt.AddHours(x.TemaRef.HorasResolucion) < ahoraSla, ct);

        var porEstado = await GrupoAsync(filt.GroupBy(x => x.Estado), ct);
        var porPrioridad = await GrupoAsync(filt.GroupBy(x => x.Prioridad), ct);
        var porTema = (await filt
            .GroupBy(x => x.TemaRef != null ? x.TemaRef.Nombre : null)
            .Select(g => new { Tema = g.Key, C = g.Count() }).ToListAsync(ct))
            .OrderByDescending(x => x.C)
            .Select(x => new ConteoDto(string.IsNullOrWhiteSpace(x.Tema) ? "Sin tema" : x.Tema!, x.C))
            .ToList();
        var porCategoria = (await filt
            .GroupBy(x => x.TemaRef != null && x.TemaRef.CategoriaRef != null ? x.TemaRef.CategoriaRef.Nombre : null)
            .Select(g => new { Cat = g.Key, C = g.Count() }).ToListAsync(ct))
            .OrderByDescending(x => x.C)
            .Select(x => new ConteoDto(string.IsNullOrWhiteSpace(x.Cat) ? "Sin categoría" : x.Cat!, x.C))
            .ToList();

        var porInstitucion = (await filt
            .GroupBy(x => x.Institucion).Select(g => new { Inst = g.Key, C = g.Count() }).ToListAsync(ct))
            .OrderByDescending(x => x.C)
            .Select(x => new ConteoDto(string.IsNullOrWhiteSpace(x.Inst) ? "Sin institución" : x.Inst!, x.C))
            .ToList();

        // Tendencias (últimos 12 meses, sobre la base con institución).
        var desde12 = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-11);
        var creadosRaw = (await baseq.Where(t => t.CreatedAt >= desde12)
            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, C = g.Count() }).ToListAsync(ct))
            .Select(x => (x.Year, x.Month, x.C));
        var resueltosRaw = (await baseq.Where(t => t.FechaResolucion != null && t.FechaResolucion >= desde12)
            .GroupBy(t => new { t.FechaResolucion!.Value.Year, t.FechaResolucion!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, C = g.Count() }).ToListAsync(ct))
            .Select(x => (x.Year, x.Month, x.C));

        // Tendencia mes actual vs anterior (creados).
        var hoy = DateTime.Now;
        var iniMes = new DateTime(hoy.Year, hoy.Month, 1);
        var iniMesAnt = iniMes.AddMonths(-1);
        var creadosMes = await baseq.CountAsync(t => t.CreatedAt >= iniMes, ct);
        var creadosMesAnt = await baseq.CountAsync(t => t.CreatedAt >= iniMesAnt && t.CreatedAt < iniMes, ct);

        var ahora = DateTime.UtcNow;
        var antiguos = (await filt
            .Where(x => x.Estado == EstadoTicket.Abierto || x.Estado == EstadoTicket.EnProgreso)
            .OrderBy(x => x.CreatedAt).Take(8)
            .Select(x => new { x.Id, x.Numero, x.Titulo, x.Institucion, x.CreatedAt, x.Prioridad }).ToListAsync(ct))
            .Select(x => new TicketAntiguedadDto(x.Id, x.Numero, x.Titulo, x.Institucion,
                Math.Max(0, (int)(ahora - x.CreatedAt).TotalDays), x.Prioridad))
            .ToList();

        return new TicketsDashboardDto(total, abiertos, criticos, resueltos, diasProm, pctResueltos, slaVencidos,
            new TendenciaDto(creadosMes, creadosMesAnt),
            porEstado, porPrioridad, porCategoria, porTema, porInstitucion,
            SerieMensual.Ultimos12(creadosRaw), SerieMensual.Ultimos12(resueltosRaw), antiguos);
    }

    private static async Task<IReadOnlyList<ConteoDto>> GrupoAsync<TEnum>(
        IQueryable<IGrouping<TEnum, Domain.Entities.Ticket>> grouped, CancellationToken ct) where TEnum : struct, Enum
    {
        var datos = (await grouped.Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct)).ToDictionary(x => x.Key, x => x.C);
        return Enum.GetValues<TEnum>().Select(e => new ConteoDto(Etiquetas.De(e), datos.TryGetValue(e, out var c) ? c : 0)).ToList();
    }
}

internal static class Etiquetas
{
    public static string De<TEnum>(TEnum value) where TEnum : Enum => value.ToString() switch
    {
        "EnProgreso"      => "En progreso",
        "ErrorPlataforma" => "Error plataforma",
        "Configuracion"   => "Configuración",
        "Capacitacion"    => "Capacitación",
        var s             => s
    };
}
