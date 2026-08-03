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
    string   Analista,
    EstadoExpediente EstadoExpediente,
    int      AvancePct,
    IReadOnlyList<EtapaAvanceDto> Etapas)
{
    public BandaAvance Banda => SemaforoAvance.Banda(AvancePct);
}

/// <summary>Última nota de seguimiento de un expediente, para la columna del tablero.</summary>
public sealed record NotaResumenDto(int ExpedienteId, string Texto, string CreadoPor, DateTime CreadoEl, int Total);

public sealed record TramitesSeguimientoDto(
    IReadOnlyList<TramiteSeguimientoDto> Tramites,
    IReadOnlyDictionary<int, NotaResumenDto> UltimaNotaPorExpediente,
    IReadOnlyList<string> Instituciones)
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
public sealed record GetTramitesSeguimientoQuery(string? InstitucionId, BandaAvance? Banda)
    : IRequest<TramitesSeguimientoDto>;

public sealed class GetTramitesSeguimientoQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetTramitesSeguimientoQuery, TramitesSeguimientoDto>
{
    public async Task<TramitesSeguimientoDto> Handle(GetTramitesSeguimientoQuery q, CancellationToken ct)
    {
        // ctx.Expedientes ya viene filtrado por el alcance institucional del usuario.
        var expedientes = await ctx.Expedientes
            .AsNoTracking()
            .Include(e => e.Tramites)
            .OrderBy(e => e.Institucion).ThenBy(e => e.Codigo)
            .Select(e => new
            {
                e.Id, e.Codigo, e.InstitucionId, e.Institucion, e.Analista, e.EstadoExpediente,
                Tramites = e.Tramites.OrderBy(t => t.TramiteIndex)
                    .Select(t => new { t.TramiteIndex, t.NombreTramite }).ToList()
            })
            .ToListAsync(ct);

        // El desplegable ofrece todas las instituciones del alcance, no solo las que
        // sobreviven al filtro: si no, al elegir una desaparecerían las demás.
        var instituciones = expedientes
            .Select(e => e.Institucion)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct().OrderBy(s => s).ToList();

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
                var hay = porTramite.TryGetValue((e.Id, t.TramiteIndex), out var d);
                var estados = hay ? d.Estados : sinDatosEstados;
                var aplica  = hay ? d.Aplica  : sinDatosAplica;

                var avance = (int)Math.Round(MetodologiaDigitalizacion.Global(estados, aplica) * 100);

                // Solo las etapas aplicables: una etapa apagada no debe figurar en el
                // modal como "0%", porque no es que falte — es que no corre.
                var etapas = MetodologiaDigitalizacion.Etapas
                    .Where(et => MetodologiaDigitalizacion.Aplica(et, aplica))
                    .Select(et => new EtapaAvanceDto(
                        et.Num, et.Label,
                        (int)Math.Round(et.Peso * 100),
                        (int)Math.Round(MetodologiaDigitalizacion.EtapaPct(et, estados) * 100)))
                    .ToList();

                tramites.Add(new TramiteSeguimientoDto(
                    e.Id, e.Codigo, e.InstitucionId ?? "", e.Institucion,
                    t.TramiteIndex, t.NombreTramite, e.Analista, e.EstadoExpediente,
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
