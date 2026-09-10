namespace Diger.TramitesEstado.Application.Siger.Publico.Queries.GetCategoriasPublicas;

/// <summary>Las categorías del catálogo, con conteo de trámites publicados — GET /api/v1/categorias.</summary>
public sealed record GetCategoriasPublicasQuery : IRequest<IReadOnlyList<CategoriaPublicaDto>>;

public sealed class GetCategoriasPublicasQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetCategoriasPublicasQuery, IReadOnlyList<CategoriaPublicaDto>>
{
    public async Task<IReadOnlyList<CategoriaPublicaDto>> Handle(GetCategoriasPublicasQuery q, CancellationToken ct)
    {
        var conteos = await ctx.TramitesSiger.AsNoTracking()
            .Where(t => t.Publicado && t.CategoriaId != null)
            .GroupBy(t => t.CategoriaId!.Value)
            .Select(g => new { CategoriaId = g.Key, Conteo = g.Count() })
            .ToDictionaryAsync(x => x.CategoriaId, x => x.Conteo, ct);

        var categorias = await ctx.CategoriasTramite.AsNoTracking()
            .Where(c => c.Activo)
            .OrderBy(c => c.Orden)
            .ToListAsync(ct);

        return categorias.Select(c => new CategoriaPublicaDto(
            c.Id, c.Nombre, c.Icono, c.Orden, conteos.TryGetValue(c.Id, out var n) ? n : 0))
            .ToList();
    }
}
