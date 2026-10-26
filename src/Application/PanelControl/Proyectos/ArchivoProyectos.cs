namespace Diger.TramitesEstado.Application.PanelControl.Proyectos;

// ── Lectura ───────────────────────────────────────────────────────────────
public sealed record ProyectoAdministracionDto(
    int Id, string Codigo, string Nombre, EstadoProyecto Estado, string? Responsable,
    string? InstitucionId, bool Archivado, DateTime? ModificadoEl, string? ModificadoPor);

/// <summary>
/// Los proyectos tal como los ve la administración: todos, de cualquier institución, y
/// —con <paramref name="Archivados"/>— también los que salieron del portafolio.
///
/// <para>Ignora los filtros globales a propósito, como el catálogo de instituciones: es una vista
/// de Panel de control con su propia clave, y sin eso un proyecto archivado no se podría ver ni
/// devolver al portafolio desde ningún lado.</para>
/// </summary>
public sealed record GetProyectosAdministracionQuery(bool Archivados, string? Buscar = null)
    : IRequest<IReadOnlyList<ProyectoAdministracionDto>>;

public sealed class GetProyectosAdministracionQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetProyectosAdministracionQuery, IReadOnlyList<ProyectoAdministracionDto>>
{
    public async Task<IReadOnlyList<ProyectoAdministracionDto>> Handle(
        GetProyectosAdministracionQuery q, CancellationToken ct)
    {
        var consulta = ctx.Proyectos.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.IsDeleted == q.Archivados);

        if (!string.IsNullOrWhiteSpace(q.Buscar))
        {
            var t = q.Buscar.Trim();
            consulta = consulta.Where(p => p.Nombre.Contains(t) || p.Codigo.Contains(t));
        }

        return await consulta
            .OrderByDescending(p => q.Archivados ? p.UpdatedAt : p.CreatedAt)
            .ThenBy(p => p.Codigo)
            .Select(p => new ProyectoAdministracionDto(
                p.Id, p.Codigo, p.Nombre, p.Estado, p.Responsable, p.InstitucionId,
                p.IsDeleted, p.UpdatedAt, p.UpdatedBy))
            .ToListAsync(ct);
    }
}

// ── Archivar y restaurar ──────────────────────────────────────────────────
/// <summary>
/// Saca un proyecto del portafolio sin borrar nada: queda oculto para todos, con su estructura,
/// bitácora, documentos y auditoría intactos, y se puede restaurar desde la misma pantalla.
///
/// <para>Es el mismo borrado lógico que hace «Eliminar proyecto» en la ficha, pero alcanza a
/// cualquier proyecto —también los que quedan fuera del alcance de quien administra— y deja en la
/// bitácora quién lo archivó y por qué.</para>
/// </summary>
public sealed record ArchivarProyectoCommand(int Id, string? Motivo) : IRequest<Unit>;

public sealed class ArchivarProyectoCommandHandler(IApplicationDbContext ctx, ICurrentUserService currentUser)
    : IRequestHandler<ArchivarProyectoCommand, Unit>
{
    public async Task<Unit> Handle(ArchivarProyectoCommand cmd, CancellationToken ct)
    {
        var proyecto = await ctx.Proyectos.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Proyecto), cmd.Id);

        if (proyecto.IsDeleted) throw new DomainException("El proyecto ya está archivado.");

        proyecto.IsDeleted = true;

        var motivo = string.IsNullOrWhiteSpace(cmd.Motivo) ? "" : $": {cmd.Motivo.Trim()}";
        ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
            proyecto.Id, TipoEventoProyecto.CambioEstado,
            $"Proyecto archivado desde el Panel de control{motivo}", currentUser.Nombre ?? "—"));

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

/// <summary>Devuelve al portafolio un proyecto archivado, en el estado que tenía al archivarse.
/// El código no choca: los códigos de proyecto no se reutilizan, aunque el dueño esté archivado.</summary>
public sealed record RestaurarProyectoCommand(int Id) : IRequest<Unit>;

public sealed class RestaurarProyectoCommandHandler(IApplicationDbContext ctx, ICurrentUserService currentUser)
    : IRequestHandler<RestaurarProyectoCommand, Unit>
{
    public async Task<Unit> Handle(RestaurarProyectoCommand cmd, CancellationToken ct)
    {
        var proyecto = await ctx.Proyectos.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Proyecto), cmd.Id);

        if (!proyecto.IsDeleted) throw new DomainException("El proyecto no está archivado.");

        proyecto.IsDeleted = false;

        ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
            proyecto.Id, TipoEventoProyecto.CambioEstado,
            "Proyecto restaurado al portafolio desde el Panel de control", currentUser.Nombre ?? "—"));

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
