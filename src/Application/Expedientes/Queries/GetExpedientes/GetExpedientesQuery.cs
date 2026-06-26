namespace Diger.TramitesEstado.Application.Expedientes.Queries.GetExpedientes;

public sealed record ExpedienteListItemDto(
    int                Id,
    string             Codigo,
    string             Institucion,
    string             Analista,
    int                NumTramites,
    EstadoExpediente   Estado,
    DateTime           FechaCreacion,
    IReadOnlyList<string> TramiteNombres);

public sealed record GetExpedientesQuery : IRequest<IReadOnlyList<ExpedienteListItemDto>>;

public sealed class GetExpedientesQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetExpedientesQuery, IReadOnlyList<ExpedienteListItemDto>>
{
    public async Task<IReadOnlyList<ExpedienteListItemDto>> Handle(
        GetExpedientesQuery _, CancellationToken ct) =>
        await ctx.Expedientes
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new ExpedienteListItemDto(
                e.Id,
                e.Codigo,
                e.Institucion,
                e.Analista,
                e.Tramites.Count,
                e.EstadoExpediente,
                e.CreatedAt,
                e.Tramites.OrderBy(t => t.TramiteIndex).Select(t => t.NombreTramite).ToList()))
            .ToListAsync(ct);
}
