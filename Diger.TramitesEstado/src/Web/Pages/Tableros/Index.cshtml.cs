namespace Diger.TramitesEstado.Web.Pages.Tableros;

[Authorize]
public sealed class IndexModel(ISender sender, IUsuarioRepository usuarioRepo, ICurrentUserService currentUser) : PageModel
{
    public ResumenDto Data { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken ct)
    {
        // El técnico ve los KPIs de tickets limitados a sus temas o sus tickets asignados.
        var esTecnico =
            User.IsInRole(nameof(RolUsuario.Tecnico))
            && !User.IsInRole(nameof(RolUsuario.Administrador))
            && !User.IsInRole(nameof(RolUsuario.Coordinador));

        IReadOnlyList<int>? temaIds = null;
        int? tecnicoId = null;
        if (esTecnico && currentUser.UserId is int uid)
        {
            temaIds = await usuarioRepo.GetTemaIdsAsync(uid, ct);
            tecnicoId = uid;
        }

        Data = await sender.Send(new GetResumenQuery(temaIds, tecnicoId), ct);
    }
}
