using Diger.TramitesEstado.Application.Siger.Llenado;
using Diger.TramitesEstado.Application.Siger.Llenado.Commands.GenerarPropuestas;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Pages.Siger;

/// <summary>
/// La cola donde se revisa lo que el llenado asistido propuso para las fichas incompletas.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nada de lo que propone el sistema llega a una ficha sin pasar por acá</b> (D-24). De las
/// 1 057 fichas del inventario, 1 032 no tienen categoría, modalidad, tiempo ni costo; escribir
/// esos valores en automático habría puesto datos sin verificar en el portal que ve el ciudadano,
/// y hacerlo a mano son meses. Esta pantalla es el punto medio: la máquina propone en masa, la
/// persona decide en bloque.
/// </para>
/// <para>
/// <b>Aprobar por tandas es el punto, no una comodidad.</b> Revisar mil propuestas de una en una
/// reproduce exactamente el problema que esta fase viene a resolver. Por eso se filtra por
/// institución, campo y certeza, y por eso existe el botón que aprueba <i>todo lo que coincide con
/// el filtro</i>: quien revisa mira veinte propuestas de tiempo de certeza alta de una
/// institución, comprueba que la regla acierta, y acepta las trescientas restantes.
/// </para>
/// <para>
/// <b>Aprobar nunca pisa lo que escribió una persona.</b> Antes de aplicar un valor se comprueba
/// que el campo siga vacío. Si alguien lo llenó a mano entre la propuesta y la aprobación, la
/// propuesta se descarta y se reporta aparte: el trabajo humano gana siempre.
/// </para>
/// </remarks>
[Authorize]
[Permission("Siger.Llenado", AccionModulo.Ver, "Ver la cola del llenado asistido")]
public sealed class LlenadoModel(IApplicationDbContext ctx, ISender sender) : PageModel
{
    private const int TamanoPagina = 25;

    /// <summary>Cuántas propuestas se procesan por vuelta al aprobar en bloque. Existe porque una
    /// aprobación por filtro puede alcanzar miles de filas, y meter miles de identificadores en un
    /// IN de SQL Server rompe el tope de 2 100 parámetros.</summary>
    private const int TamanoLote = 200;

    [BindProperty(SupportsGet = true)] public string  Tab     { get; set; } = "pendientes";
    [BindProperty(SupportsGet = true)] public string? Sigla   { get; set; }
    [BindProperty(SupportsGet = true)] public string? Campo   { get; set; }
    [BindProperty(SupportsGet = true)] public string? Certeza { get; set; }
    [BindProperty(SupportsGet = true)] public string? Buscar  { get; set; }
    [BindProperty(SupportsGet = true)] public int?    Pg      { get; set; }

    [BindProperty] public List<int> Seleccion { get; set; } = [];

    public PagedResult<FilaVm>   Resultado { get; private set; } = PagedResult<FilaVm>.Empty(TamanoPagina);
    public IReadOnlyList<string> Siglas    { get; private set; } = [];

    public int Pendientes { get; private set; }
    public int Aprobadas  { get; private set; }
    public int Rechazadas { get; private set; }

    /// <summary>Cuántas propuestas alcanzaría el botón de aprobar por filtro. Se enseña junto al
    /// botón: aprobar en bloque sin saber cuántas son es firmar en blanco.</summary>
    public int AlcanceDelFiltro { get; private set; }

    /// <summary>Huecos que siguen vacíos y sin propuesta. Es la parte del inventario que ninguna
    /// regla supo derivar y que necesita a una persona; se enseña para que no pase por completa.</summary>
    public int HuecosSinPropuesta { get; private set; }

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    // ── Acciones ──────────────────────────────────────────────────────────────

