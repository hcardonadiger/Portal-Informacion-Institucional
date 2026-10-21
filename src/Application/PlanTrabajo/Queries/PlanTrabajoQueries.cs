using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.PlanTrabajo.Common;

namespace Diger.TramitesEstado.Application.PlanTrabajo.Queries;

// ── Buscar expedientes vinculables a una meta ─────────────────────────────
public sealed record ExpedienteBusquedaItem(int Id, string Codigo, int EstadoExp, string? Analista);

public sealed record BuscarExpedientesParaPlanQuery(string InstitucionId, string? Q)
    : IRequest<IReadOnlyList<ExpedienteBusquedaItem>>;

public sealed class BuscarExpedientesParaPlanQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<BuscarExpedientesParaPlanQuery, IReadOnlyList<ExpedienteBusquedaItem>>
{
    public async Task<IReadOnlyList<ExpedienteBusquedaItem>> Handle(
        BuscarExpedientesParaPlanQuery q, CancellationToken ct)
    {
        var query = ctx.Expedientes
            .AsNoTracking()
            .Where(e => e.InstitucionId == q.InstitucionId);

        if (!string.IsNullOrWhiteSpace(q.Q))
            query = query.Where(e => e.Codigo.Contains(q.Q) || e.Analista.Contains(q.Q));

        // EstadoExpediente se guarda como nvarchar (HasConversion<string>), por lo que
        // el cast a int debe hacerse en memoria, no en SQL.
        var filas = await query
            .OrderByDescending(e => e.FechaApertura)
            .ThenBy(e => e.Codigo)
            .Take(15)
            .Select(e => new { e.Id, e.Codigo, e.EstadoExpediente, e.Analista })
            .ToListAsync(ct);

        return filas
            .Select(e => new ExpedienteBusquedaItem(e.Id, e.Codigo, (int)e.EstadoExpediente, e.Analista))
            .ToList();
    }
}

// ── Trámites de un expediente (paso 2 del picker) ─────────────────────────
public sealed record TramiteExpedienteItem(int TramiteIndex, string Nombre, string? AreaResponsable);

public sealed record GetTramitesDeExpedienteQuery(int ExpedienteId)
    : IRequest<IReadOnlyList<TramiteExpedienteItem>>;

public sealed class GetTramitesDeExpedienteQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetTramitesDeExpedienteQuery, IReadOnlyList<TramiteExpedienteItem>>
{
    public async Task<IReadOnlyList<TramiteExpedienteItem>> Handle(
        GetTramitesDeExpedienteQuery q, CancellationToken ct)
    {
        var filas = await ctx.Tramites
            .AsNoTracking()
            .Where(t => t.ExpedienteId == q.ExpedienteId)
            .OrderBy(t => t.TramiteIndex)
            .Select(t => new { t.TramiteIndex, t.NombreCorto, t.NombreTramite, t.AreaResponsable })
            .ToListAsync(ct);

        return filas
            .Select(t => new TramiteExpedienteItem(
                t.TramiteIndex,
                string.IsNullOrWhiteSpace(t.NombreCorto) ? t.NombreTramite : t.NombreCorto,
                t.AreaResponsable))
            .ToList();
    }
}

// ── Lista ─────────────────────────────────────────────────────────────────
public sealed record GetPlanesQuery(string? InstitucionId, int? Anio)
    : IRequest<IReadOnlyList<PlanTrabajoListItemDto>>;

