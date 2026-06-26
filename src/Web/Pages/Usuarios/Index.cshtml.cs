namespace Diger.TramitesEstado.Web.Pages.Usuarios;

[Authorize(Policy = "PuedeAdministrarUsuarios")]
public sealed class IndexModel(ISender sender) : PageModel
{
    public IReadOnlyList<UsuarioListItemDto> Usuarios { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Usuarios = await sender.Send(new GetUsuariosQuery(), ct);
    }
}
