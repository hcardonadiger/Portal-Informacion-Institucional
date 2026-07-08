namespace Diger.TramitesEstado.Web.Pages.Usuarios;

[Authorize(Policy = "PuedeAdministrarUsuarios")]
public sealed class IndexModel(ISender sender) : PageModel
{
    public PagedResult<UsuarioListItemDto> Resultado { get; private set; } = PagedResult<UsuarioListItemDto>.Empty(Paginacion.TamanoDefecto);
    public string? Q { get; private set; }

    public async Task OnGetAsync(string? q, int? pg, CancellationToken ct)
    {
        Q = q;
        Resultado = await sender.Send(new GetUsuariosQuery(q, pg), ct);
    }
}
