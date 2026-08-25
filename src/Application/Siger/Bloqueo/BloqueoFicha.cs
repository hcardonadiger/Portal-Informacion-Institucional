using Diger.TramitesEstado.Application.Siger.Importacion;

namespace Diger.TramitesEstado.Application.Siger.Bloqueo;

/// <summary>Dónde se edita una ficha, y por qué.</summary>
/// <param name="Bloqueada">Cierto si sus campos de contenido solo se editan en un expediente.</param>
/// <param name="EsBucket">Cierto si ese expediente es el contenedor de importados y no un
/// levantamiento; la pantalla lo dice distinto porque significa algo distinto.</param>
public sealed record BloqueoFichaDto(
    bool    Bloqueada,
    int?    ExpedienteId,
    string? ExpedienteCodigo,
    string? NombreTramite,
    bool    EsBucket);

/// <summary>
/// El bloqueo condicional de D-17: <b>una ficha enlazada a un expediente se edita solo allí.</b>
/// </summary>
/// <remarks>
/// <para>
/// La regla completa es una sola frase: existe un trámite de expediente que apunta a la ficha.
/// Ese mismo predicado gobierna la lectura en D-03 —«se trae de PD si existe, si no de SIGER»— y
/// la escritura acá. Una sola regla para las dos cosas.
/// </para>
/// <para>
/// <b>Vive en un solo lugar</b> porque la usan cuatro pantallas —el editor de la ficha, la
/// captura en lote, el llenado asistido y el detalle— y cuatro copias acabarían discrepando. La
/// discrepancia sería silenciosa y peor que no tener bloqueo: una pantalla dejaría escribir lo
/// que otra declara de solo lectura, y el siguiente pase desde el expediente borraría ese trabajo
/// sin dejar rastro.
/// </para>
/// </remarks>
public static class BloqueoFicha
{
    /// <summary>
    /// Quita de una consulta las fichas cuyos campos de contenido ya no se editan en la ficha
    /// (D-23). Se le pasa la consulta de trámites de expediente en vez de un contexto para que la
    /// condición se traduzca a SQL y no traiga mil identificadores a memoria.
    /// </summary>
    public static IQueryable<TramiteSiger> SinBloqueadas(
        this IQueryable<TramiteSiger> fichas,
        IQueryable<ExpedienteTramite> tramitesDeExpediente) =>
        fichas.Where(f => !tramitesDeExpediente.Any(t => t.TramiteSigerId == f.Id));
}

/// <summary>Dónde se edita esta ficha. Null nunca: una ficha sin enlazar responde «acá mismo».</summary>
public sealed record GetBloqueoFichaQuery(int TramiteSigerId) : IRequest<BloqueoFichaDto>;

public sealed class GetBloqueoFichaQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetBloqueoFichaQuery, BloqueoFichaDto>
{
    public async Task<BloqueoFichaDto> Handle(GetBloqueoFichaQuery q, CancellationToken ct)
    {
        var enlace = await ctx.Tramites.AsNoTracking()
            .Where(t => t.TramiteSigerId == q.TramiteSigerId)
            .Join(ctx.Expedientes.AsNoTracking(), t => t.ExpedienteId, e => e.Id,
                (t, e) => new { e.Id, e.Codigo, t.NombreTramite, e.OrigenExternoId })
            .FirstOrDefaultAsync(ct);

        return enlace is null
            ? new BloqueoFichaDto(false, null, null, null, false)
            : new BloqueoFichaDto(true, enlace.Id, enlace.Codigo, enlace.NombreTramite,
                                  BucketImportacion.EsBucket(enlace.OrigenExternoId));
    }
}
