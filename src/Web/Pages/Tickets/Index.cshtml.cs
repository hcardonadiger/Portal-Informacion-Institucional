namespace Diger.TramitesEstado.Web.Pages.Tickets;

[Authorize]
[Permission("Tickets", AccionModulo.Ver, "Ver tickets")]
public sealed class IndexModel(ISender sender, IInstitucionRepository institucionRepo, IUsuarioRepository usuarioRepo, ICurrentUserService currentUser) : PageModel
{
    public PagedResult<TicketListItemDto> Resultado { get; private set; } = PagedResult<TicketListItemDto>.Empty(Paginacion.TamanoDefecto);
    public IReadOnlyList<TicketListItemDto> Todos { get; private set; } = [];
    public IReadOnlyList<TicketListItemDto> TodosParaKpis { get; private set; } = [];
    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];

    public EstadoTicket?    Estado    { get; private set; }
    public PrioridadTicket? Prioridad { get; private set; }
    public string?             InstitucionId { get; private set; }
    public bool             Mias      { get; private set; }
    public bool             MisTemas  { get; private set; }
    public bool             SoloVencidos { get; private set; }
    public bool             SinTemas  { get; private set; } // el usuario no tiene temas asignados
    public string?          Q         { get; private set; }

    // El técnico (sin rol de gestión superior) no ve la lista completa: solo alterna entre
    // "Sus temas" (para tomar) y "Sus tickets" (para verificar estado).
    public bool             EsTecnicoRestringido { get; private set; }
    public string           Vista     { get; private set; } = "temas"; // "temas" | "mios"

    public async Task OnGetAsync(EstadoTicket? estado, PrioridadTicket? prioridad, string? institucionId, bool mias, bool misTemas, bool soloVencidos, string? vista, string? q, int? pg, CancellationToken ct)
    {
        Estado = estado; Prioridad = prioridad; InstitucionId = institucionId; SoloVencidos = soloVencidos; Q = q;
        Instituciones = await institucionRepo.GetAllActivasAsync(ct);

        // "No es jefatura" ⇒ alcance restringido. Lo decide la capacidad EsSupervisor del
        // rol (tabla Roles), no una lista de nombres — así un rol nuevo entra en la
        // categoría correcta sin tocar código.
        EsTecnicoRestringido = !currentUser.EsGlobal && !currentUser.EsSupervisor;

        Guid? asignado;
        IReadOnlyList<int>? temaIds = null;

        if (EsTecnicoRestringido && currentUser.UserId is Guid tid)
        {
            // Dos vistas exclusivas; sin acceso a "todos".
            Vista = vista == "mios" ? "mios" : "temas";
            if (Vista == "mios")
            {
                asignado = tid;                 // Sus tickets: asignados a él
            }
            else
            {
                temaIds = await usuarioRepo.GetTemaIdsAsync(tid, ct);   // Sus temas: para tomar
                SinTemas = temaIds.Count == 0;
                asignado = null;
            }
            Mias = Vista == "mios"; MisTemas = Vista == "temas";
        }
        else
        {
            // Admin/Coordinador: filtros opcionales, con acceso a todo su alcance institucional.
            Mias = mias; MisTemas = misTemas;
            asignado = mias ? currentUser.UserId : null;
            if (misTemas && currentUser.UserId is Guid uid)
            {
                temaIds = await usuarioRepo.GetTemaIdsAsync(uid, ct);
                SinTemas = temaIds.Count == 0;
            }
        }

        Resultado = await sender.Send(
            new GetTicketsQuery(estado, prioridad, institucionId, asignado, q, pg, TemaIds: temaIds, SoloVencidos: soloVencidos), ct);
        Todos = (await sender.Send(
            new GetTicketsQuery(estado, prioridad, institucionId, asignado, q, Page: 1, Size: 100, TemaIds: temaIds, SoloVencidos: soloVencidos), ct)).Items;
        TodosParaKpis = (await sender.Send(
            new GetTicketsQuery(null, prioridad, institucionId, asignado, q, Page: 1, Size: 100, TemaIds: temaIds, SoloVencidos: false), ct)).Items;
    }

    [Permission("Tickets", AccionModulo.Eliminar, "Eliminar tickets")]
    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        // El chequeo de rol por nombre que había acá lo sustituye el [Permission] de arriba.
        await sender.Send(new EliminarTicketCommand(id), ct);
        TempData["SuccessMsg"] = "Ticket eliminado.";
        return RedirectToPage();
    }
}
