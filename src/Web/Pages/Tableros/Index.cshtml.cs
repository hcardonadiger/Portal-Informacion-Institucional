namespace Diger.TramitesEstado.Web.Pages.Tableros;

[Authorize]
[Permission("Tableros", AccionModulo.Ver, "Ver tableros")]
public sealed class IndexModel(ISender sender, IUsuarioRepository usuarioRepo, ICurrentUserService currentUser) : PageModel
{
    public ResumenDto Data { get; private set; } = default!;
    public DateOnly? Desde { get; private set; }
    public DateOnly? Hasta { get; private set; }
    public EstadoTramite? Estado { get; private set; }

    public async Task OnGetAsync(DateOnly? desde, DateOnly? hasta, string? estado, CancellationToken ct)
    {
        Desde = desde;
        Hasta = hasta;
        Estado = Enum.TryParse<EstadoTramite>(estado, ignoreCase: true, out var est) ? est : null;

        // El técnico ve los KPIs de tickets limitados a sus temas o sus tickets asignados.
        // "No es jefatura" lo decide la capacidad EsSupervisor del rol (tabla Roles).
        var esTecnico = !currentUser.EsGlobal && !currentUser.EsSupervisor;

        IReadOnlyList<int>? temaIds = null;
        Guid? tecnicoId = null;
        if (esTecnico && currentUser.UserId is Guid uid)
        {
            temaIds = await usuarioRepo.GetTemaIdsAsync(uid, ct);
            tecnicoId = uid;
        }

        Data = await sender.Send(new GetResumenQuery(temaIds, tecnicoId, Desde, Hasta, Estado), ct);
    }
}
