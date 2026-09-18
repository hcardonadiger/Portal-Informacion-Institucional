namespace Diger.TramitesEstado.Application.Proyectos.Prioridades;

/// <summary>
/// Fila del catálogo de prioridades. <paramref name="Proyectos"/> cuenta también los borrados
/// lógicamente: son proyectos que siguen apuntando a esta fila, así que pesan a la hora de
/// decidir si se puede eliminar.
/// </summary>
public sealed record PrioridadProyectoDto(
    int    Id,
    string Nombre,
    int    Orden,
    ColorEtiqueta Color,
    bool   Activo,
    bool   EsPredeterminada,
    int    Proyectos);

// ── Catálogo completo, para la pantalla de administración ──────────────────
public sealed record GetPrioridadesProyectoQuery(bool SoloActivas = false)
    : IRequest<IReadOnlyList<PrioridadProyectoDto>>;

public sealed class GetPrioridadesProyectoQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetPrioridadesProyectoQuery, IReadOnlyList<PrioridadProyectoDto>>
{
    public async Task<IReadOnlyList<PrioridadProyectoDto>> Handle(
        GetPrioridadesProyectoQuery q, CancellationToken ct)
    {
        return await ctx.PrioridadesProyecto
            .AsNoTracking()
            .Where(p => !q.SoloActivas || p.Activo)
            .OrderBy(p => p.Orden).ThenBy(p => p.Nombre)
            .Select(p => new PrioridadProyectoDto(
                p.Id, p.Nombre, p.Orden, p.Color, p.Activo, p.EsPredeterminada,
                ctx.Proyectos.IgnoreQueryFilters().Count(x => x.PrioridadId == p.Id)))
            .ToListAsync(ct);
    }
}

// ── Opciones para desplegables ─────────────────────────────────────────────
/// <summary>
/// Lo que necesita un &lt;select&gt; o una insignia, sin el conteo de uso —que obliga a una
/// subconsulta por fila y no le sirve a nadie al pintar un formulario—.
/// </summary>
public sealed record OpcionPrioridadDto(int Id, string Nombre, ColorEtiqueta Color, bool EsPredeterminada);

/// <param name="IncluirId">
/// Prioridad que debe aparecer aunque esté inactiva: la que ya tiene el proyecto que se está
/// editando. Sin esto, abrir un proyecto viejo cuya prioridad se retiró cambiaría su valor por
/// el primero de la lista con solo guardar.
/// </param>
public sealed record GetOpcionesPrioridadQuery(int? IncluirId = null)
    : IRequest<IReadOnlyList<OpcionPrioridadDto>>;

public sealed class GetOpcionesPrioridadQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetOpcionesPrioridadQuery, IReadOnlyList<OpcionPrioridadDto>>
{
    public async Task<IReadOnlyList<OpcionPrioridadDto>> Handle(
        GetOpcionesPrioridadQuery q, CancellationToken ct)
    {
        return await ctx.PrioridadesProyecto
            .AsNoTracking()
            .Where(p => p.Activo || (q.IncluirId != null && p.Id == q.IncluirId))
            .OrderBy(p => p.Orden).ThenBy(p => p.Nombre)
            .Select(p => new OpcionPrioridadDto(p.Id, p.Nombre, p.Color, p.EsPredeterminada))
            .ToListAsync(ct);
    }
}
