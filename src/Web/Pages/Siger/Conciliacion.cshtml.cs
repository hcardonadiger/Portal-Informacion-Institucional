using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Pages.Siger;

/// <summary>
/// Bandeja de conciliación entre los trámites de los expedientes y el inventario SIGER.
/// Propone enlaces, deja que una persona los confirme y recuerda lo ya decidido.
/// </summary>
[Permission("Siger.Conciliacion", AccionModulo.Editar, "Conciliar trámites SIGER con expedientes")]
[Authorize(Policy = "Siger.Conciliacion.Editar")]
public sealed class ConciliacionModel(IApplicationDbContext ctx) : PageModel
{
    public const string TabPendientes   = "pendientes";
    public const string TabEnlazados    = "enlazados";
    public const string TabSinCandidato = "nuevas";
    public const string TabDescartados  = "descartados";

    /// <summary>Filas por página. Mantiene la pantalla legible y el POST pequeño.</summary>
    private const int TamanoPagina = 20;

    [BindProperty(SupportsGet = true)] public string  Tab    { get; set; } = TabPendientes;
    [BindProperty(SupportsGet = true)] public string? Buscar { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sigla  { get; set; }
    [BindProperty(SupportsGet = true)] public int?    Pg     { get; set; }

    /// <summary>Filas enviadas por el formulario. Solo se actúa sobre las marcadas.</summary>
    [BindProperty] public List<FilaPost> Seleccion { get; set; } = [];

    public PagedResult<FilaVm>    Resultado { get; private set; } = PagedResult<FilaVm>.Empty(TamanoPagina);
    public IReadOnlyList<string>  Siglas    { get; private set; } = [];
    public ResumenConciliacion    Resumen   { get; private set; } = new();

    /// <summary>
    /// Cuántos de alta confianza hay en el filtro completo, no solo en la página visible.
    /// Es lo que permite ofrecer "confirmar los N" sin obligar a recorrer todas las páginas.
    /// </summary>
    public int AltaConfianzaEnFiltro { get; private set; }

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    /// <summary>
    /// Búsqueda libre en el inventario SIGER para enlazar a mano lo que el cruce no propuso
    /// (ambiguos, parciales, o cualquier caso que el revisor quiera resolver por su cuenta).
    /// </summary>
    public async Task<IActionResult> OnGetBuscarSigerAsync(string? q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return new JsonResult(Array.Empty<object>());

        var term = q.Trim();
        var resultados = await ctx.TramitesSiger.AsNoTracking()
            .Where(t => t.Nombre.Contains(term) || t.Codigo.Contains(term)
                     || (t.Sigla != null && t.Sigla.Contains(term)))
            .OrderBy(t => t.Nombre)
            .Take(20)
            .Select(t => new { t.Id, t.Codigo, t.Nombre, t.Sigla, t.EstadoSiger })
            .ToListAsync(ct);

        return new JsonResult(resultados);
    }

    // ── Acciones ──────────────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostEnlazarAsync(CancellationToken ct)
    {
        var elegidas = Seleccion
            .Where(s => s.Marcada && s.TramiteSigerId is > 0)
            .GroupBy(s => s.ExpedienteTramiteId)
            .Select(g => g.First())
            .ToList();

        if (elegidas.Count == 0)
        {
            TempData["SuccessMsg"] = "No se marcó ningún trámite con ficha SIGER elegida.";
            return Redirigir();
        }

        var idsTramite = elegidas.Select(s => s.ExpedienteTramiteId).ToList();
        var idsSiger   = elegidas.Select(s => s.TramiteSigerId!.Value).Distinct().ToList();

        var permitidos   = await IdsEnAlcanceAsync(idsTramite, ct);
        var sigerExisten = await ctx.TramitesSiger.AsNoTracking()
            .Where(s => idsSiger.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct);

        var aplicables = elegidas
            .Where(s => permitidos.Contains(s.ExpedienteTramiteId)
                     && sigerExisten.Contains(s.TramiteSigerId!.Value))
            .ToList();

        if (aplicables.Count == 0)
        {
            TempData["SuccessMsg"] = "Ninguna de las filas marcadas pudo enlazarse.";
            return Redirigir();
        }

        var idsAplicables = aplicables.Select(s => s.ExpedienteTramiteId).ToList();
        var tramites  = await ctx.Tramites.Where(t => idsAplicables.Contains(t.Id)).ToListAsync(ct);
        var previas   = await ctx.ConciliacionesSiger
            .Where(c => idsAplicables.Contains(c.ExpedienteTramiteId)).ToListAsync(ct);

        foreach (var fila in aplicables)
        {
            var tramite = tramites.FirstOrDefault(t => t.Id == fila.ExpedienteTramiteId);
            if (tramite is null) continue;

            tramite.TramiteSigerId = fila.TramiteSigerId;
            RegistrarDecision(previas, fila.ExpedienteTramiteId,
                DecisionConciliacion.Enlazado, fila.TramiteSigerId);
        }

        await ctx.SaveChangesAsync(ct);
        TempData["SuccessMsg"] = $"{aplicables.Count} trámite(s) enlazado(s) al inventario SIGER.";
        return Redirigir();
    }

    /// <summary>
    /// Enlaza de una vez todos los de alta confianza del filtro actual, no solo los de la
    /// página visible. Se recalcula el cruce en el servidor: la petición no trae qué enlazar,
    /// así que un formulario manipulado no puede colar pares arbitrarios.
    /// </summary>
    public async Task<IActionResult> OnPostEnlazarAltaConfianzaAsync(CancellationToken ct)
    {
        var candidatas = AplicarFiltros(await ConstruirFilasAsync(ct))
            .Where(EsEnlazableAutomatico)
            .ToList();

        if (candidatas.Count == 0)
        {
            TempData["SuccessMsg"] = "No quedan tramites de alta confianza en el filtro actual.";
            return Redirigir();
        }

        var ids      = candidatas.Select(f => f.Id).ToList();
        var tramites = await ctx.Tramites.Where(t => ids.Contains(t.Id)).ToListAsync(ct);
        var previas  = await ctx.ConciliacionesSiger
            .Where(c => ids.Contains(c.ExpedienteTramiteId)).ToListAsync(ct);

        foreach (var fila in candidatas)
        {
            var tramite = tramites.FirstOrDefault(t => t.Id == fila.Id);
            if (tramite is null) continue;

            tramite.TramiteSigerId = fila.SugeridoId;
            RegistrarDecision(previas, fila.Id, DecisionConciliacion.Enlazado, fila.SugeridoId);
        }

        await ctx.SaveChangesAsync(ct);
        TempData["SuccessMsg"] = $"{candidatas.Count} tramite(s) de alta confianza enlazado(s) al inventario SIGER.";
        return Redirigir();
    }

    public Task<IActionResult> OnPostDescartarAsync(CancellationToken ct)
        => DecidirSinEnlaceAsync(DecisionConciliacion.Descartado,
            n => $"{n} trámite(s) marcado(s) como descartado(s).", ct);

    public Task<IActionResult> OnPostProponerAsync(CancellationToken ct)
        => DecidirSinEnlaceAsync(DecisionConciliacion.ProponerFichaNueva,
            n => $"{n} trámite(s) en cola para darlos de alta en SIGER.", ct);

    /// <summary>Deshace un enlace: el trámite vuelve a pendientes y se borra su decisión.</summary>
    public async Task<IActionResult> OnPostDesenlazarAsync(int id, CancellationToken ct)
    {
        var permitidos = await IdsEnAlcanceAsync([id], ct);
        if (!permitidos.Contains(id)) return NotFound();

        var tramite = await ctx.Tramites.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tramite is null) return NotFound();

        tramite.TramiteSigerId = null;
        await BorrarDecisionAsync(id, ct);

        await ctx.SaveChangesAsync(ct);
        TempData["SuccessMsg"] = "Enlace deshecho. El trámite volvió a la bandeja de pendientes.";
        return Redirigir();
    }

