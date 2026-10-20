namespace Diger.TramitesEstado.Application.Siger.Historial.Commands.CapturarFotosOriginales;

/// <summary>Guarda la foto original de cada ficha SIGER que todavía no la tenga.</summary>
public sealed record CapturarFotosOriginalesCommand : IRequest<ResultadoCapturaOriginal>;

/// <param name="Capturadas">Fichas retratadas en esta corrida.</param>
/// <param name="YaTenian">Fichas que ya estaban en el archivo y se dejaron intactas.</param>
/// <param name="Total">Fichas en el inventario.</param>
public sealed record ResultadoCapturaOriginal(int Capturadas, int YaTenian, int Total);

/// <remarks>
/// <para>
/// <b>Es idempotente y se puede correr varias veces.</b> Nunca reescribe una foto ya tomada: el
/// original se retrata una sola vez y después es inmutable. Si se pudiera repisar, bastaría una
/// segunda corrida —después de que alguien editara fichas— para que el «original» dejara de
/// serlo, que es exactamente lo que esta tabla existe para impedir.
/// </para>
/// <para>
/// <b>Va por lotes</b> porque son más de mil fichas con seis colecciones hijas cada una. Cada
/// lote guarda por su cuenta, así que una corrida interrumpida no pierde lo ya retratado: la
/// siguiente sigue donde quedó.
/// </para>
/// </remarks>
public sealed class CapturarFotosOriginalesCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<CapturarFotosOriginalesCommand, ResultadoCapturaOriginal>
{
    private const int TamanoLote = 100;

    public async Task<ResultadoCapturaOriginal> Handle(
        CapturarFotosOriginalesCommand _, CancellationToken ct)
    {
        var yaRetratadas = (await ctx.FotosTramiteSiger
            .AsNoTracking()
            .Where(f => f.Version == OrigenFoto.VersionOriginal)
            .Select(f => f.TramiteSigerId)
            .ToListAsync(ct))
            .ToHashSet();

        var todas = await ctx.TramitesSiger.AsNoTracking()
            .Select(t => t.Id).OrderBy(id => id).ToListAsync(ct);

        // El descarte se hace en memoria a propósito: llevarlo a la consulta como un NOT IN con
        // más de mil identificadores rompería el tope de 2 100 parámetros de SQL Server.
        var pendientes = todas.Where(id => !yaRetratadas.Contains(id)).ToList();

        var capturadas = 0;
        foreach (var lote in EnLotes(pendientes, TamanoLote))
            capturadas += await CapturarLoteAsync(lote, ct);

        return new ResultadoCapturaOriginal(capturadas, yaRetratadas.Count, todas.Count);
    }

    private async Task<int> CapturarLoteAsync(List<int> ids, CancellationToken ct)
    {
        var fichas = await ctx.TramitesSiger.AsNoTracking()
            .Where(t => ids.Contains(t.Id)).ToListAsync(ct);

        // Las seis colecciones se traen por separado en vez de con Include. Seis Include de
        // colecciones en una misma consulta producen el producto cartesiano entre todas ellas:
        // una ficha con diez requisitos, cinco pasos y cuatro lugares deja de ser diecinueve
        // filas y pasa a ser doscientas. Con mil fichas eso no termina nunca.
        var pasos = (await ctx.PasosSiger.AsNoTracking()
            .Where(x => ids.Contains(x.TramiteSigerId)).ToListAsync(ct)).ToLookup(x => x.TramiteSigerId);
        var requisitos = (await ctx.RequisitosSiger.AsNoTracking()
            .Where(x => ids.Contains(x.TramiteSigerId)).ToListAsync(ct)).ToLookup(x => x.TramiteSigerId);
        var entregables = (await ctx.EntregablesSiger.AsNoTracking()
            .Where(x => ids.Contains(x.TramiteSigerId)).ToListAsync(ct)).ToLookup(x => x.TramiteSigerId);
        var lugares = (await ctx.LugaresAtencionSiger.AsNoTracking()
            .Where(x => ids.Contains(x.TramiteSigerId)).ToListAsync(ct)).ToLookup(x => x.TramiteSigerId);
        var enlaces = (await ctx.EnlacesSiger.AsNoTracking()
            .Where(x => ids.Contains(x.TramiteSigerId)).ToListAsync(ct)).ToLookup(x => x.TramiteSigerId);
        var tareas = (await ctx.TareasDigitalizacionSiger.AsNoTracking()
            .Where(x => ids.Contains(x.TramiteSigerId)).ToListAsync(ct)).ToLookup(x => x.TramiteSigerId);

        var ahora = DateTime.UtcNow;
        foreach (var ficha in fichas)
        {
            ficha.Pasos                = [.. pasos[ficha.Id]];
            ficha.Requisitos           = [.. requisitos[ficha.Id]];
            ficha.Entregables          = [.. entregables[ficha.Id]];
            ficha.LugaresAtencion      = [.. lugares[ficha.Id]];
            ficha.Enlaces              = [.. enlaces[ficha.Id]];
            ficha.TareasDigitalizacion = [.. tareas[ficha.Id]];

            ctx.FotosTramiteSiger.Add(new FotoTramiteSiger
            {
                TramiteSigerId = ficha.Id,
                Version        = OrigenFoto.VersionOriginal,
                Origen         = OrigenFoto.SigerOriginal,
                Codigo         = ficha.Codigo,
                IdSiger        = ficha.IdSiger,
                CapturadaEl    = ahora,
                Contenido      = FotoSigerSerializador.Serializar(FotoSigerSerializador.Retratar(ficha))
            });
        }

        // Un solo SaveChanges por lote, sin transacción explícita: EnableRetryOnFailure y
        // BeginTransaction no conviven, y EF ya envuelve cada SaveChanges en su transacción.
        await ctx.SaveChangesAsync(ct);
        return fichas.Count;
    }

    private static IEnumerable<List<int>> EnLotes(List<int> origen, int tamano)
    {
        for (var i = 0; i < origen.Count; i += tamano)
            yield return origen.GetRange(i, Math.Min(tamano, origen.Count - i));
    }
}