    [Permission("Siger.Llenado", AccionModulo.Editar, "Generar propuestas de llenado")]
    public async Task<IActionResult> OnPostGenerarAsync(CancellationToken ct)
    {
        var r = await sender.Send(new GenerarPropuestasLlenadoCommand(), ct);

        var porCampo = r.PorCampo.Count == 0
            ? ""
            : " (" + string.Join(", ", r.PorCampo.OrderByDescending(x => x.Value).Select(x => $"{x.Value} de {x.Key}")) + ")";

        TempData["SuccessMsg"] =
            $"Se revisaron {r.FichasRevisadas} fichas con huecos. Propuestas nuevas: {r.Creadas}{porCampo}. " +
            $"Ya estaban en cola: {r.YaEstaban}. No se insistió con {r.RespetadasPorRechazo} ya rechazadas. " +
            $"Huecos que ninguna regla supo llenar: {r.SinPropuesta}.";

        return Redirigir();
    }

    [Permission("Siger.Llenado", AccionModulo.Editar, "Aprobar propuestas de llenado")]
    public Task<IActionResult> OnPostAprobarAsync(CancellationToken ct) => DecidirMarcadasAsync(true, ct);

    [Permission("Siger.Llenado", AccionModulo.Editar, "Rechazar propuestas de llenado")]
    public Task<IActionResult> OnPostRechazarAsync(CancellationToken ct) => DecidirMarcadasAsync(false, ct);

    /// <summary>
    /// Aprueba todo lo que coincide con el filtro, no solo lo marcado en la página visible. Es la
    /// única forma de que mil propuestas se resuelvan en una tarde y no en un mes.
    /// </summary>
    [Permission("Siger.Llenado", AccionModulo.Editar, "Aprobar en bloque por filtro")]
    public async Task<IActionResult> OnPostAprobarFiltroAsync(CancellationToken ct)
    {
        var ids = await Filtrada(Pendientes: true).Select(p => p.Id).ToListAsync(ct);
        return await DecidirAsync(ids, aprobar: true, ct);
    }

    private async Task<IActionResult> DecidirMarcadasAsync(bool aprobar, CancellationToken ct)
    {
        var ids = Seleccion.Distinct().ToList();
        if (ids.Count == 0)
        {
            TempData["SuccessMsg"] = "No se marcó ninguna propuesta.";
            return Redirigir();
        }

        return await DecidirAsync(ids, aprobar, ct);
    }

    private async Task<IActionResult> DecidirAsync(List<int> ids, bool aprobar, CancellationToken ct)
    {
        var quien = User.Identity?.Name ?? "desconocido";
        var ahora = DateTime.UtcNow;

        var aplicadas = 0;
        var superadas = 0;   // alguien llenó el campo a mano mientras tanto
        var ilegibles = 0;   // el valor guardado ya no se puede interpretar

        for (var i = 0; i < ids.Count; i += TamanoLote)
        {
            var lote = ids.GetRange(i, Math.Min(TamanoLote, ids.Count - i));

            var propuestas = await ctx.PropuestasLlenado
                .Where(p => lote.Contains(p.Id) && p.Estado == EstadoPropuesta.Pendiente)
                .ToListAsync(ct);

            if (propuestas.Count == 0) continue;

            var fichaIds = propuestas.Select(p => p.TramiteSigerId).Distinct().ToList();
            var fichas = await ctx.TramitesSiger
                .Where(t => fichaIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, ct);

            foreach (var p in propuestas)
            {
                p.DecididaEl  = ahora;
                p.DecididaPor = quien;

                if (!aprobar)
                {
                    p.Estado = EstadoPropuesta.Rechazada;
                    continue;
                }

                if (!fichas.TryGetValue(p.TramiteSigerId, out var ficha))
                {
                    p.Estado = EstadoPropuesta.Rechazada;
                    superadas++;
                    continue;
                }

                // El trabajo humano gana. Si el campo ya no está vacío es porque alguien lo llenó
                // entre la propuesta y esta aprobación, y pisarlo con un valor derivado sería
                // exactamente lo que D-24 existe para impedir.
                if (!ValorLlenado.EstaVacio(ficha, p.Campo))
                {
                    p.Estado = EstadoPropuesta.Rechazada;
                    superadas++;
                    continue;
                }

                if (!ValorLlenado.Aplicar(ficha, p.Campo, p.ValorPropuesto))
                {
                    p.Estado = EstadoPropuesta.Rechazada;
                    ilegibles++;
                    continue;
                }

                p.Estado = EstadoPropuesta.Aprobada;
                aplicadas++;
            }

            await ctx.SaveChangesAsync(ct);
        }

        TempData["SuccessMsg"] = aprobar
            ? Mensaje(aplicadas, superadas, ilegibles)
            : $"{ids.Count} propuesta(s) rechazada(s). El campo se queda vacío y la regla no volverá a proponer ese mismo valor.";

        return Redirigir();
    }

