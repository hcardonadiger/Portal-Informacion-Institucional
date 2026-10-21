using Diger.TramitesEstado.Application.Dashboards.Common;

namespace Diger.TramitesEstado.Application.Dashboards.Queries.GetMisProyectosDashboard;

public sealed record MisProyectosItemDto(
    int ProyectoId, string Codigo, string Nombre, string? UnidadId, string? UnidadNombre,
    EstadoProyecto Estado, int AvancePct, DateOnly? FechaFinPlan, bool Atrasado, bool SinReportar);

public sealed record MisProyectosDashboardDto(
    int TotalProyectos, int AvancePromedio, int Atrasados, int SinReportar30,
    IReadOnlyList<MisProyectosItemDto> Proyectos);

/// <summary>Proyectos donde la persona que consulta es interesado o responsable — la vista
/// «Unidad» tal cual. Un jefe de área suele ver aquí casi todo su portafolio porque la
/// sincronización automática (ver IInteresadosAutomaticosSync) ya lo dejó como interesado de cada
/// proyecto de su área.
///
/// <para>Desde el 2026-09-07 la vista «Área» ya no se apoya en esta consulta: pasó a
/// <c>GetProyectosDashboardQuery</c>, que le da el mismo detalle que al nivel institución y le
/// permite conmutar entre «toda mi área» y «solo lo mío». Ese «solo lo mío» usa el mismo predicado
/// de acá —responsable o interesado— para no estrenar una segunda definición de «mío».</para></summary>
public sealed record GetMisProyectosDashboardQuery : IRequest<MisProyectosDashboardDto>;

public sealed class GetMisProyectosDashboardQueryHandler(IApplicationDbContext ctx, ICurrentUserService currentUser)
    : IRequestHandler<GetMisProyectosDashboardQuery, MisProyectosDashboardDto>
{
    public async Task<MisProyectosDashboardDto> Handle(GetMisProyectosDashboardQuery q, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        if (userId is null) return new MisProyectosDashboardDto(0, 0, 0, 0, []);

        var proyectos = await ctx.Proyectos.AsNoTracking()
            .Where(p => p.ResponsableId == userId
                     || ctx.ProyectoInteresados.Any(i => i.ProyectoId == p.Id && i.UsuarioId == userId))
            .Select(p => new
            {
                p.Id, p.Codigo, p.Nombre, p.UnidadId, p.Estado, p.AvancePct, p.FechaFinPlan,
                UltimoAvance = ctx.ProyectoAvances
                    .Where(a => a.ProyectoId == p.Id)
                    .OrderByDescending(a => a.Fecha)
                    .Select(a => (DateTime?)a.Fecha)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var unidadIds = proyectos.Where(p => p.UnidadId != null).Select(p => p.UnidadId!).Distinct().ToList();
        var nombresUnidad = await ctx.Unidades.AsNoTracking()
            .Where(u => unidadIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Nombre, ct);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var corte = ProyectoEstadoReglas.CorteSinReporte(DateTime.UtcNow);

        var items = proyectos.Select(p =>
        {
            var atrasado    = ProyectoEstadoReglas.Atrasado(p.FechaFinPlan, p.Estado, hoy);
            var sinReportar = ProyectoEstadoReglas.SinReportar(p.Estado, p.UltimoAvance, corte);
            return new MisProyectosItemDto(
                p.Id, p.Codigo, p.Nombre, p.UnidadId,
                p.UnidadId != null && nombresUnidad.TryGetValue(p.UnidadId, out var n) ? n : null,
                p.Estado, p.AvancePct, p.FechaFinPlan, atrasado, sinReportar);
        }).OrderByDescending(i => i.Atrasado).ThenBy(i => i.FechaFinPlan).ToList();

        var enEjecucion = items.Where(i => i.Estado == EstadoProyecto.EnEjecucion).ToList();

        return new MisProyectosDashboardDto(
            items.Count,
            ProyectoEstadoReglas.AvancePromedio(enEjecucion.Select(i => i.AvancePct)),
            items.Count(i => i.Atrasado),
            items.Count(i => i.SinReportar),
            items);
    }
}
