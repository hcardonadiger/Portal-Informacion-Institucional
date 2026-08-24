namespace Diger.TramitesEstado.Application.Siger.Historial.Queries.GetFotoOriginal;

/// <summary>La foto original de una ficha, para poder visitarla.</summary>
public sealed record GetFotoOriginalQuery(int TramiteSigerId) : IRequest<FotoOriginalDto?>;

/// <param name="Legible">Falso cuando el documento existe pero no se pudo leer. La pantalla
/// enseña el texto crudo en ese caso: un archivo ilegible sigue siendo mejor que nada.</param>
public sealed record FotoOriginalDto(
    int       TramiteSigerId,
    string    Codigo,
    int?      IdSiger,
    DateTime  CapturadaEl,
    bool      Legible,
    FichaFoto? Ficha,
    string    Contenido);

public sealed class GetFotoOriginalQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetFotoOriginalQuery, FotoOriginalDto?>
{
    public async Task<FotoOriginalDto?> Handle(GetFotoOriginalQuery q, CancellationToken ct)
    {
        var foto = await ctx.FotosTramiteSiger.AsNoTracking()
            .Where(f => f.TramiteSigerId == q.TramiteSigerId
                     && f.Version == OrigenFoto.VersionOriginal)
            .FirstOrDefaultAsync(ct);

        if (foto is null) return null;

        var ficha = FotoSigerSerializador.Leer(foto.Contenido);
        return new FotoOriginalDto(
            foto.TramiteSigerId, foto.Codigo, foto.IdSiger, foto.CapturadaEl,
            ficha is not null, ficha, foto.Contenido);
    }
}
