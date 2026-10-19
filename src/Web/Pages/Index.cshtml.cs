namespace Diger.TramitesEstado.Web.Pages;

// La raíz del portal es la lista de expedientes.
[Permission("Expedientes", AccionModulo.Ver, "Ver expedientes")]
public sealed class IndexModel(ISender sender) : PageModel
{
    public PagedResult<ExpedienteListItemDto> Resultado { get; private set; } = PagedResult<ExpedienteListItemDto>.Empty(Paginacion.TamanoDefecto);
    public IReadOnlyList<ExpedienteListItemDto> Todos { get; private set; } = [];
    public string? Q { get; private set; }
    public bool? Legado { get; private set; }

    /// <summary>
    /// Cuántos expedientes hay en cada filtro, para mostrarlo en los chips.
    /// Un filtro que no dice cuánto trae obliga a pulsarlo para averiguarlo.
    /// Respetan la búsqueda actual: si hay texto, el conteo es de lo que esa
    /// búsqueda devolvería en cada filtro, no del total del sistema.
    /// </summary>
    public int ConteoTodos { get; private set; }
    public int ConteoNuevos { get; private set; }
    public int ConteoLegados { get; private set; }

    /// <summary>
    /// Los cuatro filtros de la lista. Hasta el 2026-10-19 esta pantalla solo
    /// tenía la búsqueda libre y un legado oculto —dos controles contra los
    /// siete de Proyectos—, y por eso se veía vacía: un listado de 35
    /// expedientes sin manera de acotarlo obliga a leerlo entero.
    ///
    /// Institución y Encargado son los dos ejes por los que de verdad se
    /// reparte el trabajo; Estado es la fase, que contesta "¿qué me falta?";
    /// y Acción es lo que se decidió hacer con los trámites, que es el
    /// resultado del expediente.
    /// </summary>
    [BindProperty(SupportsGet = true)] public string? Institucion { get; set; }
    [BindProperty(SupportsGet = true)] public string? Encargado { get; set; }
    [BindProperty(SupportsGet = true)] public EstadoExpediente? Estado { get; set; }
    [BindProperty(SupportsGet = true)] public string? Accion { get; set; }

    /// <summary>Opciones de los selectores: el universo completo, no la página.</summary>
    public OpcionesExpedientesDto Opciones { get; private set; } = new([], [], []);

    /// <summary>Si hay algún filtro puesto además de la búsqueda y el legado.</summary>
    public bool HayFiltros =>
        !string.IsNullOrWhiteSpace(Institucion) || !string.IsNullOrWhiteSpace(Encargado)
        || Estado is not null || !string.IsNullOrWhiteSpace(Accion);

    public async Task OnGetAsync(string? q, int? pg, bool? legado, CancellationToken ct)
    {
        Q = q;
        Legado = legado;

        var filtros = new GetExpedientesQuery(
            q, pg, Legado: legado,
            Institucion: Institucion, Analista: Encargado, Estado: Estado, Accion: Accion);

        Resultado = await sender.Send(filtros, ct);
        Todos = (await sender.Send(filtros with { Page = 1, Size = 100 }, ct)).Items;
        Opciones = await sender.Send(new GetOpcionesExpedientesQuery(), ct);

        // Size: 1 porque solo interesa el Total; no se traen las filas.
        // Los chips cuentan sobre los filtros puestos: si no, dirían "35" con
        // tres expedientes en pantalla y el número no significaría nada.
        var conteo = filtros with { Page = 1, Size = 1 };
        ConteoTodos   = (await sender.Send(conteo with { Legado = null }, ct)).Total;
        ConteoNuevos  = (await sender.Send(conteo with { Legado = false }, ct)).Total;
        ConteoLegados = (await sender.Send(conteo with { Legado = true }, ct)).Total;
    }

    [Permission("Expedientes", AccionModulo.Eliminar, "Eliminar expedientes")]
    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        // El chequeo de rol por nombre que había acá lo sustituye el [Permission] de arriba.
        await sender.Send(new EliminarExpedienteCommand(id), ct);
        return RedirectToPage();
    }
}
