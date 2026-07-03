namespace Diger.TramitesEstado.Web.Pages.Usuarios;

[Authorize(Policy = "PuedeAdministrarUsuarios")]
public sealed class IndexModel(ISender sender) : PageModel
{
    public PagedResult<UsuarioListItemDto> Resultado { get; private set; } = PagedResult<UsuarioListItemDto>.Empty(15);
    public string? Q { get; private set; }

    public async Task OnGetAsync(string? q, int? page, CancellationToken ct)
    {
        Q = q;
        Resultado = await sender.Send(new GetUsuariosQuery(q, page), ct);
    }
}
