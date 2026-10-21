using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Expedientes.Seguimiento;

namespace Diger.TramitesEstado.Application.Expedientes.Cronograma;

public sealed record GetCronogramaExpedienteQuery(int ExpedienteId, int? TramiteIndex = null)
    : IRequest<CronogramaExpedienteDto>;

public sealed class GetCronogramaExpedienteQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetCronogramaExpedienteQuery, CronogramaExpedienteDto>
{
    public async Task<CronogramaExpedienteDto> Handle(GetCronogramaExpedienteQuery q, CancellationToken ct)
    {
        var exp = await ctx.Expedientes.AsNoTracking()
            .Where(e => e.Id == q.ExpedienteId)
            .Select(e => new { e.Id, e.Codigo, e.Institucion })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Expediente), q.ExpedienteId);

        var tramites = await ctx.Tramites.AsNoTracking()
            .Where(t => t.ExpedienteId == q.ExpedienteId)
            .OrderBy(t => t.TramiteIndex)
            .Select(t => new TramiteItem(t.TramiteIndex, t.NombreTramite))
            .ToListAsync(ct);

        // t = -1 → vista centralizada: opera sobre todos los trámites a la vez
        if (q.TramiteIndex == -1 && tramites.Count > 0)
            return await CronogramaCentralizadoAsync(exp.Id, exp.Codigo, exp.Institucion, tramites, ct);

        var actual = q.TramiteIndex is int ti && tramites.Any(x => x.Index == ti)
            ? ti : tramites.Count > 0 ? tramites[0].Index : 0;

        var cronRows = await ctx.EtapaCronogramas.AsNoTracking()
            .Where(c => c.ExpedienteId == q.ExpedienteId && c.TramiteIndex == actual)
            .ToListAsync(ct);
        var cronMap = cronRows.ToDictionary(c => c.EtapaNum);

        var avanceRows = await ctx.ExpedienteEtapaAvances.AsNoTracking()
            .Where(a => a.ExpedienteId == q.ExpedienteId && a.TramiteIndex == actual)
            .ToListAsync(ct);

        var estados = new Dictionary<string, int>();
        foreach (var f in avanceRows.Where(f => !f.SubId.StartsWith("APLICA:")))
            estados[f.SubId] = f.Estado;

        var etapas = MetodologiaDigitalizacion.Etapas.Select(e =>
        {
            cronMap.TryGetValue(e.Num, out var row);
            return new EtapaCronogramaDto(
                e.Num, e.Label, e.Peso,
                row?.FechaInicio, row?.FechaFin, row?.FechaRealFin,
                row?.Responsable, row?.Observacion,
                MetodologiaDigitalizacion.EtapaPct(e, estados)
            );
        }).ToList();

        return new CronogramaExpedienteDto(exp.Id, exp.Codigo, exp.Institucion, tramites, actual, etapas);
    }

    /// <summary>Vista centralizada: por etapa muestra los valores comunes a todos los
    /// trámites (campo vacío + Variaciones=true cuando difieren) y el avance promedio.</summary>
    private async Task<CronogramaExpedienteDto> CronogramaCentralizadoAsync(
        int expId, string codigo, string institucion, IReadOnlyList<TramiteItem> tramites, CancellationToken ct)
    {
        var cronRows = await ctx.EtapaCronogramas.AsNoTracking()
            .Where(c => c.ExpedienteId == expId)
            .ToListAsync(ct);
        var cronMap = cronRows.ToDictionary(c => (c.TramiteIndex, c.EtapaNum));

        var avanceRows = await ctx.ExpedienteEtapaAvances.AsNoTracking()
            .Where(a => a.ExpedienteId == expId)
            .ToListAsync(ct);
        var estadosPorTramite = avanceRows
            .Where(f => !f.SubId.StartsWith("APLICA:"))
            .GroupBy(f => f.TramiteIndex)
            .ToDictionary(g => g.Key, g => g.ToDictionary(f => f.SubId, f => f.Estado));

        var etapas = MetodologiaDigitalizacion.Etapas.Select(e =>
        {
            var filas = tramites
                .Select(tr => cronMap.GetValueOrDefault((tr.Index, e.Num)))
                .ToList(); // null = trámite sin fila para esta etapa

            DateOnly? ComunF(Func<ExpedienteEtapaCronograma?, DateOnly?> sel)
            {
                var vals = filas.Select(sel).Distinct().ToList();
                return vals.Count == 1 ? vals[0] : null;
            }
            string? ComunS(Func<ExpedienteEtapaCronograma?, string?> sel)
            {
                var vals = filas.Select(sel).Distinct().ToList();
                return vals.Count == 1 ? vals[0] : null;
            }
            bool Difiere<T>(Func<ExpedienteEtapaCronograma?, T?> sel) =>
                filas.Select(sel).Distinct().Count() > 1;

            var variaciones =
                Difiere(f => f?.FechaInicio) || Difiere(f => f?.FechaFin) ||
                Difiere(f => f?.FechaRealFin) || Difiere(f => f?.Responsable) ||
                Difiere(f => f?.Observacion);

            var avanceProm = tramites.Average(tr =>
                MetodologiaDigitalizacion.EtapaPct(e, estadosPorTramite.GetValueOrDefault(tr.Index) ?? []));

            return new EtapaCronogramaDto(
                e.Num, e.Label, e.Peso,
                ComunF(f => f?.FechaInicio), ComunF(f => f?.FechaFin), ComunF(f => f?.FechaRealFin),
                ComunS(f => f?.Responsable), ComunS(f => f?.Observacion),
                avanceProm, variaciones);
        }).ToList();

        return new CronogramaExpedienteDto(expId, codigo, institucion, tramites, -1, etapas, EsCentralizado: true);
    }
}
