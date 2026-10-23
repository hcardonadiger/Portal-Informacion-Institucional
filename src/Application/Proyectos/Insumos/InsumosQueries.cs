namespace Diger.TramitesEstado.Application.Proyectos.Insumos;

// ── Categorías, para su pantalla de administración ─────────────────────────
/// <param name="Insumos">Cuántos insumos tiene, activos o no.</param>
public sealed record CategoriaInsumoDto(int Id, string Nombre, int Orden, bool Activo, int Insumos);

public sealed record GetCategoriasInsumoQuery : IRequest<IReadOnlyList<CategoriaInsumoDto>>;

public sealed class GetCategoriasInsumoQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetCategoriasInsumoQuery, IReadOnlyList<CategoriaInsumoDto>>
{
    public async Task<IReadOnlyList<CategoriaInsumoDto>> Handle(GetCategoriasInsumoQuery q, CancellationToken ct) =>
        await ctx.CategoriasInsumo.AsNoTracking()
            .OrderBy(c => c.Orden).ThenBy(c => c.Nombre)
            .Select(c => new CategoriaInsumoDto(c.Id, c.Nombre, c.Orden, c.Activo,
                ctx.Insumos.Count(i => i.CategoriaId == c.Id)))
            .ToListAsync(ct);
}

// ── Insumos, para su pantalla de administración ────────────────────────────
/// <param name="Actividades">En cuántas actividades está asignado. Es lo que el administrador
/// necesita saber antes de desactivarlo: esas actividades lo conservan.</param>
public sealed record InsumoDto(
    int     Id,
    int     CategoriaId,
    string  Categoria,
    bool    CategoriaActiva,
    string  Nombre,
    string? Descripcion,
    bool    Activo,
    int     Actividades);

/// <param name="Texto">Busca en el nombre y la descripción.</param>
/// <param name="CategoriaId">Solo los de esa categoría.</param>
public sealed record GetInsumosQuery(string? Texto = null, int? CategoriaId = null)
    : IRequest<IReadOnlyList<InsumoDto>>;

public sealed class GetInsumosQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetInsumosQuery, IReadOnlyList<InsumoDto>>
{
    public async Task<IReadOnlyList<InsumoDto>> Handle(GetInsumosQuery q, CancellationToken ct)
    {
        var texto = string.IsNullOrWhiteSpace(q.Texto) ? null : q.Texto.Trim();

        var filas = await ctx.Insumos.AsNoTracking()
            .Where(i => q.CategoriaId == null || i.CategoriaId == q.CategoriaId)
            .Where(i => texto == null || i.Nombre.Contains(texto)
                                      || (i.Descripcion != null && i.Descripcion.Contains(texto)))
            .OrderBy(i => i.Categoria!.Orden).ThenBy(i => i.Categoria!.Nombre)
            .ThenByDescending(i => i.Activo).ThenBy(i => i.Nombre)
            .Select(i => new { i.Id, i.CategoriaId, Categoria = i.Categoria!.Nombre, CategoriaActiva = i.Categoria.Activo,
                               i.Nombre, i.Descripcion, i.Activo })
            .ToListAsync(ct);

        // El conteo cruza todos los proyectos: es un número para decidir si desactivar, no un dato
        // de ningún proyecto, y quien lo ve es el administrador.
        var ids  = filas.Select(f => f.Id).ToList();
        var usos = ids.Count == 0
            ? new Dictionary<int, int>()
            : await ctx.ProyectoActividades.AsNoTracking()
                .SelectMany(a => a.Insumos)
                .Where(x => ids.Contains(x.InsumoId))
                .GroupBy(x => x.InsumoId)
                .Select(g => new { g.Key, Cuantas = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Cuantas, ct);

        return filas.Select(f => new InsumoDto(f.Id, f.CategoriaId, f.Categoria, f.CategoriaActiva,
                                               f.Nombre, f.Descripcion, f.Activo,
                                               usos.GetValueOrDefault(f.Id)))
                    .ToList();
    }
}

// ── Lo que se ofrece en la ventana de la actividad ─────────────────────────
public sealed record InsumoElegibleDto(int Id, string Nombre, string? Descripcion, int CategoriaId, string Categoria);

/// <summary>
/// Los insumos que se pueden elegir hoy: activos y en una categoría activa, en el orden del
/// catálogo. Es la misma regla con la que el guardado acepta un insumo nuevo.
/// </summary>
public sealed record GetInsumosElegiblesQuery : IRequest<IReadOnlyList<InsumoElegibleDto>>;

public sealed class GetInsumosElegiblesQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetInsumosElegiblesQuery, IReadOnlyList<InsumoElegibleDto>>
{
    public async Task<IReadOnlyList<InsumoElegibleDto>> Handle(GetInsumosElegiblesQuery q, CancellationToken ct) =>
        await ctx.Insumos.AsNoTracking()
            .Where(i => i.Activo && i.Categoria!.Activo)
            .OrderBy(i => i.Categoria!.Orden).ThenBy(i => i.Categoria!.Nombre).ThenBy(i => i.Nombre)
            .Select(i => new InsumoElegibleDto(i.Id, i.Nombre, i.Descripcion, i.CategoriaId, i.Categoria!.Nombre))
            .ToListAsync(ct);
}
