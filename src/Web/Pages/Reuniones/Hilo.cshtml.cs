namespace Diger.TramitesEstado.Web.Pages.Reuniones;

[Authorize]
public sealed class HiloModel(ISender sender) : PageModel
{
    public HiloDetalleDto Hilo { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        try
        {
            Hilo = await sender.Send(new GetHiloQuery(id), ct);
            return Page();
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}
