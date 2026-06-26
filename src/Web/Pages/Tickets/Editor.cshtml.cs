using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Web.Pages.Tickets;

[Authorize(Policy = "PuedeGestionarTickets")]
public sealed class EditorModel(ISender sender, IInstitucionRepository institucionRepo, ICurrentUserService currentUser) : PageModel
{
    public int? TicketId { get; private set; }
    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];
    public IReadOnlyList<ExpedienteListItemDto> Expedientes { get; private set; } = [];

    [BindProperty] public TicketFormDto Datos { get; set; } = new();
    public string? Error { get; set; }

    public CategoriaTicket[] Categorias => Enum.GetValues<CategoriaTicket>();
    public PrioridadTicket[] Prioridades => Enum.GetValues<PrioridadTicket>();

    public static string CatLabel(CategoriaTicket c) => c switch
    {
        CategoriaTicket.ErrorPlataforma => "Error en plataforma",
        CategoriaTicket.Configuracion   => "Configuración",
        CategoriaTicket.Capacitacion    => "Capacitación",
        _ => c.ToString()
    };

    private async Task CargarCatalogosAsync(CancellationToken ct)
    {
        var insts = await institucionRepo.GetAllActivasAsync(ct);
        Instituciones = currentUser.EsGlobal
            ? insts
            : insts.Where(i => currentUser.InstitucionesAsignadas.Contains(i.Id)).ToList();
        Expedientes   = await sender.Send(new GetExpedientesQuery(), ct); // ya viene filtrado por alcance
    }

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken ct)
    {
        await CargarCatalogosAsync(ct);
        if (id is null) return Page();

        try
        {
            var d = await sender.Send(new GetTicketByIdQuery(id.Value), ct);
            TicketId = d.Id;
            Datos = new TicketFormDto
            {
                Titulo = d.Titulo, Descripcion = d.Descripcion, Categoria = d.Categoria, Prioridad = d.Prioridad,
                InstitucionId = d.InstitucionId, ExpedienteId = d.ExpedienteId,
                ReportanteNombre = d.ReportanteNombre, ReportanteCorreo = d.ReportanteCorreo, ReportanteTelefono = d.ReportanteTelefono
            };
            return Page();
        }
        catch (NotFoundException) { return NotFound(); }
    }

    public async Task<IActionResult> OnPostAsync(int? id, CancellationToken ct)
    {
        TicketId = id;
        await CargarCatalogosAsync(ct);

        if (string.IsNullOrWhiteSpace(Datos.Titulo))
        {
            Error = "El título del ticket es obligatorio.";
            return Page();
        }

        try
        {
            int destinoId;
            if (id is null)
                destinoId = await sender.Send(new CrearTicketCommand(Datos), ct);
            else
            {
                await sender.Send(new ActualizarTicketCommand(id.Value, Datos), ct);
                destinoId = id.Value;
            }
            TempData["SuccessMsg"] = id is null ? "Ticket creado." : "Ticket actualizado.";
            return RedirectToPage("/Tickets/Detalle", new { id = destinoId });
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
            return Page();
        }
    }
}