    /// <summary>Reabre una decisión previa (descartado o propuesta de ficha) para revisarla de nuevo.</summary>
    public async Task<IActionResult> OnPostReabrirAsync(int id, CancellationToken ct)
    {
        var permitidos = await IdsEnAlcanceAsync([id], ct);
        if (!permitidos.Contains(id)) return NotFound();

        await BorrarDecisionAsync(id, ct);
        await ctx.SaveChangesAsync(ct);
        TempData["SuccessMsg"] = "Decisión reabierta. El trámite volvió a la bandeja de pendientes.";
        return Redirigir();
    }

    // ── Apoyo ─────────────────────────────────────────────────────────────────

    private async Task<IActionResult> DecidirSinEnlaceAsync(
        DecisionConciliacion decision, Func<int, string> mensaje, CancellationToken ct)
    {
        var ids = Seleccion.Where(s => s.Marcada).Select(s => s.ExpedienteTramiteId).Distinct().ToList();
        if (ids.Count == 0)
        {
            TempData["SuccessMsg"] = "No se marcó ningún trámite.";
            return Redirigir();
        }

        var permitidos = await IdsEnAlcanceAsync(ids, ct);
        if (permitidos.Count == 0)
        {
            TempData["SuccessMsg"] = "Ninguna de las filas marcadas pudo procesarse.";
            return Redirigir();
        }

        var previas = await ctx.ConciliacionesSiger
            .Where(c => permitidos.Contains(c.ExpedienteTramiteId)).ToListAsync(ct);

        foreach (var idTramite in permitidos)
            RegistrarDecision(previas, idTramite, decision, null);

        await ctx.SaveChangesAsync(ct);
        TempData["SuccessMsg"] = mensaje(permitidos.Count);
        return Redirigir();
    }

