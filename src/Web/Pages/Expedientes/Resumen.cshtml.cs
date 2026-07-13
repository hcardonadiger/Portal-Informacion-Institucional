namespace Diger.TramitesEstado.Web.Pages.Expedientes;

[Authorize]
public sealed class ResumenModel(ISender sender) : PageModel
{
    public int ExpedienteId { get; private set; }
    public string Codigo { get; private set; } = "";
    public ExpedienteInputDto Datos { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        try
        {
            var d = await sender.Send(new GetExpedienteByIdQuery(id), ct);
            ExpedienteId = d.Id;
            Codigo = d.Codigo;
            Datos = d.Datos;
            return Page();
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}