    private static string Mensaje(int aplicadas, int superadas, int ilegibles)
    {
        var texto = $"{aplicadas} valor(es) escrito(s) en su ficha.";

        if (superadas > 0)
            texto += $" {superadas} se descartaron porque alguien ya había llenado el campo a mano.";

        if (ilegibles > 0)
            texto += $" {ilegibles} no se pudieron aplicar por tener un valor ilegible; quedan rechazadas.";

        return texto;
    }

    private IActionResult Redirigir() => RedirectToPage(new { Tab, Sigla, Campo, Certeza, Buscar, Pg });

    // ── Carga ─────────────────────────────────────────────────────────────────

    /// <summary>La consulta con los filtros de la pantalla aplicados. Vive en un solo lugar porque
    /// la tabla que se enseña y el botón que aprueba en bloque tienen que alcanzar exactamente las
    /// mismas filas: si divergieran, alguien aprobaría a ciegas un conjunto distinto del que vio.</summary>
    private IQueryable<PropuestaLlenado> Filtrada(bool Pendientes)
    {
        var q = ctx.PropuestasLlenado.AsNoTracking();

        q = Pendientes
            ? q.Where(p => p.Estado == EstadoPropuesta.Pendiente)
            : Tab switch
            {
                "aprobadas"  => q.Where(p => p.Estado == EstadoPropuesta.Aprobada),
                "rechazadas" => q.Where(p => p.Estado == EstadoPropuesta.Rechazada),
                _            => q.Where(p => p.Estado == EstadoPropuesta.Pendiente)
            };

        if (Enum.TryParse<CampoFicha>(Campo, out var campo))
            q = q.Where(p => p.Campo == campo);

        if (Enum.TryParse<CertezaLlenado>(Certeza, out var certeza))
            q = q.Where(p => p.Certeza == certeza);

        if (!string.IsNullOrWhiteSpace(Sigla))
        {
            var s = Sigla;
            q = q.Where(p => ctx.TramitesSiger.Any(t => t.Id == p.TramiteSigerId && t.Sigla == s));
        }

        if (!string.IsNullOrWhiteSpace(Buscar))
        {
            var b = Buscar.Trim();
            q = q.Where(p => ctx.TramitesSiger.Any(t => t.Id == p.TramiteSigerId
                && (t.Nombre.Contains(b) || t.Codigo.Contains(b) || t.Institucion.Contains(b))));
        }

        return q;
    }