    /// <summary>Crea o actualiza la decisión vigente del trámite. Hay a lo sumo una.</summary>
    private void RegistrarDecision(
        List<ConciliacionSiger> previas, int expedienteTramiteId,
        DecisionConciliacion decision, int? tramiteSigerId)
    {
        var existente = previas.FirstOrDefault(c => c.ExpedienteTramiteId == expedienteTramiteId);
        if (existente is null)
        {
            var nueva = new ConciliacionSiger
            {
                ExpedienteTramiteId = expedienteTramiteId,
                TramiteSigerId      = tramiteSigerId,
                Decision            = decision
            };
            ctx.ConciliacionesSiger.Add(nueva);
            previas.Add(nueva);
        }
        else
        {
            existente.TramiteSigerId = tramiteSigerId;
            existente.Decision       = decision;
        }
    }

    private async Task BorrarDecisionAsync(int expedienteTramiteId, CancellationToken ct)
    {
        var previa = await ctx.ConciliacionesSiger
            .FirstOrDefaultAsync(c => c.ExpedienteTramiteId == expedienteTramiteId, ct);
        if (previa is not null) ctx.ConciliacionesSiger.Remove(previa);
    }

    /// <summary>
    /// Filtra los ids recibidos del formulario a los que el usuario realmente puede ver.
    /// El join contra Expedientes aplica el filtro global de alcance institucional y de
    /// borrado lógico; <c>ctx.Tramites</c> por sí solo no lo tiene.
    /// </summary>
    private async Task<List<int>> IdsEnAlcanceAsync(List<int> ids, CancellationToken ct)
        => await (from et in ctx.Tramites
                  join e in ctx.Expedientes on et.ExpedienteId equals e.Id
                  where ids.Contains(et.Id)
                  select et.Id).ToListAsync(ct);

    private IActionResult Redirigir()
        => RedirectToPage(new { Tab, Buscar, Sigla, Pg });

    // ── Carga y clasificación ─────────────────────────────────────────────────

