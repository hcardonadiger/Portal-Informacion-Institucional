namespace Diger.TramitesEstado.Application.Siger.Promocion.Queries.GetVistaPreviaPase;

/// <summary>Qué cambiaría al pasar un trámite del expediente a SIGER. No escribe nada.</summary>
public sealed record GetVistaPreviaPaseQuery(int ExpedienteId, int TramiteIndex)
    : IRequest<VistaPreviaPase>;

/// <remarks>
/// Existe para que nadie confirme a ciegas. Un pase sobre una ficha publicada reescribe lo que
/// ve el ciudadano; enseñar antes qué campos cambian, y de qué a qué, es la diferencia entre
/// decidir y apretar un botón.
/// </remarks>
public sealed class GetVistaPreviaPaseQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetVistaPreviaPaseQuery, VistaPreviaPase>
{
    public async Task<VistaPreviaPase> Handle(GetVistaPreviaPaseQuery q, CancellationToken ct)
    {
        var expediente = await ctx.Expedientes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == q.ExpedienteId, ct)
            ?? throw new NotFoundException(nameof(Expediente), q.ExpedienteId.ToString());

        var tramite = await ctx.Tramites.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ExpedienteId == q.ExpedienteId && x.TramiteIndex == q.TramiteIndex, ct)
            ?? throw new NotFoundException(nameof(ExpedienteTramite), $"{q.ExpedienteId}/{q.TramiteIndex}");

        // Solo se cargan las tres colecciones que el pase reemplaza; los pasos y los enlaces no
        // se tocan (D-11) y traerlos solo para contarlos sería trabajo para nada.
        TramiteSiger? actual = null;
        if (tramite.TramiteSigerId is { } id)
            actual = await ctx.TramitesSiger.AsNoTracking()
                .Include(f => f.Requisitos)
                .Include(f => f.Entregables)
                .Include(f => f.LugaresAtencion)
                .FirstOrDefaultAsync(f => f.Id == id, ct);

        var requisitos = await ctx.Requisitos.AsNoTracking()
            .Where(r => r.ExpedienteId == q.ExpedienteId && r.TramiteIndex == q.TramiteIndex)
            .OrderBy(r => r.Orden).ToListAsync(ct);

        var entregables = await ctx.EntregablesTramite.AsNoTracking()
            .Where(g => g.ExpedienteId == q.ExpedienteId && g.TramiteIndex == q.TramiteIndex)
            .OrderBy(g => g.Orden).ToListAsync(ct);

        var lugares = await ctx.LugaresTramite.AsNoTracking()
            .Where(l => l.ExpedienteId == q.ExpedienteId && l.TramiteIndex == q.TramiteIndex)
            .OrderBy(l => l.Orden).ToListAsync(ct);

        // El código que se le daría si es nueva se calcula acá para que el diálogo pueda
        // enseñarlo. No reserva nada: si dos personas promueven a la vez, el segundo pase
        // recalcula y toma el siguiente libre.
        var codigoSiEsNueva = "(se asigna al confirmar)";
        if (actual is null)
        {
            var codigos = await ctx.TramitesSiger.AsNoTracking()
                .Where(f => f.InstitucionId == expediente.InstitucionId)
                .Select(f => f.Codigo).ToListAsync(ct);

            var prefijo = codigos.FirstOrDefault(c =>
                !string.IsNullOrWhiteSpace(c) && !c.Contains("-P", StringComparison.OrdinalIgnoreCase));

            codigoSiEsNueva = CodigoPromovido.Siguiente(
                prefijo is null ? CodigoPromovido.PrefijoPorDefecto : CodigoPromovido.PrefijoDe(prefijo),
                codigos);
        }

        return DiffPase.Calcular(actual, tramite, expediente, codigoSiEsNueva,
                                 requisitos, entregables, lugares);
    }
}
