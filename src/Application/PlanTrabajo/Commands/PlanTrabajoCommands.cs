using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.PlanTrabajo.Commands;

// ── Crear plan ─────────────────────────────────────────────────────────────
public sealed record CrearPlanTrabajoCommand(
    string  InstitucionId,
    string  Institucion,
    int     Anio,
    string? Observaciones
) : IRequest<int>;

public sealed class CrearPlanTrabajoCommandHandler(IApplicationDbContext ctx, IUnitOfWork uow)
    : IRequestHandler<CrearPlanTrabajoCommand, int>
{
    public async Task<int> Handle(CrearPlanTrabajoCommand cmd, CancellationToken ct)
    {
        var existe = await ctx.PlanTrabajos
            .AnyAsync(p => p.InstitucionId == cmd.InstitucionId && p.Anio == cmd.Anio, ct);

        if (existe)
            throw new DomainException($"Ya existe un plan de trabajo {cmd.Anio} para esta institución.");

        var plan = Diger.TramitesEstado.Domain.Entities.PlanTrabajo.Crear(cmd.InstitucionId, cmd.Institucion, cmd.Anio);
        plan.Observaciones = cmd.Observaciones;

        ctx.PlanTrabajos.Add(plan);
        await uow.SaveChangesAsync(ct);
        return plan.Id;
    }
}

// ── Actualizar encabezado del plan ─────────────────────────────────────────
public sealed record ActualizarPlanCommand(
    int     Id,
    string? Observaciones
) : IRequest;

public sealed class ActualizarPlanCommandHandler(IApplicationDbContext ctx, IUnitOfWork uow)
    : IRequestHandler<ActualizarPlanCommand>
{
    public async Task Handle(ActualizarPlanCommand cmd, CancellationToken ct)
    {
        var plan = await ctx.PlanTrabajos.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new NotFoundException("PlanTrabajo", cmd.Id);

        plan.Observaciones = cmd.Observaciones;
        await uow.SaveChangesAsync(ct);
    }
}

// ── Cambiar estado del plan ────────────────────────────────────────────────
public sealed record CambiarEstadoPlanCommand(int Id, EstadoPlanTrabajo NuevoEstado) : IRequest;

public sealed class CambiarEstadoPlanCommandHandler(IApplicationDbContext ctx, IUnitOfWork uow)
    : IRequestHandler<CambiarEstadoPlanCommand>
{
    public async Task Handle(CambiarEstadoPlanCommand cmd, CancellationToken ct)
    {
        var plan = await ctx.PlanTrabajos.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new NotFoundException("PlanTrabajo", cmd.Id);

        if (plan.Estado == EstadoPlanTrabajo.Cerrado && cmd.NuevoEstado != EstadoPlanTrabajo.Activo)
            throw new DomainException("Un plan cerrado no puede modificarse.");

        plan.Estado = cmd.NuevoEstado;
        if (cmd.NuevoEstado == EstadoPlanTrabajo.Activo)
            plan.FechaAprobacion = DateOnly.FromDateTime(DateTime.UtcNow);

        await uow.SaveChangesAsync(ct);
    }
}

// ── Guardar meta (crear o actualizar) ─────────────────────────────────────
public sealed record GuardarMetaCommand(
    int        PlanTrabajoId,
    int?       MetaId,
    string     NombreTramite,
    DateOnly?  FechaEstimadaInicio,
    DateOnly?  FechaEstimadaFin,
    DateOnly?  FechaRealFin,
    Guid?      ResponsableId,
    EstadoMeta Estado,
    string?    Observaciones,
    int?       ExpedienteId,
    int?       ExpedienteTramiteIndex
) : IRequest<int>;

public sealed class GuardarMetaCommandHandler(IApplicationDbContext ctx, IUnitOfWork uow)
    : IRequestHandler<GuardarMetaCommand, int>
{
    public async Task<int> Handle(GuardarMetaCommand cmd, CancellationToken ct)
    {
        MetaTramite meta;

        if (cmd.MetaId.HasValue)
        {
            meta = await ctx.MetasTrabajo
                .FirstOrDefaultAsync(m => m.Id == cmd.MetaId && m.PlanTrabajoId == cmd.PlanTrabajoId, ct)
                ?? throw new NotFoundException(nameof(MetaTramite), cmd.MetaId);
        }
        else
        {
            var maxOrden = await ctx.MetasTrabajo
                .Where(m => m.PlanTrabajoId == cmd.PlanTrabajoId)
                .MaxAsync(m => (int?)m.Orden, ct) ?? 0;

            meta = new MetaTramite
            {
                PlanTrabajoId = cmd.PlanTrabajoId,
                Orden         = maxOrden + 1
            };
            ctx.MetasTrabajo.Add(meta);
        }

        meta.NombreTramite        = cmd.NombreTramite.Trim();
        meta.FechaEstimadaInicio  = cmd.FechaEstimadaInicio;
        meta.FechaEstimadaFin     = cmd.FechaEstimadaFin;
        meta.FechaRealFin         = cmd.FechaRealFin;

        if (cmd.ResponsableId.HasValue)
        {
            var nombreResp = await ctx.Usuarios
                .Where(u => u.Id == cmd.ResponsableId.Value)
                .Select(u => u.Nombre)
                .FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException(nameof(Usuario), cmd.ResponsableId.Value);
            meta.ResponsableId = cmd.ResponsableId;
            meta.Responsable   = nombreResp;
        }
        else
        {
            meta.ResponsableId = null;
            meta.Responsable   = null;
        }

        meta.Estado               = cmd.Estado;
        meta.Observaciones        = cmd.Observaciones?.Trim();
        meta.ExpedienteId         = cmd.ExpedienteId;
        // El índice de trámite solo tiene sentido con un expediente vinculado
        meta.ExpedienteTramiteIndex = cmd.ExpedienteId.HasValue ? cmd.ExpedienteTramiteIndex : null;

        await uow.SaveChangesAsync(ct);
        return meta.Id;
    }
}

// ── Eliminar meta ──────────────────────────────────────────────────────────
public sealed record EliminarMetaCommand(int MetaId, int PlanTrabajoId) : IRequest;

public sealed class EliminarMetaCommandHandler(IApplicationDbContext ctx, IUnitOfWork uow)
    : IRequestHandler<EliminarMetaCommand>
{
    public async Task Handle(EliminarMetaCommand cmd, CancellationToken ct)
    {
        var meta = await ctx.MetasTrabajo
            .FirstOrDefaultAsync(m => m.Id == cmd.MetaId && m.PlanTrabajoId == cmd.PlanTrabajoId, ct)
            ?? throw new NotFoundException(nameof(MetaTramite), cmd.MetaId);

        ctx.MetasTrabajo.Remove(meta);
        await uow.SaveChangesAsync(ct);
    }
}