public sealed class GetPlanesQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetPlanesQuery, IReadOnlyList<PlanTrabajoListItemDto>>
{
    public async Task<IReadOnlyList<PlanTrabajoListItemDto>> Handle(GetPlanesQuery q, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        return await ctx.PlanTrabajos
            .AsNoTracking()
            .Where(p => (q.InstitucionId == null || p.InstitucionId == q.InstitucionId)
                     && (q.Anio == null || p.Anio == q.Anio))
            .OrderByDescending(p => p.Anio)
            .ThenBy(p => p.Institucion)
            .Select(p => new PlanTrabajoListItemDto(
                p.Id, p.InstitucionId, p.Institucion, p.Anio, p.Estado,
                p.Metas.Count,
                p.Metas.Count(m => m.Estado == EstadoMeta.Cumplida),
                p.Metas.Count(m =>
                    m.FechaEstimadaFin.HasValue && m.FechaEstimadaFin < hoy
                    && m.Estado != EstadoMeta.Cumplida && m.Estado != EstadoMeta.Cancelada),
                p.CreatedAt))
            .ToListAsync(ct);
    }
}

// ── Detalle ───────────────────────────────────────────────────────────────
public sealed record GetPlanTrabajoByIdQuery(int Id)
    : IRequest<PlanTrabajoDetailDto>;

public sealed class GetPlanTrabajoByIdQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetPlanTrabajoByIdQuery, PlanTrabajoDetailDto>
{
    public async Task<PlanTrabajoDetailDto> Handle(GetPlanTrabajoByIdQuery q, CancellationToken ct)
    {
        var plan = await ctx.PlanTrabajos
            .AsNoTracking()
            .Include(p => p.Metas.OrderBy(m => m.Orden))
            .FirstOrDefaultAsync(p => p.Id == q.Id, ct)
            ?? throw new NotFoundException("PlanTrabajo", q.Id);

        var expIds = plan.Metas
            .Where(m => m.ExpedienteId.HasValue)
            .Select(m => m.ExpedienteId!.Value)
            .ToList();

        Dictionary<int, (string Codigo, EstadoExpediente Estado)> expInfo = expIds.Count > 0
            ? await ctx.Expedientes
                .AsNoTracking()
                .Where(e => expIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => (e.Codigo, e.EstadoExpediente), ct)
            : [];

        // Nombres de los trámites vinculados a nivel de trámite específico
        Dictionary<(int ExpId, int Idx), string> tramiteNombres = expIds.Count > 0
            ? (await ctx.Tramites
                .AsNoTracking()
                .Where(t => expIds.Contains(t.ExpedienteId))
                .Select(t => new { t.ExpedienteId, t.TramiteIndex, t.NombreCorto, t.NombreTramite })
                .ToListAsync(ct))
                .ToDictionary(t => (t.ExpedienteId, t.TramiteIndex),
                              t => string.IsNullOrWhiteSpace(t.NombreCorto) ? t.NombreTramite : t.NombreCorto)
            : [];

        var metas = plan.Metas
            .OrderBy(m => m.Orden)
            .Select(m =>
            {
                string? codigo = null;
                EstadoExpediente? estadoExp = null;
                string? tramiteNombre = null;
                if (m.ExpedienteId.HasValue && expInfo.TryGetValue(m.ExpedienteId.Value, out var inf))
                {
                    codigo    = inf.Codigo;
                    estadoExp = inf.Estado;
                    if (m.ExpedienteTramiteIndex.HasValue)
                        tramiteNombre = tramiteNombres.GetValueOrDefault(
                            (m.ExpedienteId.Value, m.ExpedienteTramiteIndex.Value));
                }
                return new MetaTramiteDto(
                    m.Id, m.Orden, m.NombreTramite,
                    m.FechaEstimadaInicio, m.FechaEstimadaFin, m.FechaRealFin,
                    m.ResponsableId, m.Responsable, m.Estado, m.Observaciones,
                    m.ExpedienteId, codigo, estadoExp,
                    m.ExpedienteTramiteIndex, tramiteNombre);
            })
            .ToList();

        return new PlanTrabajoDetailDto(
            plan.Id, plan.InstitucionId, plan.Institucion, plan.Anio, plan.Estado,
            plan.Observaciones, plan.AprobadoPorId, plan.FechaAprobacion, plan.CreatedAt, metas);
    }
}
