namespace Diger.TramitesEstado.Web.Pages.Tickets;

[Authorize]
public sealed class IndexModel(ISender sender, IInstitucionRepository institucionRepo, IUsuarioRepository usuarioRepo, ICurrentUserService currentUser) : PageModel
{
    public PagedResult<TicketListItemDto> Resultado { get; private set; } = PagedResult<TicketListItemDto>.Empty(15);
    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];

    public EstadoTicket?    Estado    { get; private set; }
    public PrioridadTicket? Prioridad { get; private set; }
    public int?             InstitucionId { get; private set; }
    public bool             Mias      { get; private set; }
    public bool             MisTemas  { get; private set; }
    public bool             SoloVencidos { get; private set; }
    public bool             SinTemas  { get; private set; } // el usuario no tiene temas asignados
    public string?          Q         { get; private set; }

    // El técnico (sin rol de gestión superior) no ve la lista completa: solo alterna entre
    // "Sus temas" (para tomar) y "Sus tickets" (para verificar estado).
    public bool             EsTecnicoRestringido { get; private set; }
    public string           Vista     { get; private set; } = "temas"; // "temas" | "mios"

    public async Task OnGetAsync(EstadoTicket? estado, PrioridadTicket? prioridad, int? institucionId, bool mias, bool misTemas, bool soloVencidos, string? vista, string? q, int? page, CancellationToken ct)
    {
        Estado = estado; Prioridad = prioridad; InstitucionId = institucionId; SoloVencidos = soloVencidos; Q = q;
        Instituciones = await institucionRepo.GetAllActivasAsync(ct);

        EsTecnicoRestringido =
            User.IsInRole(nameof(RolUsuario.Tecnico))
            && !User.IsInRole(nameof(RolUsuario.Administrador))
            && !User.IsInRole(nameof(RolUsuario.Coordinador));

        int? asignado;
        IReadOnlyList<int>? temaIds = null;

        if (EsTecnicoRestringido && currentUser.UserId is int tid)
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
            if (misTemas && currentUser.UserId is int uid)
            {
                temaIds = await usuarioRepo.GetTemaIdsAsync(uid, ct);
                SinTemas = temaIds.Count == 0;
            }
        }

        Resultado = await sender.Send(
            new GetTicketsQuery(estado, prioridad, institucionId, asignado, q, page, TemaIds: temaIds, SoloVencidos: soloVencidos), ct);
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        if (!User.IsInRole(nameof(RolUsuario.Administrador)) && !User.IsInRole(nameof(RolUsuario.Coordinador)))
            return Forbid();
        await sender.Send(new EliminarTicketCommand(id), ct);
        TempData["SuccessMsg"] = "Ticket eliminado.";
        return RedirectToPage();
    }
}
