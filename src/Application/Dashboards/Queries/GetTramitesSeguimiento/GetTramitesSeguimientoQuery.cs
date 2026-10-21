using Diger.TramitesEstado.Application.Expedientes.Seguimiento;

namespace Diger.TramitesEstado.Application.Dashboards.Queries.GetTramitesSeguimiento;

/// <summary>Avance de una etapa de la metodología dentro de un trámite.</summary>
public sealed record EtapaAvanceDto(string Num, string Label, int PesoPct, int AvancePct)
{
    public BandaAvance Banda => SemaforoAvance.Banda(AvancePct);
}

/// <summary>Una fila del tablero: un trámite de un expediente.</summary>
public sealed record TramiteSeguimientoDto(
    int      ExpedienteId,
    string   Codigo,
    string   InstitucionId,
    string   Institucion,
    int      TramiteIndex,
    string   NombreTramite,

    /// <summary>Qué hace DIGER con este trámite. Null mientras nadie lo haya clasificado.</summary>
    string?  Accion,
    string   Analista,
    EstadoExpediente EstadoExpediente,
    int      AvancePct,
    IReadOnlyList<EtapaAvanceDto> Etapas)
{
    public BandaAvance Banda => SemaforoAvance.Banda(AvancePct);
}

/// <summary>Última nota de seguimiento de un expediente, para la columna del tablero.</summary>
public sealed record NotaResumenDto(int ExpedienteId, string Texto, string CreadoPor, DateTime CreadoEl, int Total);

/// <summary>Opción del desplegable de institución: solo las que tienen algún expediente vigente
/// (post corte-legado), no el catálogo completo — el filtro necesita el Id, no solo el nombre.</summary>
public sealed record InstitucionOpcionDto(string Id, string Nombre);

public sealed record TramitesSeguimientoDto(
    IReadOnlyList<TramiteSeguimientoDto> Tramites,
    IReadOnlyDictionary<int, NotaResumenDto> UltimaNotaPorExpediente,
    IReadOnlyList<InstitucionOpcionDto> Instituciones)
{
    public int Total     => Tramites.Count;
    public int Rezagados => Tramites.Count(t => t.Banda == BandaAvance.Rezagado);
    public int EnProceso => Tramites.Count(t => t.Banda == BandaAvance.EnProceso);
    public int Avanzados => Tramites.Count(t => t.Banda == BandaAvance.Avanzado);
    public int AvancePromedio => Tramites.Count > 0
        ? (int)Math.Round(Tramites.Average(t => t.AvancePct))
        : 0;
}

/// <param name="Banda">Filtro por banda del semáforo; null = todas.</param>
/// <param name="Accion">
/// Qué hace DIGER con el trámite (Acompañamiento, Digitalización, Soporte, Desarrollo). Null =
/// todas. El valor <see cref="FiltroAccion.SinClasificar"/> pide justamente las que nadie
/// clasificó — es un filtro tan legítimo como los demás y, mientras el campo sea nuevo, el más
/// útil de todos.
/// </param>
public sealed record GetTramitesSeguimientoQuery(
    string? InstitucionId, BandaAvance? Banda,
    DateOnly? Desde = null, DateOnly? Hasta = null, EstadoTramite? Estado = null,
    string? Accion = null)
    : IRequest<TramitesSeguimientoDto>;

/// <summary>Valores del filtro de acción que no son una acción en sí.</summary>
public static class FiltroAccion
{
    /// <summary>Centinela para «los que no tienen acción asignada». Va como constante y no como
    /// cadena suelta porque lo comparten la consulta y la vista.</summary>
    public const string SinClasificar = "(sin clasificar)";
}

public sealed class GetTramitesSeguimientoQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetTramitesSeguimientoQuery, TramitesSeguimientoDto>
{
    public async Task<TramitesSeguimientoDto> Handle(GetTramitesSeguimientoQuery q, CancellationToken ct)
    {
        // ctx.Expedientes ya viene filtrado por el alcance institucional del usuario.
        // Los legados (sin seguimiento confiable en la nueva metodología) no cuentan en tableros.
        var expQuery = ctx.Expedientes
            .AsNoTracking()
            .Where(e => e.FechaApertura != null && e.FechaApertura >= CorteLegado.Fecha)
            .Include(e => e.Tramites)
            .AsQueryable();

        var expedientes = await expQuery
            .OrderBy(e => e.Institucion).ThenBy(e => e.Codigo)
            .Select(e => new
            {
                e.Id, e.Codigo, e.InstitucionId, e.Institucion, e.Analista, e.EstadoExpediente,
                Tramites = e.Tramites.OrderBy(t => t.TramiteIndex)
                    .Select(t => new { t.TramiteIndex, t.NombreTramite, t.FechaCreacion, t.EstadoTramite, t.Accion }).ToList()
            })
            .ToListAsync(ct);

        // El desplegable ofrece todas las instituciones del alcance, no solo las que
        // sobreviven al filtro: si no, al elegir una desaparecerían las demás. Pero solo las que
        // de verdad tienen un expediente vigente — no el catálogo completo de instituciones.
        var instituciones = expedientes
            .Where(e => !string.IsNullOrWhiteSpace(e.InstitucionId))
            .GroupBy(e => e.InstitucionId!)
            .Select(g => new InstitucionOpcionDto(g.Key, g.First().Institucion))
            .OrderBy(i => i.Nombre)
            .ToList();

        if (!string.IsNullOrWhiteSpace(q.InstitucionId))
            expedientes = expedientes.Where(e => e.InstitucionId == q.InstitucionId).ToList();

        var expIds = expedientes.Select(e => e.Id).ToList();

        // Mismas filas crudas que usa el seguimiento: el avance es ponderado por etapa
        // y sub-paso, así que no se puede resolver con un COUNT agrupado en SQL.
        var filas = await ctx.ExpedienteEtapaAvances
            .AsNoTracking()
            .Where(a => expIds.Contains(a.ExpedienteId))
            .Select(a => new { a.ExpedienteId, a.TramiteIndex, a.SubId, a.Estado })
            .ToListAsync(ct);

        var porTramite = filas
            .GroupBy(a => (a.ExpedienteId, a.TramiteIndex))
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var estados = new Dictionary<string, int>();
                    var aplica  = new Dictionary<string, bool>();
                    foreach (var f in g)
                    {
                        if (f.SubId.StartsWith("APLICA:", StringComparison.Ordinal))
                            aplica[f.SubId["APLICA:".Length..]] = f.Estado == 1;
                        else
                            estados[f.SubId] = f.Estado;
                    }
                    return (Estados: (IReadOnlyDictionary<string, int>)estados,
                            Aplica:  (IReadOnlyDictionary<string, bool>)aplica);
                });

        var sinDatosEstados = (IReadOnlyDictionary<string, int>)new Dictionary<string, int>();
        var sinDatosAplica  = (IReadOnlyDictionary<string, bool>)new Dictionary<string, bool>();

        var tramites = new List<TramiteSeguimientoDto>();
        foreach (var e in expedientes)
        {
            foreach (var t in e.Tramites)
            {
                if (q.Desde is { } desde && t.FechaCreacion < desde) continue;
                if (q.Hasta is { } hasta && t.FechaCreacion > hasta) continue;
                if (q.Estado is { } estado && t.EstadoTramite != estado) continue;

                // «Sin clasificar» no es una acción, es la ausencia de una: se resuelve aparte
                // porque comparar contra el centinela como si fuera un valor guardado no
                // encontraría nada.
                if (!string.IsNullOrWhiteSpace(q.Accion))
                {
                    var sinAccion = string.IsNullOrWhiteSpace(t.Accion);
                    if (q.Accion == FiltroAccion.SinClasificar)
                    {
                        if (!sinAccion) continue;
                    }
                    else if (sinAccion || !string.Equals(t.Accion, q.Accion, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                var hay = porTramite.TryGetValue((e.Id, t.TramiteIndex), out var d);
                var estados = hay ? d.Estados : sinDatosEstados;
                var aplica  = hay ? d.Aplica  : sinDatosAplica;

                var avance = (int)Math.Round(MetodologiaDigitalizacion.Global(estados, aplica) * 100);

                var etapas = MetodologiaDigitalizacion.Etapas
                    .Where(et => MetodologiaDigitalizacion.Aplica(et, aplica))
                    .Select(et => new EtapaAvanceDto(
                        et.Num, et.Label,
                        (int)Math.Round(et.Peso * 100),
                        (int)Math.Round(MetodologiaDigitalizacion.EtapaPct(et, estados) * 100)))
                    .ToList();

                tramites.Add(new TramiteSeguimientoDto(
                    e.Id, e.Codigo, e.InstitucionId ?? "", e.Institucion,
                    t.TramiteIndex, t.NombreTramite, t.Accion, e.Analista, e.EstadoExpediente,
                    avance, etapas));
            }
        }

        if (q.Banda is { } banda)
            tramites = tramites.Where(t => t.Banda == banda).ToList();

        // Los más rezagados primero: el tablero existe para encontrarlos.
        tramites = tramites
            .OrderBy(t => t.AvancePct)
            .ThenBy(t => t.Institucion)
            .ThenBy(t => t.NombreTramite)
            .ToList();

        // Última nota + total, por expediente.
        var notas = await ctx.NotasSeguimiento
            .AsNoTracking()
            .Where(n => expIds.Contains(n.ExpedienteId))
            .Select(n => new { n.ExpedienteId, n.Texto, n.CreadoPor, n.CreadoEl })
            .ToListAsync(ct);

        var ultimaNota = notas
            .GroupBy(n => n.ExpedienteId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var u = g.OrderByDescending(n => n.CreadoEl).First();
                    return new NotaResumenDto(g.Key, u.Texto, u.CreadoPor, u.CreadoEl, g.Count());
                });

        return new TramitesSeguimientoDto(tramites, ultimaNota, instituciones);
    }
}
