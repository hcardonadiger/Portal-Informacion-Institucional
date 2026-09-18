namespace Diger.TramitesEstado.Application.Proyectos.Categorias;

/// <summary>
/// Fila del catálogo de categorías. <paramref name="Proyectos"/> cuenta también los borrados
/// lógicamente: siguen apuntando a esta fila, así que pesan a la hora de decidir si se puede
/// eliminar.
/// </summary>
public sealed record CategoriaProyectoDto(
    int    Id,
    string Nombre,
    int    Orden,
    ColorEtiqueta Color,
    bool   Activo,
    int    Proyectos);

// ── Catálogo completo, para la pantalla de administración ──────────────────
public sealed record GetCategoriasProyectoQuery(bool SoloActivas = false)
    : IRequest<IReadOnlyList<CategoriaProyectoDto>>;

public sealed class GetCategoriasProyectoQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetCategoriasProyectoQuery, IReadOnlyList<CategoriaProyectoDto>>
{
    public async Task<IReadOnlyList<CategoriaProyectoDto>> Handle(
        GetCategoriasProyectoQuery q, CancellationToken ct)
    {
        return await ctx.CategoriasProyecto
            .AsNoTracking()
            .Where(c => !q.SoloActivas || c.Activo)
            .OrderBy(c => c.Orden).ThenBy(c => c.Nombre)
            .Select(c => new CategoriaProyectoDto(
                c.Id, c.Nombre, c.Orden, c.Color, c.Activo,
                ctx.Proyectos.IgnoreQueryFilters().Count(p => p.CategoriaId == c.Id)))
            .ToListAsync(ct);
    }
}

// ── Opciones para desplegables ─────────────────────────────────────────────
public sealed record OpcionCategoriaDto(int Id, string Nombre, ColorEtiqueta Color);

/// <param name="IncluirId">
/// Categoría que debe aparecer aunque esté inactiva: la que ya tiene el proyecto que se está
/// editando. Sin esto, abrir un proyecto viejo cuya categoría se retiró lo dejaría, al guardar,
/// como si nunca hubiera tenido ninguna.
/// </param>
public sealed record GetOpcionesCategoriaQuery(int? IncluirId = null)
    : IRequest<IReadOnlyList<OpcionCategoriaDto>>;

public sealed class GetOpcionesCategoriaQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetOpcionesCategoriaQuery, IReadOnlyList<OpcionCategoriaDto>>
{
    public async Task<IReadOnlyList<OpcionCategoriaDto>> Handle(
        GetOpcionesCategoriaQuery q, CancellationToken ct)
    {
        return await ctx.CategoriasProyecto
            .AsNoTracking()
            .Where(c => c.Activo || (q.IncluirId != null && c.Id == q.IncluirId))
            .OrderBy(c => c.Orden).ThenBy(c => c.Nombre)
            .Select(c => new OpcionCategoriaDto(c.Id, c.Nombre, c.Color))
            .ToListAsync(ct);
    }
}
