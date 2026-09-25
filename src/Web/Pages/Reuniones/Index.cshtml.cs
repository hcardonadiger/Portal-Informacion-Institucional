namespace Diger.TramitesEstado.Web.Pages.Reuniones;

[Authorize]
[Permission("Reuniones", AccionModulo.Ver, "Ver reuniones y compromisos")]
public sealed class IndexModel(ISender sender) : PageModel
{
    public PagedResult<ReunionListItemDto> Resultado { get; private set; } = PagedResult<ReunionListItemDto>.Empty(Paginacion.TamanoDefecto);
    public IReadOnlyList<ReunionListItemDto> Todas { get; private set; } = [];
    public OpcionesFiltroReuniones Opciones { get; private set; } = new([], []);

    public string? Q { get; private set; }
    public string? FTitulo { get; private set; }
    public string? FInstitucion { get; private set; }
    public string? FTipo { get; private set; }
    public DateOnly? FDesde { get; private set; }
    public DateOnly? FHasta { get; private set; }
    public DateOnly? FCreadaDesde { get; private set; }
    public DateOnly? FCreadaHasta { get; private set; }
    public OrdenReuniones Orden { get; private set; } = OrdenReuniones.Creacion;
    public bool Desc { get; private set; } = true;

    /// <summary>Hay filtros aplicados (la búsqueda general no cuenta: tiene su propio "Limpiar").</summary>
    public bool HayFiltros => FTitulo is not null || FInstitucion is not null || FTipo is not null
                              || FDesde is not null || FHasta is not null
                              || FCreadaDesde is not null || FCreadaHasta is not null;

    public async Task OnGetAsync(string? q, int? pg, string? titulo, string? institucion, string? tipo,
                                 DateOnly? desde, DateOnly? hasta, DateOnly? creadaDesde, DateOnly? creadaHasta,
                                 string? orden, bool? desc, CancellationToken ct)
    {
        Q = Limpiar(q);
        FTitulo = Limpiar(titulo);
        FInstitucion = Limpiar(institucion);
        FTipo = Limpiar(tipo);
        FDesde = desde;
        FHasta = hasta;
        FCreadaDesde = creadaDesde;
        FCreadaHasta = creadaHasta;
        // Un valor de orden que no reconocemos cae al de siempre en vez de romper la página:
        // el querystring lo escribe cualquiera.
        Orden = Enum.TryParse<OrdenReuniones>(orden, ignoreCase: true, out var o) ? o : OrdenReuniones.Creacion;
        Desc = desc ?? true;

        var filtros = new GetReunionesQuery(Q, pg, null, FTitulo, FInstitucion, FTipo,
                                            FDesde, FHasta, FCreadaDesde, FCreadaHasta, Orden, Desc);
        Resultado = await sender.Send(filtros, ct);
        Todas = (await sender.Send(filtros with { Page = 1, Size = 100 }, ct)).Items;
        Opciones = await sender.Send(new GetOpcionesFiltroReunionesQuery(), ct);
    }

    private static string? Limpiar(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    [Permission("Reuniones", AccionModulo.Eliminar, "Eliminar reuniones")]
    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        // El chequeo de rol por nombre que había acá lo sustituye el [Permission] de arriba.
        await sender.Send(new EliminarReunionCommand(id), ct);
        TempData["SuccessMsg"] = "Reunión eliminada.";
        return RedirectToPage();
    }
}
