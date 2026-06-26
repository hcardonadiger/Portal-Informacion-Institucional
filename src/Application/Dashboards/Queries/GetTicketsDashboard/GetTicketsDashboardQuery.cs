using Diger.TramitesEstado.Application.Dashboards.Common;

namespace Diger.TramitesEstado.Application.Dashboards.Queries.GetTicketsDashboard;

public sealed record GetTicketsDashboardQuery : IRequest<TicketsDashboardDto>;

public sealed class GetTicketsDashboardQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetTicketsDashboardQuery, TicketsDashboardDto>
{
    public async Task<TicketsDashboardDto> Handle(GetTicketsDashboardQuery _, CancellationToken ct)
    {
        var t = ctx.Tickets;
        var total = await t.CountAsync(ct);
        var abiertos = await t.CountAsync(x => x.Estado == EstadoTicket.Abierto || x.Estado == EstadoTicket.EnProgreso, ct);
        var criticos = await t.CountAsync(x => x.Prioridad == PrioridadTicket.Critica &&
            (x.Estado == EstadoTicket.Abierto || x.Estado == EstadoTicket.EnProgreso), ct);

        var porEstado = await GrupoAsync(t.GroupBy(x => x.Estado), ct);
        var porPrioridad = await GrupoAsync(t.GroupBy(x => x.Prioridad), ct);
        var porCategoria = await GrupoAsync(t.GroupBy(x => x.Categoria), ct);

        var porInstitucion = (await t
            .GroupBy(x => x.Institucion)
            .Select(g => new { Inst = g.Key, C = g.Count() })
            .ToListAsync(ct))
            .OrderByDescending(x => x.C)
            .Select(x => new ConteoDto(string.IsNullOrWhiteSpace(x.Inst) ? "Sin institución" : x.Inst!, x.C))
            .ToList();

        var ahora = DateTime.UtcNow;
        var antiguos = (await t
            .Where(x => x.Estado == EstadoTicket.Abierto || x.Estado == EstadoTicket.EnProgreso)
            .OrderBy(x => x.CreatedAt)
            .Take(8)
            .Select(x => new { x.Id, x.Numero, x.Titulo, x.Institucion, x.CreatedAt, x.Prioridad })
            .ToListAsync(ct))
            .Select(x => new TicketAntiguedadDto(
                x.Id, x.Numero, x.Titulo, x.Institucion,
                Math.Max(0, (int)(ahora - x.CreatedAt).TotalDays), x.Prioridad))
            .ToList();

        return new TicketsDashboardDto(total, abiertos, criticos,
            porEstado, porPrioridad, porCategoria, porInstitucion, antiguos);
    }

    // Agrupa por un enum, devuelve TODOS los valores del enum (con 0 si no hay datos), en orden.
    private static async Task<IReadOnlyList<ConteoDto>> GrupoAsync<TEnum>(
        IQueryable<IGrouping<TEnum, Domain.Entities.Ticket>> grouped, CancellationToken ct) where TEnum : struct, Enum
    {
        var datos = (await grouped.Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.C);
        return Enum.GetValues<TEnum>()
            .Select(e => new ConteoDto(Etiquetas.De(e), datos.TryGetValue(e, out var c) ? c : 0))
            .ToList();
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
