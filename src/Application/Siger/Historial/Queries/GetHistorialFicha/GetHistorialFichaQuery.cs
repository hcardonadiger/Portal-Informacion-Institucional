namespace Diger.TramitesEstado.Application.Siger.Historial.Queries.GetHistorialFicha;

/// <summary>Una versión archivada de la ficha, sin su contenido: la lista no lo necesita.</summary>
/// <param name="EsOriginal">Cierto para la versión 0, el inventario tal como llegó de SIGER.</param>
public sealed record VersionFichaDto(
    int      Version,
    string   Origen,
    DateTime CapturadaEl,
    string   Codigo,
    bool     EsOriginal);

/// <summary>Las versiones archivadas de una ficha, de la más nueva a la más vieja.</summary>
public sealed record GetHistorialFichaQuery(int TramiteSigerId) : IRequest<IReadOnlyList<VersionFichaDto>>;

/// <remarks>
/// <b>No trae el contenido de las fotos.</b> Cada una lleva la ficha entera con sus seis
/// colecciones serializadas; una ficha con veinte pases traería veinte documentos completos solo
/// para pintar una lista de fechas. El contenido se pide de a una, al abrir una versión.
/// </remarks>
public sealed class GetHistorialFichaQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetHistorialFichaQuery, IReadOnlyList<VersionFichaDto>>
{
    public async Task<IReadOnlyList<VersionFichaDto>> Handle(GetHistorialFichaQuery q, CancellationToken ct) =>
        await ctx.FotosTramiteSiger.AsNoTracking()
            .Where(f => f.TramiteSigerId == q.TramiteSigerId)
            .OrderByDescending(f => f.Version)
            .Select(f => new VersionFichaDto(
                f.Version, f.Origen, f.CapturadaEl, f.Codigo,
                f.Version == OrigenFoto.VersionOriginal))
            .ToListAsync(ct);
}
