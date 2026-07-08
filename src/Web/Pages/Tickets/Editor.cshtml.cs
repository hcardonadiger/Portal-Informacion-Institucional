using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Web.Pages.Tickets;

[Authorize(Policy = "PuedeGestionarTickets")]
public sealed class EditorModel(ISender sender, IInstitucionRepository institucionRepo, ICurrentUserService currentUser, IWebHostEnvironment env) : PageModel
{
    public int? TicketId { get; private set; }
    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];
    public IReadOnlyList<ExpedienteListItemDto> Expedientes { get; private set; } = [];
    public IReadOnlyList<TramiteOpcion> Tramites { get; private set; } = [];
    public IReadOnlyList<TemaOpcionDto> Temas { get; private set; } = [];

    public sealed record TramiteOpcion(int Id, string Nombre, int InstitucionId);

    [BindProperty] public TicketFormDto Datos { get; set; } = new();
    [BindProperty] public List<IFormFile> Archivos { get; set; } = [];
    public string? Error { get; set; }

    // "Reportado por" es informativo (no editable): al crear = usuario actual; al editar = el reportante existente.
    public string? UsuarioActualNombre => currentUser.Nombre ?? currentUser.Correo;
    public string? UsuarioActualCorreo => currentUser.Correo;
    public string? ReportanteView { get; private set; }

    public PrioridadTicket[] Prioridades => Enum.GetValues<PrioridadTicket>();

    private async Task CargarCatalogosAsync(CancellationToken ct)
    {
        var insts = await institucionRepo.GetAllActivasAsync(ct);
        Instituciones = currentUser.EsGlobal
            ? insts
            : insts.Where(i => currentUser.InstitucionesAsignadas.Contains(i.Id)).ToList();
        // Lista completa para el selector dependiente (ya viene filtrado por alcance)
        Expedientes   = (await sender.Send(new GetExpedientesQuery(Todos: true), ct)).Items;
        Temas         = await sender.Send(new GetTemasActivosQuery(), ct);

        // Trámites del catálogo, solo de las instituciones dentro del alcance.
        var scopeIds = Instituciones.Select(i => i.Id).ToHashSet();
        Tramites = (await institucionRepo.GetAllTramitesAsync(ct))
            .Where(t => scopeIds.Contains(t.InstitucionId))
            .Select(t => new TramiteOpcion(t.Id, t.Nombre, t.InstitucionId))
            .ToList();
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
                Titulo = d.Titulo, Descripcion = d.Descripcion, TemaId = d.TemaId, TemaOtro = d.TemaOtro, Prioridad = d.Prioridad,
                InstitucionId = d.InstitucionId, ExpedienteId = d.ExpedienteId
            };
            ReportanteView = string.Join(" · ",
                new[] { d.ReportanteNombre, d.ReportanteCorreo }.Where(s => !string.IsNullOrWhiteSpace(s)));
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
            {
                var adjuntos = await AdjuntoStorage.GuardarAsync(Archivos, env, ct);
                destinoId = await sender.Send(new CrearTicketCommand(Datos, adjuntos), ct);
            }
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
