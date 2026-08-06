using Diger.TramitesEstado.Application.Dashboards.Common;
using Diger.TramitesEstado.Application.Expedientes.Seguimiento;

namespace Diger.TramitesEstado.Application.Dashboards.Queries.GetDigitalizacionDashboard;

public sealed record GetDigitalizacionDashboardQuery(
    string? InstitucionId = null, DateOnly? Desde = null, DateOnly? Hasta = null, EstadoTramite? Estado = null)
    : IRequest<DigitalizacionDashboardDto>;

public sealed class GetDigitalizacionDashboardQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetDigitalizacionDashboardQuery, DigitalizacionDashboardDto>
{
    public async Task<DigitalizacionDashboardDto> Handle(
        GetDigitalizacionDashboardQuery q, CancellationToken ct)
    {
        var expedientes = ctx.Expedientes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.InstitucionId))
            expedientes = expedientes.Where(x => x.InstitucionId == q.InstitucionId);

        var listaExp = await expedientes
            .Select(e => new
            {
                e.Id,
                e.Institucion,
                e.InstitucionId,
                e.Analista,
                Tramites = e.Tramites.Select(t => new { t.TramiteIndex, t.NombreTramite, t.FechaCreacion, t.EstadoTramite }).ToList()
            })
            .ToListAsync(ct);

        var institucionesActivas = listaExp.Select(e => e.InstitucionId).Distinct().Count();
        var totalTramites = listaExp.Sum(e => e.Tramites.Count);

        var expIds = listaExp.Select(e => e.Id).ToList();
        var avances = await ctx.ExpedienteEtapaAvances
            .AsNoTracking()
            .Where(a => expIds.Contains(a.ExpedienteId))
            .ToListAsync(ct);

        var avancePorTramite = avances
            .GroupBy(a => (a.ExpedienteId, a.TramiteIndex))
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var estados = new Dictionary<string, int>();
                    var aplica = new Dictionary<string, bool>();
                    foreach (var row in g)
                    {
                        if (row.SubId.StartsWith("APLICA:"))
                            aplica[row.SubId[7..]] = row.Estado == 1;
                        else
                            estados[row.SubId] = row.Estado;
                    }
                    return MetodologiaDigitalizacion.Global(estados, aplica);
                });

        var tramitesConAvance = new List<(int ExpId, string Institucion, string Tramite, string? Analista, double Avance)>();
        foreach (var exp in listaExp)
        foreach (var t in exp.Tramites)
        {
            if (q.Desde is { } desde && t.FechaCreacion < desde) continue;
            if (q.Hasta is { } hasta && t.FechaCreacion > hasta) continue;
            if (q.Estado is { } estado && t.EstadoTramite != estado) continue;
            var pct = avancePorTramite.TryGetValue((exp.Id, t.TramiteIndex), out var v) ? v : 0;
            tramitesConAvance.Add((exp.Id, exp.Institucion, t.NombreTramite, exp.Analista, pct));
        }

        var enOperacion = tramitesConAvance.Count(t => t.Avance >= 0.9999);
        var enProceso = tramitesConAvance.Count(t => t.Avance > 0 && t.Avance < 0.9999);
        var noIniciados = tramitesConAvance.Count(t => t.Avance <= 0);
        var avanceGlobal = tramitesConAvance.Count == 0
            ? 0 : tramitesConAvance.Average(t => t.Avance);

        // Avance por institución
        var porInstitucion = tramitesConAvance
            .GroupBy(t => t.Institucion)
            .Select(g => new AvanceInstitucionDto(
                g.Key,
                g.Count(),
                g.Count(t => t.Avance >= 0.9999),
                g.Average(t => t.Avance)))
            .OrderByDescending(x => x.AvancePromedio)
            .ToList();

        // Avance por etapa de la metodología
        var etapaPorTramite = avances
            .Where(a => !a.SubId.StartsWith("APLICA:"))
            .GroupBy(a => (a.ExpedienteId, a.TramiteIndex))
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(r => r.SubId, r => r.Estado));

        var aplicaPorTramite = avances
            .Where(a => a.SubId.StartsWith("APLICA:"))
            .GroupBy(a => (a.ExpedienteId, a.TramiteIndex))
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(r => r.SubId[7..], r => r.Estado == 1));

        var detallePorEtapa = new List<TramiteEtapaDetalleDto>();
        var porEtapa = MetodologiaDigitalizacion.Etapas.Select(etapa =>
        {
            var valores = new List<double>();
            int completados = 0;
            foreach (var exp in listaExp)
            foreach (var t in exp.Tramites)
            {
                if (q.Desde is { } d && t.FechaCreacion < d) continue;
                if (q.Hasta is { } h && t.FechaCreacion > h) continue;
                if (q.Estado is { } st && t.EstadoTramite != st) continue;
                var key = (exp.Id, t.TramiteIndex);
                var estados = etapaPorTramite.TryGetValue(key, out var e) ? e : new Dictionary<string, int>();
                var aplica = aplicaPorTramite.TryGetValue(key, out var a) ? a : new Dictionary<string, bool>();
                if (!MetodologiaDigitalizacion.Aplica(etapa, aplica)) continue;
                var pct = MetodologiaDigitalizacion.EtapaPct(etapa, estados);
                valores.Add(pct);
                if (pct >= 0.9999) completados++;
                detallePorEtapa.Add(new TramiteEtapaDetalleDto(
                    exp.Id, exp.Institucion, t.NombreTramite, exp.Analista,
                    etapa.Num, pct));
            }
            return new AvanceEtapaDto(
                etapa.Num,
                etapa.Label,
                valores.Count == 0 ? 0 : valores.Average(),
                completados,
                valores.Count);
        }).ToList();

        // Distribución de avance por rangos
        var distribucion = new List<ConteoDto>
        {
            new("0 – 25%", tramitesConAvance.Count(t => t.Avance < 0.25)),
            new("25 – 50%", tramitesConAvance.Count(t => t.Avance >= 0.25 && t.Avance < 0.50)),
            new("50 – 75%", tramitesConAvance.Count(t => t.Avance >= 0.50 && t.Avance < 0.75)),
            new("75 – 100%", tramitesConAvance.Count(t => t.Avance >= 0.75))
        };

        // Carga y avance por analista
        var porAnalista = tramitesConAvance
            .Where(t => !string.IsNullOrWhiteSpace(t.Analista))
            .GroupBy(t => t.Analista!)
            .Select(g => new AnalistaAvanceDto(
                g.Key,
                g.Count(),
                g.Count(t => t.Avance >= 0.9999),
                g.Average(t => t.Avance)))
            .OrderByDescending(x => x.TotalTramites)
            .ToList();

        // Todos los trámites con avance global
        var todosAvance = tramitesConAvance
            .Select(t => new TramiteAvanceDto(t.ExpId, t.Institucion, t.Tramite, t.Analista, t.Avance))
            .ToList();

        // Trámites con menor avance (top 10)
        var rezagados = todosAvance
            .OrderBy(t => t.Avance)
            .Take(10)
            .ToList();

        return new DigitalizacionDashboardDto(
            totalTramites, enOperacion, enProceso, noIniciados,
            institucionesActivas, avanceGlobal,
            porInstitucion, porEtapa, distribucion, porAnalista, rezagados, detallePorEtapa,
            todosAvance);
    }
}
