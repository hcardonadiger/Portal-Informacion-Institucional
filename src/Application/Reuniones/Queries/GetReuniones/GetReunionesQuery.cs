namespace Diger.TramitesEstado.Application.Reuniones.Queries.GetReuniones;

public sealed record ReunionListItemDto(
    int Id, string Titulo, DateOnly? Fecha, string? Institucion, string? Tipo, int NumAsistentes);

public sealed record GetReunionesQuery : IRequest<IReadOnlyList<ReunionListItemDto>>;

public sealed class GetReunionesQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetReunionesQuery, IReadOnlyList<ReunionListItemDto>>
{
    public async Task<IReadOnlyList<ReunionListItemDto>> Handle(GetReunionesQuery _, CancellationToken ct) =>
        await ctx.Reuniones
            .AsNoTracking()
            .OrderByDescending(r => r.Fecha).ThenByDescending(r => r.CreatedAt)
            .Select(r => new ReunionListItemDto(
                r.Id, r.Titulo, r.Fecha, r.Institucion, r.Tipo, r.Asistentes.Count))
            .ToListAsync(ct);
}