    /// <summary>
    /// Clasifica los trámites del alcance del usuario contra todo el inventario SIGER.
    /// Devuelve el universo completo, sin filtrar ni paginar, y deja calculado el resumen.
    /// </summary>
    private async Task<List<FilaVm>> ConstruirFilasAsync(CancellationToken ct)
    {
        var crudos = await (from et in ctx.Tramites
                            join e in ctx.Expedientes on et.ExpedienteId equals e.Id
                            select new
                            {
                                et.Id,
                                et.NombreTramite,
                                et.TramiteSigerId,
                                ExpedienteCodigo = e.Codigo,
                                e.Institucion,
                                e.InstitucionId
                            })
                           .AsNoTracking().ToListAsync(ct);

        var fichasCrudas = await ctx.TramitesSiger.AsNoTracking()
            .Select(s => new { s.Id, s.Codigo, s.Nombre, s.Sigla, s.EstadoSiger })
            .ToListAsync(ct);

        var decisiones = await ctx.ConciliacionesSiger.AsNoTracking()
            .ToDictionaryAsync(d => d.ExpedienteTramiteId, ct);

        var fichas = fichasCrudas
            .Select(f => new FichaIndexada(
                f.Id, f.Codigo, f.Nombre, f.Sigla, f.EstadoSiger,
                ConciliacionMatcher.Normalizar(f.Nombre)))
            .ToList();

        var porNombre = fichas.ToLookup(f => f.NomNorm, StringComparer.Ordinal);
        var porId     = fichas.ToDictionary(f => f.Id);

        var todas = new List<FilaVm>(crudos.Count);
        foreach (var t in crudos)
        {
            var norm = ConciliacionMatcher.Normalizar(t.NombreTramite);
            decisiones.TryGetValue(t.Id, out var decision);

            if (t.TramiteSigerId is int enlazadoId)
            {
                var ficha = porId.GetValueOrDefault(enlazadoId);
                todas.Add(new FilaVm(
                    t.Id, t.ExpedienteCodigo, t.Institucion, t.InstitucionId, t.NombreTramite,
                    CubetaConciliacion.AltaConfianza, [], null, false,
                    ficha?.ACandidato(), ficha is not null && ficha.NomNorm == norm,
                    decision?.Decision, decision?.CreatedBy, decision?.CreatedAt));
                continue;
            }

            var exactos    = porNombre[norm].ToList();
            var mismaSigla = exactos
                .Where(f => !string.IsNullOrWhiteSpace(f.Sigla)
                         && string.Equals(f.Sigla, t.InstitucionId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // El corte de 8 caracteres evita que nombres muy cortos arrastren medio inventario.
            var parciales = exactos.Count == 0 && norm.Length >= 8
                ? fichas.Where(f => f.NomNorm.Contains(norm, StringComparison.Ordinal)
                                 || norm.Contains(f.NomNorm, StringComparison.Ordinal))
                        .Take(15).ToList()
                : [];

            var cubeta     = ConciliacionMatcher.Clasificar(exactos.Count, mismaSigla.Count, parciales.Count);
            var candidatos = exactos.Count > 0 ? exactos : parciales;
            var sugerido   = cubeta switch
            {
                CubetaConciliacion.AltaConfianza  => mismaSigla[0].Id,
                CubetaConciliacion.MediaConfianza => exactos[0].Id,
                _                                 => (int?)null
            };

            todas.Add(new FilaVm(
                t.Id, t.ExpedienteCodigo, t.Institucion, t.InstitucionId, t.NombreTramite,
                cubeta,
                candidatos.Select(c => c.ACandidato()).ToList(),
                sugerido,
                cubeta == CubetaConciliacion.AltaConfianza,
                null, false,
                decision?.Decision, decision?.CreatedBy, decision?.CreatedAt));
        }

        Resumen = new ResumenConciliacion
        {
            Total           = todas.Count,
            Enlazados       = todas.Count(f => f.Enlazado is not null),
            AltaConfianza   = todas.Count(f => f.Enlazado is null && f.Decision is null
                                            && f.Cubeta == CubetaConciliacion.AltaConfianza),
            SinCandidato    = todas.Count(f => f.Enlazado is null && f.Decision is null
                                            && f.Cubeta == CubetaConciliacion.SinCandidato),
            Descartados     = todas.Count(f => f.Decision == DecisionConciliacion.Descartado),
            PropuestasFicha = todas.Count(f => f.Decision == DecisionConciliacion.ProponerFichaNueva),
            FichasSiger     = fichas.Count,
            FichasCubiertas = todas.Where(f => f.Enlazado is not null)
                                   .Select(f => f.Enlazado!.Id).Distinct().Count()
        };
        Resumen.Pendientes = todas.Count(f => EsPendiente(f));

        Siglas = todas.Select(f => f.InstitucionId)
                      .Where(s => !string.IsNullOrWhiteSpace(s))
                      .Select(s => s!).Distinct().OrderBy(s => s).ToList();

        return todas;
    }

    /// <summary>Aplica pestaña, búsqueda e institución. Lo que sobrevive es lo que el usuario ve.</summary>
    private List<FilaVm> AplicarFiltros(IEnumerable<FilaVm> todas)
    {
        var delTab = Tab switch
        {
            TabEnlazados    => todas.Where(f => f.Enlazado is not null),
            TabSinCandidato => todas.Where(f => f.Enlazado is null
                                             && f.Decision != DecisionConciliacion.Descartado
                                             && (f.Cubeta == CubetaConciliacion.SinCandidato
                                              || f.Decision == DecisionConciliacion.ProponerFichaNueva)),
            TabDescartados  => todas.Where(f => f.Decision == DecisionConciliacion.Descartado),
            _               => todas.Where(EsPendiente)
        };

        if (!string.IsNullOrWhiteSpace(Buscar))
        {
            var b = ConciliacionMatcher.Normalizar(Buscar);
            delTab = delTab.Where(f => ConciliacionMatcher.Normalizar(f.Nombre).Contains(b, StringComparison.Ordinal)
                                    || ConciliacionMatcher.Normalizar(f.ExpedienteCodigo).Contains(b, StringComparison.Ordinal));
        }
        if (!string.IsNullOrWhiteSpace(Sigla))
            delTab = delTab.Where(f => f.InstitucionId == Sigla);

        return delTab
            .OrderBy(f => f.Cubeta)
            .ThenBy(f => f.Institucion)
            .ThenBy(f => f.Nombre)
            .ToList();
    }

    private async Task CargarAsync(CancellationToken ct)
    {
        var filtradas = AplicarFiltros(await ConstruirFilasAsync(ct));

        AltaConfianzaEnFiltro = filtradas.Count(EsEnlazableAutomatico);

        var (_, page, size) = Paginacion.Normalizar(null, Pg, TamanoPagina);

        // Si un filtro deja menos páginas que la pedida, se cae a la última en vez de mostrar vacío.
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(filtradas.Count / (double)size));
        if (page > totalPaginas) page = totalPaginas;

        var pagina = filtradas.Skip((page - 1) * size).Take(size).ToList();
        Resultado = new PagedResult<FilaVm>(pagina, filtradas.Count, page, size);
    }

    /// <summary>Alta confianza con ficha sugerida: lo único que el botón global puede enlazar solo.</summary>
    private static bool EsEnlazableAutomatico(FilaVm f)
        => f.Enlazado is null
        && f.Decision is null
        && f.Cubeta == CubetaConciliacion.AltaConfianza
        && f.SugeridoId is not null;

    /// <summary>Pendiente = sin enlazar, sin decisión previa y con algún candidato que ofrecer.</summary>
    private static bool EsPendiente(FilaVm f)
        => f.Enlazado is null && f.Decision is null && f.Cubeta != CubetaConciliacion.SinCandidato;

    // ── Modelos de vista ──────────────────────────────────────────────────────

    private sealed record FichaIndexada(
        int Id, string Codigo, string Nombre, string? Sigla, string? Estado, string NomNorm)
    {
        public CandidatoVm ACandidato() => new(Id, Codigo, Nombre, Sigla, Estado);
    }

    public sealed record CandidatoVm(int Id, string Codigo, string Nombre, string? Sigla, string? Estado)
    {
        public string Texto => string.IsNullOrWhiteSpace(Sigla)
            ? $"{Codigo} — {Nombre}"
            : $"{Codigo} — {Nombre} ({Sigla})";
    }

    public sealed record FilaVm(
        int Id,
        string ExpedienteCodigo,
        string Institucion,
        string? InstitucionId,
        string Nombre,
        CubetaConciliacion Cubeta,
        IReadOnlyList<CandidatoVm> Candidatos,
        int? SugeridoId,
        bool Preseleccionado,
        CandidatoVm? Enlazado,
        bool NombreCoincide,
        DecisionConciliacion? Decision,
        string? DecididoPor,
        DateTime? DecididoEl);

    public sealed class FilaPost
    {
        public bool Marcada             { get; set; }
        public int  ExpedienteTramiteId { get; set; }
        public int? TramiteSigerId      { get; set; }
    }

    public sealed class ResumenConciliacion
    {
        public int Total           { get; set; }
        public int Enlazados       { get; set; }
        public int Pendientes      { get; set; }
        public int AltaConfianza   { get; set; }
        public int SinCandidato    { get; set; }
        public int Descartados     { get; set; }
        public int PropuestasFicha { get; set; }
        public int FichasSiger     { get; set; }
        public int FichasCubiertas { get; set; }

        public int PctCobertura => FichasSiger > 0
            ? (int)Math.Round(100.0 * FichasCubiertas / FichasSiger) : 0;
    }
}
