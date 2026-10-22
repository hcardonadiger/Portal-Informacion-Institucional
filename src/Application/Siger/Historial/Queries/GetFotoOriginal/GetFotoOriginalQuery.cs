namespace Diger.TramitesEstado.Application.Siger.Historial.Queries.GetFotoOriginal;

/// <summary>
/// Una versión archivada de una ficha, para poder visitarla. Por omisión la 0 —el inventario tal
/// como llegó de SIGER— que es la que se pide desde el detalle de la ficha; las demás las escribe
/// cada pase desde un expediente.
/// </summary>
public sealed record GetFotoOriginalQuery(int TramiteSigerId, int Version = OrigenFoto.VersionOriginal)
    : IRequest<FotoOriginalDto?>;

/// <param name="Legible">Falso cuando el documento existe pero no se pudo leer. La pantalla
/// enseña el texto crudo en ese caso: un archivo ilegible sigue siendo mejor que nada.</param>
public sealed record FotoOriginalDto(
    int       TramiteSigerId,
    int       Version,
    string    Origen,
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
                     && f.Version == q.Version)
            .FirstOrDefaultAsync(ct);

        if (foto is null) return null;

        var ficha = FotoSigerSerializador.Leer(foto.Contenido);
        return new FotoOriginalDto(
            foto.TramiteSigerId, foto.Version, foto.Origen, foto.Codigo, foto.IdSiger, foto.CapturadaEl,
            ficha is not null, ficha, foto.Contenido);
    }
}
