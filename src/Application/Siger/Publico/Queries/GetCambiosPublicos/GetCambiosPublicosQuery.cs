namespace Diger.TramitesEstado.Application.Siger.Publico.Queries.GetCambiosPublicos;

/// <summary>Códigos a refrescar desde una fecha — GET /api/v1/cambios?desde=.
/// No distingue alta de cambio a propósito (M-09): el consumidor hace upsert, que es
/// idempotente. Las bajas se resuelven contra /api/v1/codigos-publicados, no aquí.</summary>
public sealed record GetCambiosPublicosQuery(DateTime Desde) : IRequest<CambiosPublicosDto>;

public sealed class GetCambiosPublicosQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetCambiosPublicosQuery, CambiosPublicosDto>
{
    public async Task<CambiosPublicosDto> Handle(GetCambiosPublicosQuery q, CancellationToken ct)
    {
        var generadoEl = DateTime.UtcNow;

        var codigos = await ctx.TramitesSiger.AsNoTracking()
            .Where(t => t.Publicado && (t.UpdatedAt ?? t.CreatedAt) >= q.Desde)
            .Select(t => t.Codigo)
            .ToListAsync(ct);

        return new CambiosPublicosDto(codigos, generadoEl);
    }
}
