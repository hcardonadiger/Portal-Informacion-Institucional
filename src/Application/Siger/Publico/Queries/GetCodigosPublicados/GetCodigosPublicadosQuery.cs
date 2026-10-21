namespace Diger.TramitesEstado.Application.Siger.Publico.Queries.GetCodigosPublicados;

/// <summary>Todos los códigos vivos — GET /api/v1/codigos-publicados. El consumidor retira de
/// su catálogo local cualquier código que ya no aparezca aquí (M-09: cubre por igual la ficha
/// despublicada y la borrada, sin necesitar una columna de bitácora de bajas).</summary>
public sealed record GetCodigosPublicadosQuery : IRequest<IReadOnlyList<string>>;

public sealed class GetCodigosPublicadosQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetCodigosPublicadosQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(GetCodigosPublicadosQuery q, CancellationToken ct) =>
        await ctx.TramitesSiger.AsNoTracking()
            .Where(t => t.Publicado)
            .Select(t => t.Codigo)
            .ToListAsync(ct);
}
