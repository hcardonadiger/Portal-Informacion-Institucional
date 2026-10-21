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

        var registros = asistenciasCap
            .Where(a => !string.IsNullOrWhiteSpace(a.Nombre) && !EsDiger(a.Institucion))
            .Select(a => new RegistroAsistencia(
                a.Nombre.Trim(), a.Correo?.Trim(), a.Institucion?.Trim(), a.Capacitacion?.Trim()))
            .ToList();

        var personasCapacitadas = ResolverPersonas(registros);

        // Asistencias registradas (filas, no personas): en capacitaciones y en todas las reuniones.
        var asistenciasEnCapacitaciones = registros.Count;
        var asistenciasTotales = await r.SumAsync(x => x.Asistentes.Count, ct);

        return new ReunionesDashboardDto(total, mes, asistentes, vencidos, proximos, sinPlazo,
            porTipo, porInstitucion, SerieMensual.Ultimos12(porMesRaw), lista,
            acuerdosTotal, cumplidos, tasa, porEstado, personasCapacitadas,
            asistenciasEnCapacitaciones, asistenciasTotales);
    }

    private sealed record RegistroAsistencia(string Nombre, string? Correo, string? Institucion, string? Capacitacion);

    /// <summary>
    /// Resuelve la identidad de las personas y agrupa sus capacitaciones.
    /// Dos registros son la misma persona si (a) comparten correo, o (b) sus nombres coinciden
    /// por tokens: uno contenido en el otro, mismo primer nombre y al menos 2 tokens en común.
    /// Se usa union-find para que las coincidencias encadenen (A=B y B=C ⇒ A=C).
    /// </summary>
    private static IReadOnlyList<PersonaCapacitadaDto> ResolverPersonas(List<RegistroAsistencia> registros)
    {
        var n = registros.Count;
        var padre = Enumerable.Range(0, n).ToArray();

        int Raiz(int x) { while (padre[x] != x) { padre[x] = padre[padre[x]]; x = padre[x]; } return x; }
        void Unir(int a, int b) { int ra = Raiz(a), rb = Raiz(b); if (ra != rb) padre[rb] = ra; }

        // (a) mismo correo
        var porCorreo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < n; i++)
        {
            var c = registros[i].Correo;
            if (string.IsNullOrWhiteSpace(c)) continue;
            var clave = c.Trim().ToLowerInvariant();
            if (porCorreo.TryGetValue(clave, out var j)) Unir(i, j); else porCorreo[clave] = i;
        }

        // (b) nombres compatibles
        var tokens = registros.Select(x => Tokens(x.Nombre)).ToArray();
        for (var i = 0; i < n; i++)
            for (var j = i + 1; j < n; j++)
                if (Raiz(i) != Raiz(j) && MismoNombre(tokens[i], tokens[j])) Unir(i, j);

        return registros
            .Select((reg, idx) => (reg, raiz: Raiz(idx)))
            .GroupBy(x => x.raiz)
            .Select(g =>
            {
                // Se conserva el nombre más completo y la institución más específica.
                var nombre = g.Select(x => x.reg.Nombre)
                              .OrderByDescending(x => Tokens(x).Count).ThenByDescending(x => x.Length)
                              .First();
                var institucion = g.Select(x => x.reg.Institucion)
                                   .FirstOrDefault(i => !string.IsNullOrWhiteSpace(i) && i!.Trim() != "—");
                var caps = g.Select(x => x.reg.Capacitacion)
                            .Where(t => !string.IsNullOrWhiteSpace(t))
                            .Select(t => t!)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(t => t, StringComparer.CurrentCultureIgnoreCase)
                            .ToList();
                return new PersonaCapacitadaDto(nombre, institucion, caps);
            })
            .OrderBy(p => p.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Tokens del nombre en orden, normalizados (sin acentos, mayúsculas).</summary>
    private static List<string> Tokens(string nombre) =>
        Normalizar(nombre).Split(' ', StringSplitOptions.RemoveEmptyEntries)
                          .Where(t => t.Length > 1)   // descarta iniciales sueltas ("J.")
                          .ToList();

    /// <summary>¿Son el mismo nombre? Mismo primer nombre, uno contenido en el otro y ≥2 en común.</summary>
    private static bool MismoNombre(List<string> a, List<string> b)
    {
        if (a.Count < 2 || b.Count < 2) return false;

        // El primer nombre debe coincidir: evita unir "Ana Perez" con "Luis Ana Perez".
        if (!string.Equals(a[0], b[0], StringComparison.Ordinal)) return false;

        var sa = a.ToHashSet(StringComparer.Ordinal);
        var sb = b.ToHashSet(StringComparer.Ordinal);
        if (sa.Count(sb.Contains) < 2) return false;      // al menos nombre + un apellido
        return sa.IsSubsetOf(sb) || sb.IsSubsetOf(sa);    // uno es forma corta del otro
    }

    // ── Helpers de deduplicación ───────────────────────────────────────────────
    /// <summary>DIGER facilita las capacitaciones; su personal no cuenta como capacitado.</summary>
    private static bool EsDiger(string? institucion) =>
        !string.IsNullOrWhiteSpace(institucion) && Normalizar(institucion).Contains("DIGER");

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