    private async Task CargarAsync(CancellationToken ct)
    {
        var todas = ctx.PropuestasLlenado.AsNoTracking();

        Pendientes = await todas.CountAsync(p => p.Estado == EstadoPropuesta.Pendiente, ct);
        Aprobadas  = await todas.CountAsync(p => p.Estado == EstadoPropuesta.Aprobada, ct);
        Rechazadas = await todas.CountAsync(p => p.Estado == EstadoPropuesta.Rechazada, ct);

        HuecosSinPropuesta = await ContarHuecosSinPropuestaAsync(ct);

        Siglas = await ctx.TramitesSiger.AsNoTracking()
            .Where(t => t.Sigla != null && ctx.PropuestasLlenado.Any(p => p.TramiteSigerId == t.Id))
            .Select(t => t.Sigla!).Distinct().OrderBy(s => s).ToListAsync(ct);

        var q = Filtrada(Pendientes: false);

        AlcanceDelFiltro = Tab == "pendientes" ? await q.CountAsync(ct) : 0;

        var total = await q.CountAsync(ct);
        var (_, page, size) = Paginacion.Normalizar(null, Pg, TamanoPagina);
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        if (page > totalPaginas) page = totalPaginas;

        // La ficha se une acá y no con una propiedad de navegación porque la propuesta no tiene
        // una: la entidad guarda solo el id, y cargar la ficha entera para enseñar cuatro columnas
        // traería seis colecciones hijas que nadie va a mirar.
        var filas = await q
            .OrderBy(p => p.Certeza).ThenBy(p => p.Campo).ThenBy(p => p.Id)
            .Skip((page - 1) * size).Take(size)
            .Join(ctx.TramitesSiger.AsNoTracking(), p => p.TramiteSigerId, t => t.Id, (p, t) => new
            {
                p.Id, p.Campo, p.ValorPropuesto, p.Certeza, p.Justificacion, p.Estado,
                p.DecididaPor, p.DecididaEl,
                t.Codigo, t.Nombre, t.Institucion, t.Sigla
            })
            .ToListAsync(ct);

        var categorias = await ctx.CategoriasTramite.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Nombre, ct);

        var vm = filas.Select(f => new FilaVm(
            f.Id, f.Codigo, f.Nombre, f.Sigla ?? f.Institucion,
            ValorLlenado.Etiqueta(f.Campo),
            ValorLlenado.ParaMostrar(f.Campo, f.ValorPropuesto, categorias),
            f.Certeza, f.Justificacion, f.Estado, f.DecididaPor, f.DecididaEl)).ToList();

        Resultado = new PagedResult<FilaVm>(vm, total, page, size);
    }

    /// <summary>
    /// Huecos que siguen vacíos y para los que no hay ninguna propuesta viva. Se cuenta en la base
    /// campo por campo en vez de traerse las fichas: son mil y solo hace falta el número.
    /// </summary>
    private async Task<int> ContarHuecosSinPropuestaAsync(CancellationToken ct)
    {
        var fichas = ctx.TramitesSiger.AsNoTracking();
        var vivas  = ctx.PropuestasLlenado.AsNoTracking().Where(p => p.Estado == EstadoPropuesta.Pendiente);

        var sinCategoria = await fichas.CountAsync(t => t.CategoriaId == null
            && !vivas.Any(p => p.TramiteSigerId == t.Id && p.Campo == CampoFicha.Categoria), ct);
        var sinModalidad = await fichas.CountAsync(t => t.Modalidad == null
            && !vivas.Any(p => p.TramiteSigerId == t.Id && p.Campo == CampoFicha.Modalidad), ct);
        var sinTiempo = await fichas.CountAsync(t => t.TiempoTexto == null
            && !vivas.Any(p => p.TramiteSigerId == t.Id && p.Campo == CampoFicha.Tiempo), ct);
        var sinCosto = await fichas.CountAsync(t => t.CostoEsGratuito == null
            && !vivas.Any(p => p.TramiteSigerId == t.Id && p.Campo == CampoFicha.Costo), ct);

        return sinCategoria + sinModalidad + sinTiempo + sinCosto;
    }

    // ── Modelo de vista ───────────────────────────────────────────────────────

    public sealed record FilaVm(
        int             Id,
        string          Codigo,
        string          Nombre,
        string          Institucion,
        string          Campo,
        string          Valor,
        CertezaLlenado  Certeza,
        string          Justificacion,
        EstadoPropuesta Estado,
        string?         DecididaPor,
        DateTime?       DecididaEl)
    {
        public string ColorCerteza => Certeza switch
        {
            CertezaLlenado.Alta  => "#0f5132",
            CertezaLlenado.Media => "#b45309",
            _                    => "#9f1239"
        };

        public string FondoCerteza => Certeza switch
        {
            CertezaLlenado.Alta  => "#d1e7dd",
            CertezaLlenado.Media => "#fef3c7",
            _                    => "#ffe4e6"
        };
    }
}
