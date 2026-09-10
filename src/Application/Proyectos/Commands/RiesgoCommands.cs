using Diger.TramitesEstado.Application.Proyectos.Common;

namespace Diger.TramitesEstado.Application.Proyectos.Commands;

// ── Registrar riesgo ──────────────────────────────────────────────────────
public sealed record RegistrarRiesgoCommand(
    int              ProyectoId,
    string           Descripcion,
    CategoriaRiesgo  Categoria,
    NivelCualitativo Probabilidad,
    NivelCualitativo Impacto,
    EstrategiaRiesgo Estrategia,
    string?          Mitigacion    = null,
    Guid?            ResponsableId = null,
    string?          Responsable   = null,
    DateOnly?        FechaRevision = null) : IRequest<int>;

public sealed class RegistrarRiesgoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<RegistrarRiesgoCommand, int>
{
    public async Task<int> Handle(RegistrarRiesgoCommand cmd, CancellationToken ct)
    {
        var proyecto = await ctx.Proyectos.FirstOrDefaultAsync(p => p.Id == cmd.ProyectoId, ct)
            ?? throw new NotFoundException(nameof(Proyecto), cmd.ProyectoId);

        if (proyecto.Estado is EstadoProyecto.Cerrado or EstadoProyecto.Cancelado)
            throw new DomainException($"El proyecto está «{proyecto.Estado}» y ya no admite riesgos nuevos.");

        var actor  = currentUser.Nombre ?? "—";
        var riesgo = RiesgoProyecto.Crear(
            cmd.ProyectoId, cmd.Descripcion, cmd.Categoria, cmd.Probabilidad, cmd.Impacto,
            cmd.Estrategia, actor, cmd.Mitigacion, cmd.ResponsableId, cmd.Responsable, cmd.FechaRevision);

        ctx.ProyectoRiesgos.Add(riesgo);

        ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
            cmd.ProyectoId, TipoEventoProyecto.Riesgo,
            $"Riesgo registrado ({riesgo.Categoria}, severidad {riesgo.Severidad}): {riesgo.Descripcion}", actor));

        await ctx.SaveChangesAsync(ct);
        return riesgo.Id;
    }
}

// ── Actualizar riesgo ─────────────────────────────────────────────────────
public sealed record ActualizarRiesgoCommand(
    int              RiesgoId,
    string           Descripcion,
    CategoriaRiesgo  Categoria,
    NivelCualitativo Probabilidad,
    NivelCualitativo Impacto,
    EstrategiaRiesgo Estrategia,
    string?          Mitigacion,
    Guid?            ResponsableId,
    string?          Responsable,
    DateOnly?        FechaRevision) : IRequest<Unit>;

public sealed class ActualizarRiesgoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<ActualizarRiesgoCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarRiesgoCommand cmd, CancellationToken ct)
    {
        var riesgo = await ctx.ProyectoRiesgos.FirstOrDefaultAsync(r => r.Id == cmd.RiesgoId, ct)
            ?? throw new NotFoundException(nameof(RiesgoProyecto), cmd.RiesgoId);

        var severidadAntes = riesgo.Severidad;

        riesgo.Actualizar(cmd.Descripcion, cmd.Categoria, cmd.Probabilidad, cmd.Impacto,
                          cmd.Estrategia, cmd.Mitigacion, cmd.ResponsableId, cmd.Responsable,
                          cmd.FechaRevision);

        // Solo se audita si la severidad se movió: es el dato que cambia decisiones.
        if (riesgo.Severidad != severidadAntes)
            ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
                riesgo.ProyectoId, TipoEventoProyecto.Riesgo,
                $"Severidad del riesgo «{riesgo.Descripcion}»: {severidadAntes} → {riesgo.Severidad}.",
                currentUser.Nombre ?? "—"));

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Cambiar el estado de un riesgo ────────────────────────────────────────
public sealed record CambiarEstadoRiesgoCommand(int RiesgoId, EstadoRiesgo Nuevo) : IRequest<Unit>;

public sealed class CambiarEstadoRiesgoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<CambiarEstadoRiesgoCommand, Unit>
{
    public async Task<Unit> Handle(CambiarEstadoRiesgoCommand cmd, CancellationToken ct)
    {
        var riesgo = await ctx.ProyectoRiesgos.FirstOrDefaultAsync(r => r.Id == cmd.RiesgoId, ct)
            ?? throw new NotFoundException(nameof(RiesgoProyecto), cmd.RiesgoId);

        var anterior = riesgo.Estado;
        riesgo.CambiarEstado(cmd.Nuevo);
        if (anterior == riesgo.Estado) return Unit.Value;

        ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
            riesgo.ProyectoId, TipoEventoProyecto.Riesgo,
            $"Riesgo «{riesgo.Descripcion}»: {anterior} → {riesgo.Estado}.", currentUser.Nombre ?? "—"));

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Eliminar riesgo ───────────────────────────────────────────────────────
/// <summary>
/// Borrado real, no lógico: un riesgo mal cargado es ruido, no historia. Lo que sí queda es la
/// entrada de auditoría, que es la que sostiene el rastro.
/// </summary>
public sealed record EliminarRiesgoCommand(int RiesgoId) : IRequest<Unit>;

public sealed class EliminarRiesgoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<EliminarRiesgoCommand, Unit>
{
    public async Task<Unit> Handle(EliminarRiesgoCommand cmd, CancellationToken ct)
    {
        var riesgo = await ctx.ProyectoRiesgos.FirstOrDefaultAsync(r => r.Id == cmd.RiesgoId, ct)
            ?? throw new NotFoundException(nameof(RiesgoProyecto), cmd.RiesgoId);

        // Los avances que señalaban este riesgo se desvinculan a mano antes de borrarlo.
        // La FK está en NoAction —un SetNull en la base abriría una segunda ruta de cascada hacia
        // ProyectoAvances y SQL Server la rechaza—, así que sin esto el borrado falla por la
        // restricción. La entrada de bitácora del avance no se toca: el bloqueo ocurrió igual, lo
        // único que se pierde es la referencia al riesgo que lo anticipaba.
        var vinculados = await ctx.ProyectoAvances
            .Where(a => a.RiesgoId == cmd.RiesgoId)
            .ToListAsync(ct);

        foreach (var a in vinculados) a.DesvincularRiesgo();

        ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
            riesgo.ProyectoId, TipoEventoProyecto.Riesgo,
            $"Riesgo eliminado: «{riesgo.Descripcion}»." +
            (vinculados.Count > 0
                ? $" {vinculados.Count} reporte(s) de avance quedan sin el vínculo."
                : ""),
            currentUser.Nombre ?? "—"));

        ctx.ProyectoRiesgos.Remove(riesgo);
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
