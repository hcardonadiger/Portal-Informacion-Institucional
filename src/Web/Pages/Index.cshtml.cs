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

    public async Task OnGetAsync(string? q, int? pg, bool? legado, CancellationToken ct)
    {
        Q = q;
        Legado = legado;
        Resultado = await sender.Send(new GetExpedientesQuery(q, pg, Legado: legado), ct);
        Todos = (await sender.Send(new GetExpedientesQuery(q, Page: 1, Size: 100, Legado: legado), ct)).Items;

        // Size: 1 porque solo interesa el Total; no se traen las filas.
        ConteoTodos   = (await sender.Send(new GetExpedientesQuery(q, Page: 1, Size: 1), ct)).Total;
        ConteoNuevos  = (await sender.Send(new GetExpedientesQuery(q, Page: 1, Size: 1, Legado: false), ct)).Total;
        ConteoLegados = (await sender.Send(new GetExpedientesQuery(q, Page: 1, Size: 1, Legado: true), ct)).Total;
    }

    [Permission("Expedientes", AccionModulo.Eliminar, "Eliminar expedientes")]
    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        // El chequeo de rol por nombre que había acá lo sustituye el [Permission] de arriba.
        await sender.Send(new EliminarExpedienteCommand(id), ct);
        return RedirectToPage();
    }
}
