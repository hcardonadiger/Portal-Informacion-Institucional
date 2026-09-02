using Diger.TramitesEstado.Application.Dashboards.Common;
using Diger.TramitesEstado.Application.Expedientes.Seguimiento;

namespace Diger.TramitesEstado.Application.Dashboards.Queries.GetResumen;

public sealed record GetResumenQuery(
    IReadOnlyList<int>? TecnicoTemaIds = null, Guid? TecnicoUserId = null,
    DateOnly? Desde = null, DateOnly? Hasta = null, EstadoTramite? Estado = null) : IRequest<ResumenDto>;

public sealed class GetResumenQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetResumenQuery, ResumenDto>
{
    public async Task<ResumenDto> Handle(GetResumenQuery q, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);
        var primerDiaMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var finMes = primerDiaMes.AddMonths(1).AddDays(-1);

        // ── Tickets ──────────────────────────────────────────────────
        var tickets = ctx.Tickets.AsNoTracking();
        if (q.TecnicoUserId is Guid tuid)
        {
            var temaIds = q.TecnicoTemaIds ?? [];
            tickets = tickets.Where(t => (t.TemaId != null && temaIds.Contains(t.TemaId.Value)) || t.AsignadoAId == tuid);
        }

        var ticketsTotal      = await tickets.CountAsync(ct);
        var ticketsAbiertos   = await tickets.CountAsync(t => t.Estado == EstadoTicket.Abierto, ct);
        var ticketsEnProgreso = await tickets.CountAsync(t => t.Estado == EstadoTicket.EnProgreso, ct);
        var ticketsCriticos   = await tickets.CountAsync(t =>
            t.Prioridad == PrioridadTicket.Critica &&
            (t.Estado == EstadoTicket.Abierto || t.Estado == EstadoTicket.EnProgreso), ct);

        var ticketsResueltos = await tickets.CountAsync(t => t.Estado == EstadoTicket.Resuelto, ct);

        var resueltosFechas = await tickets
            .Where(t => t.Estado == EstadoTicket.Resuelto && t.FechaResolucion != null)
            .Select(t => new { t.CreatedAt, Resuelto = t.FechaResolucion!.Value })
            .ToListAsync(ct);
        var diasPromedio = resueltosFechas.Count == 0
            ? 0 : (int)Math.Round(resueltosFechas.Average(t => (t.Resuelto - t.CreatedAt).TotalDays));

        var iniMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var iniMesAnt = iniMes.AddMonths(-1);
        var creadosMes    = await tickets.CountAsync(t => t.CreatedAt >= iniMes, ct);
        var creadosMesAnt = await tickets.CountAsync(t => t.CreatedAt >= iniMesAnt && t.CreatedAt < iniMes, ct);

