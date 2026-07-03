namespace Diger.TramitesEstado.Web.Pages.Tableros;

[Authorize]
public sealed class TicketsModel(ISender sender, IInstitucionRepository institucionRepo, IUsuarioRepository usuarioRepo, ICurrentUserService currentUser) : PageModel
{
    public TicketsDashboardDto Data { get; private set; } = default!;
    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];
    public int? InstitucionId { get; private set; }
    public DateOnly? Desde { get; private set; }
    public DateOnly? Hasta { get; private set; }
    // El técnico ve el tablero limitado a sus temas y tickets asignados.
    public bool AlcanceTecnico { get; private set; }

    public async Task OnGetAsync(int? institucionId, DateOnly? desde, DateOnly? hasta, CancellationToken ct)
    {
        InstitucionId = institucionId; Desde = desde; Hasta = hasta;
        var insts = await institucionRepo.GetAllActivasAsync(ct);
        Instituciones = currentUser.EsGlobal ? insts : insts.Where(i => currentUser.InstitucionesAsignadas.Contains(i.Id)).ToList();

        // Alcance del técnico: solo sus temas o sus tickets (mismo criterio que la lista y el detalle).
        AlcanceTecnico =
            User.IsInRole(nameof(RolUsuario.Tecnico))
            && !User.IsInRole(nameof(RolUsuario.Administrador))
            && !User.IsInRole(nameof(RolUsuario.Coordinador));

        IReadOnlyList<int>? temaIds = null;
        int? tecnicoId = null;
        if (AlcanceTecnico && currentUser.UserId is int uid)
        {
            temaIds = await usuarioRepo.GetTemaIdsAsync(uid, ct);
            tecnicoId = uid;
        }

        Data = await sender.Send(new GetTicketsDashboardQuery(institucionId, desde, hasta, temaIds, tecnicoId), ct);
    }
}
