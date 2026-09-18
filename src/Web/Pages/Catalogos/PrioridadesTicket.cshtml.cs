using Diger.TramitesEstado.Application.Tickets.Prioridades;

namespace Diger.TramitesEstado.Web.Pages.Catalogos;

/// <summary>
/// CRUD del catálogo de prioridades de ticket. Hermano del de proyectos, con una casilla más:
/// «cuenta como crítica», que es lo que alimenta el indicador de tickets críticos de los
/// tableros. Antes esa condición miraba el miembro <c>Critica</c> del enum; con el nombre
/// editable hace falta declararla.
/// </summary>
[Permission("Prioridades.Tickets", AccionModulo.Editar, "Administrar prioridades de tickets")]
[Authorize(Policy = "Prioridades.Tickets.Editar")]
public sealed class PrioridadesTicketModel(ISender sender) : PageModel
{
    public IReadOnlyList<PrioridadTicketDto> Prioridades { get; private set; } = [];
    public string? Error { get; set; }

    [BindProperty] public string        Nombre    { get; set; } = string.Empty;
    [BindProperty] public int           Orden     { get; set; }
    [BindProperty] public ColorEtiqueta Color     { get; set; } = ColorEtiqueta.Gris;
    [BindProperty] public bool          EsCritica { get; set; }

    private async Task CargarAsync(CancellationToken ct) =>
        Prioridades = await sender.Send(new GetPrioridadesTicketQuery(), ct);

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    public async Task<IActionResult> OnPostCrearAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Nombre))
        {
            Error = "El nombre de la prioridad es obligatorio.";
            await CargarAsync(ct);
            return Page();
        }

        try
        {
            // Orden 0 significa «no me importa»: se va al final en vez de empatar con la primera.
            var orden = Orden > 0 ? Orden : Prioridades.Count + 1;
            await sender.Send(new CrearPrioridadTicketCommand(Nombre, orden, Color, EsCritica), ct);
            TempData["SuccessMsg"] = "Prioridad creada.";
            return RedirectToPage();
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
            await CargarAsync(ct);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostActualizarAsync(
        int id, string nombre, int orden, ColorEtiqueta color, bool esCritica, bool activo, CancellationToken ct)
    {
        try
        {
            await sender.Send(
                new ActualizarPrioridadTicketCommand(id, nombre ?? "", orden, color, esCritica, activo), ct);
            TempData["SuccessMsg"] = "Prioridad actualizada.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPredeterminadaAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new MarcarPrioridadTicketPredeterminadaCommand(id), ct);
            TempData["SuccessMsg"] = "Prioridad predeterminada actualizada.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new EliminarPrioridadTicketCommand(id), ct);
            TempData["SuccessMsg"] = "Prioridad eliminada.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        return RedirectToPage();
    }
}
