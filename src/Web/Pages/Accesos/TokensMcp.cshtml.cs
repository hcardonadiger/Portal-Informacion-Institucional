using Diger.TramitesEstado.Application.TokensMcp;

namespace Diger.TramitesEstado.Web.Pages.Accesos;

/// <summary>
/// Vista global de los tokens de acceso MCP del portal.
///
/// Es un módulo aparte de <c>TokensMcp</c> —el autoservicio— y no un permiso más de aquel,
/// porque son privilegios distintos: administrar los propios es rutina, ver y revocar los de
/// cualquiera es una potestad de administración. Y hace falta: ante un token filtrado o un
/// funcionario que ya no está, alguien tiene que poder cortarlo sin la sesión del dueño.
/// </summary>
[Permission("TokensMcp.Admin", AccionModulo.Ver, "Ver los tokens de acceso MCP de todo el portal")]
public sealed class TokensMcpAdminModel(ISender sender) : PageModel
{
    public IReadOnlyList<TokenMcpAdminDto> Tokens { get; private set; } = [];

    [TempData] public string? Aviso { get; set; }
    [TempData] public string? Error { get; set; }

    public async Task OnGetAsync(CancellationToken ct) =>
        Tokens = await sender.Send(new GetTokensMcpAdminQuery(), ct);

    [Permission("TokensMcp.Admin", AccionModulo.Eliminar, "Revocar tokens de acceso MCP de cualquier usuario")]
    public async Task<IActionResult> OnPostRevocarAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new RevocarTokenMcpComoAdminCommand(id), ct);
            Aviso = "Token revocado. Deja de funcionar en la próxima petición.";
        }
        catch (Exception ex) when (ex is DomainException or NotFoundException)
        {
            Error = ex.Message;
        }

        return RedirectToPage();
    }
}