        var desde12 = iniMes.AddMonths(-11);
        var porMesRaw = (await tickets.Where(t => t.CreatedAt >= desde12)
            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, C = g.Count() }).ToListAsync(ct))
            .Select(x => (x.Year, x.Month, x.C));

        var resueltosMesRaw = (await tickets
            .Where(t => t.Estado == EstadoTicket.Resuelto && t.FechaResolucion != null && t.FechaResolucion >= desde12)
            .GroupBy(t => new { t.FechaResolucion!.Value.Year, t.FechaResolucion!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, C = g.Count() }).ToListAsync(ct))
            .Select(x => (x.Year, x.Month, x.C));

        // ── Expedientes (con filtros de período y estado de trámite) ──
        // Los legados (sin seguimiento confiable en la nueva metodología) no cuentan en tableros.
        var expedientes = ctx.Expedientes.AsNoTracking()
            .Where(e => e.FechaApertura != null && e.FechaApertura >= CorteLegado.Fecha);
        if (q.Desde is { } desdeExp)
            expedientes = expedientes.Where(e => e.Tramites.Any(t => t.FechaCreacion >= desdeExp));
        if (q.Hasta is { } hastaExp)
            expedientes = expedientes.Where(e => e.Tramites.Any(t => t.FechaCreacion <= hastaExp));
        if (q.Estado is { } estadoExp)
            expedientes = expedientes.Where(e => e.Tramites.Any(t => t.EstadoTramite == estadoExp));

        var expedientesTotal    = await expedientes.CountAsync(ct);
        var expedientesCerrados = await expedientes.CountAsync(e => e.EstadoExpediente == EstadoExpediente.Cerrado, ct);

        // ── Reuniones y acuerdos ─────────────────────────────────────
        var reunionesTotal = await ctx.Reuniones.CountAsync(ct);
        var reunionesMes   = await ctx.Reuniones.CountAsync(r => r.Fecha >= primerDiaMes && r.Fecha <= finMes, ct);

        var acuerdos = ctx.Reuniones.SelectMany(r => r.Acuerdos);
        var abiertos = acuerdos.Where(a =>
            a.Estado == EstadoCompromiso.Pendiente || a.Estado == EstadoCompromiso.EnProgreso || a.Estado == EstadoCompromiso.Reprogramado);
        var acuerdosVencidos  = await abiertos.CountAsync(a => a.Plazo != null && a.Plazo < hoy, ct);
        var acuerdosProximos  = await abiertos.CountAsync(a => a.Plazo != null && a.Plazo >= hoy, ct);
        var acuerdosTotal     = await acuerdos.CountAsync(ct);
        var acuerdosCumplidos = await acuerdos.CountAsync(a => a.Estado == EstadoCompromiso.Cumplido, ct);
        var tasaCumplimiento  = acuerdosTotal == 0 ? 0 : (int)Math.Round(acuerdosCumplidos * 100.0 / acuerdosTotal);

        // ── Capacitación (conteo simplificado) ───────────────────────
        var correosCap = await ctx.Reuniones.AsNoTracking()
            .Where(x => x.EsCapacitacionPlataforma || (x.Tipo != null && x.Tipo.Contains("Capacitaci")))
            .SelectMany(x => x.Asistentes.Select(a => new { a.Correo, a.Nombre }))
            .ToListAsync(ct);

        var asistenciasCap = correosCap.Count;
        var personasCap = correosCap
            .Where(a => !string.IsNullOrWhiteSpace(a.Nombre))
            .Select(a => !string.IsNullOrWhiteSpace(a.Correo) ? a.Correo.Trim().ToLowerInvariant() : a.Nombre!.Trim().ToLowerInvariant())
            .Distinct()
            .Count();

        // ── Digitalización ───────────────────────────────────────────
        var listaExp = await expedientes
            .Select(e => new
            {
                e.Id, e.Institucion, e.InstitucionId, e.Analista,
                Tramites = e.Tramites.Select(t => new { t.TramiteIndex, t.NombreTramite, t.FechaCreacion, t.EstadoTramite }).ToList()
            })
            .ToListAsync(ct);

        var digTotalTramites = listaExp.Sum(e => e.Tramites.Count);
        var expIds = listaExp.Select(e => e.Id).ToList();
        var avances = await ctx.ExpedienteEtapaAvances.AsNoTracking()
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

        var tramitesConAvance = new List<(string Institucion, string? Analista, double Avance)>();
        foreach (var exp in listaExp)
        foreach (var t in exp.Tramites)
        {
            if (q.Desde is { } dT && t.FechaCreacion < dT) continue;
            if (q.Hasta is { } hT && t.FechaCreacion > hT) continue;
            if (q.Estado is { } sT && t.EstadoTramite != sT) continue;
            var pct = avancePorTramite.TryGetValue((exp.Id, t.TramiteIndex), out var v) ? v : 0;
            tramitesConAvance.Add((exp.Institucion, exp.Analista, pct));
        }

        var digEnOperacion  = tramitesConAvance.Count(t => t.Avance >= 0.9999);
        var digEnProceso    = tramitesConAvance.Count(t => t.Avance > 0 && t.Avance < 0.9999);
        var digNoIniciados  = tramitesConAvance.Count(t => t.Avance <= 0);
        var digAvanceGlobal = tramitesConAvance.Count == 0
            ? 0 : tramitesConAvance.Average(t => t.Avance);

        var digPorInstitucion = tramitesConAvance
            .GroupBy(t => t.Institucion)
            .Select(g => new AvanceInstitucionDto(
                g.Key, g.Count(),
                g.Count(t => t.Avance >= 0.9999),
                g.Average(t => t.Avance)))
            .OrderByDescending(x => x.AvancePromedio)
            .ToList();

        var digPorAnalista = tramitesConAvance
            .Where(t => !string.IsNullOrWhiteSpace(t.Analista))
            .GroupBy(t => t.Analista!)
            .Select(g => new AnalistaAvanceDto(
                g.Key, g.Count(),
                g.Count(t => t.Avance >= 0.9999),
                g.Average(t => t.Avance)))
            .OrderByDescending(x => x.TotalTramites)
            .ToList();

        return new ResumenDto(
            ticketsAbiertos, ticketsEnProgreso, ticketsCriticos, ticketsTotal,
            ticketsResueltos, diasPromedio,
            new TendenciaDto(creadosMes, creadosMesAnt),
            SerieMensual.Ultimos12(porMesRaw),
            SerieMensual.Ultimos12(resueltosMesRaw),
            expedientesTotal, expedientesCerrados, expedientesTotal - expedientesCerrados,
            reunionesTotal, reunionesMes,
            acuerdosVencidos, acuerdosProximos,
            acuerdosTotal, acuerdosCumplidos, tasaCumplimiento,
            personasCap, asistenciasCap,
            digTotalTramites, digEnOperacion, digEnProceso, digNoIniciados, digAvanceGlobal,
            digPorInstitucion, digPorAnalista);
    }
}
