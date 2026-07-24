using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Application.Dashboards.Common;

namespace Diger.TramitesEstado.Application.Dashboards.Queries.GetReunionesDashboard;

public sealed record GetReunionesDashboardQuery(string? InstitucionId = null) : IRequest<ReunionesDashboardDto>;

public sealed class GetReunionesDashboardQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetReunionesDashboardQuery, ReunionesDashboardDto>
{
    public async Task<ReunionesDashboardDto> Handle(GetReunionesDashboardQuery q, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);
        var primerDiaMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var finMes = primerDiaMes.AddMonths(1).AddDays(-1);

        var r = ctx.Reuniones.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.InstitucionId)) r = r.Where(x => x.InstitucionId == q.InstitucionId);
        var total = await r.CountAsync(ct);
        var mes = await r.CountAsync(x => x.Fecha >= primerDiaMes && x.Fecha <= finMes, ct);
        var asistentes = total == 0 ? 0 : await r.SumAsync(x => x.Asistentes.Count, ct);

        var acuerdos = r.SelectMany(x => x.Acuerdos);

        // Un compromiso está "abierto" si no fue cumplido ni cancelado.
        var vencidos = await acuerdos.CountAsync(a =>
            a.Plazo != null && a.Plazo < hoy &&
            (a.Estado == EstadoCompromiso.Pendiente || a.Estado == EstadoCompromiso.EnProgreso || a.Estado == EstadoCompromiso.Reprogramado), ct);
        var proximos = await acuerdos.CountAsync(a =>
            a.Plazo != null && a.Plazo >= hoy &&
            (a.Estado == EstadoCompromiso.Pendiente || a.Estado == EstadoCompromiso.EnProgreso || a.Estado == EstadoCompromiso.Reprogramado), ct);
        var sinPlazo = await acuerdos.CountAsync(a =>
            a.Plazo == null &&
            (a.Estado == EstadoCompromiso.Pendiente || a.Estado == EstadoCompromiso.EnProgreso || a.Estado == EstadoCompromiso.Reprogramado), ct);

        var acuerdosTotal = await acuerdos.CountAsync(ct);
        var cumplidos     = await acuerdos.CountAsync(a => a.Estado == EstadoCompromiso.Cumplido, ct);
        var tasa = acuerdosTotal == 0 ? 0 : (int)Math.Round(cumplidos * 100.0 / acuerdosTotal);

        var porEstado = (await acuerdos.GroupBy(a => a.Estado).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct))
            .Select(x => new ConteoDto(x.Key.ToString(), x.C))
            .ToList();

        var porTipo = (await r.GroupBy(x => x.Tipo).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct))
            .OrderByDescending(x => x.C)
            .Select(x => new ConteoDto(string.IsNullOrWhiteSpace(x.Key) ? "Sin tipo" : x.Key!, x.C))
            .ToList();

        var porInstitucion = (await r.GroupBy(x => x.Institucion).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct))
            .OrderByDescending(x => x.C)
            .Select(x => new ConteoDto(string.IsNullOrWhiteSpace(x.Key) ? "Sin institución" : x.Key!, x.C))
            .ToList();

        // Compromisos abiertos con plazo: vencidos primero, luego próximos por fecha.
        var lista = (await r
            .SelectMany(x => x.Acuerdos.Select(a => new
            {
                a.Compromiso, a.Responsable, a.Plazo, a.Estado, RTitulo = x.Titulo, RId = x.Id
            }))
            .Where(a => a.Plazo != null &&
                (a.Estado == EstadoCompromiso.Pendiente || a.Estado == EstadoCompromiso.EnProgreso || a.Estado == EstadoCompromiso.Reprogramado))
            .OrderBy(a => a.Plazo)
            .Take(12)
            .ToListAsync(ct))
            .Select(a => new AcuerdoPendienteDto(
                a.Compromiso, a.Responsable, a.Plazo, a.RTitulo, a.RId, a.Plazo < hoy, a.Estado))
            .ToList();

        var desde12 = primerDiaMes.AddMonths(-11);
        var porMesRaw = (await r.Where(x => x.Fecha != null && x.Fecha >= desde12)
            .GroupBy(x => new { x.Fecha!.Value.Year, x.Fecha!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, C = g.Count() }).ToListAsync(ct))
            .Select(x => (x.Year, x.Month, x.C));

        // ── Personas capacitadas ───────────────────────────────────────────────
        // Sólo reuniones de capacitación; se traen planas y la deduplicación se hace en
        // memoria (normalizar nombres no es traducible a SQL). El volumen es acotado.
        var asistenciasCap = await r
            .Where(x => x.EsCapacitacionPlataforma || (x.Tipo != null && x.Tipo.Contains("Capacitaci")))
            .SelectMany(x => x.Asistentes.Select(a => new
            {
                Capacitacion = x.Titulo,
                a.Nombre,
                a.Correo,
                a.Institucion
            }))
            .ToListAsync(ct);

        var personasCapacitadas = asistenciasCap
            .Where(a => !string.IsNullOrWhiteSpace(a.Nombre) && !EsDiger(a.Institucion))
            .GroupBy(a => ClavePersona(a.Correo, a.Nombre))
            .Select(g => new PersonaCapacitadaDto(
                g.Select(x => x.Nombre.Trim()).First(),
                g.Select(x => x.Institucion?.Trim()).FirstOrDefault(i => !string.IsNullOrWhiteSpace(i)),
                g.Select(x => x.Capacitacion?.Trim())
                 .Where(t => !string.IsNullOrWhiteSpace(t))
                 .Distinct(StringComparer.OrdinalIgnoreCase)
                 .OrderBy(t => t, StringComparer.CurrentCultureIgnoreCase)
                 .ToList()!))
            .OrderBy(p => p.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new ReunionesDashboardDto(total, mes, asistentes, vencidos, proximos, sinPlazo,
            porTipo, porInstitucion, SerieMensual.Ultimos12(porMesRaw), lista,
            acuerdosTotal, cumplidos, tasa, porEstado, personasCapacitadas);
    }

    // ── Helpers de deduplicación ───────────────────────────────────────────────
    /// <summary>DIGER facilita las capacitaciones; su personal no cuenta como capacitado.</summary>
    private static bool EsDiger(string? institucion) =>
        !string.IsNullOrWhiteSpace(institucion) && Normalizar(institucion).Contains("DIGER");

    /// <summary>Clave de identidad: el correo manda; si no hay, el nombre normalizado.</summary>
    private static string ClavePersona(string? correo, string nombre) =>
        !string.IsNullOrWhiteSpace(correo)
            ? "@" + correo.Trim().ToLowerInvariant()
            : "#" + Normalizar(nombre);

    /// <summary>Mayúsculas, sin acentos y con espacios colapsados, para comparar nombres.</summary>
    private static string Normalizar(string s)
    {
        var desc = s.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(desc.Length);
        foreach (var ch in desc)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        return Regex.Replace(sb.ToString(), @"\s+", " ").ToUpperInvariant();
    }
}
