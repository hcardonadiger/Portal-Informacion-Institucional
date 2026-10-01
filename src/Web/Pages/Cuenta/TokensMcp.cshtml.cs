using Diger.TramitesEstado.Application.TokensMcp;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Diger.TramitesEstado.Web.Pages.Cuenta;

/// <summary>
/// Autoservicio de tokens de acceso MCP: el usuario emite y revoca los suyos.
///
/// A diferencia del perfil o la contraseña, esto NO lleva <c>[PermisoNoRequerido]</c>: un
/// token deja que un agente externo actúe en el portal con la identidad de quien lo emitió,
/// así que quién puede emitirlo se decide en la matriz de Accesos, usuario por usuario.
/// </summary>
// [Authorize] explícito: la carpeta /Cuenta está exenta por convención (AllowAnonymousToFolder,
// necesario para login/logout/recuperación).
[Authorize]
[Permission("TokensMcp", AccionModulo.Ver, "Ver y administrar sus tokens de acceso MCP")]
public sealed class TokensMcpModel(ISender sender) : PageModel
{
    [BindProperty] public string  Nombre        { get; set; } = string.Empty;
    [BindProperty] public Guid?   AsignacionId  { get; set; }
    [BindProperty] public int?    DiasVigencia  { get; set; } = 90;

    public IReadOnlyList<TokenMcpDto>       Tokens       { get; private set; } = [];
    public IReadOnlyList<OpcionAsignacionDto> Asignaciones { get; private set; } = [];

    /// <summary>El token recién emitido. Viaja por TempData y se muestra una sola vez:
    /// de la base solo vuelve a salir el prefijo.</summary>
    [TempData] public string? TokenNuevo { get; set; }
    [TempData] public string? Error      { get; set; }
    [TempData] public string? Aviso      { get; set; }

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    [Permission("TokensMcp", AccionModulo.Crear, "Generar tokens de acceso MCP")]
    public async Task<IActionResult> OnPostGenerarAsync(CancellationToken ct)
    {
        try
        {
            var generado = await sender.Send(new GenerarTokenMcpCommand(Nombre, AsignacionId, DiasVigencia), ct);
            TokenNuevo = generado.Token;
            Aviso      = "Token generado. Cópielo ahora: no se vuelve a mostrar.";
        }
        catch (Exception ex) when (ex is DomainException or FluentValidation.ValidationException)
        {
            Error = ex.Message;
        }

        return RedirectToPage();
    }

    [Permission("TokensMcp", AccionModulo.Eliminar, "Revocar sus tokens de acceso MCP")]
    public async Task<IActionResult> OnPostRevocarAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new RevocarTokenMcpCommand(id), ct);
            Aviso = "Token revocado. Deja de funcionar en la próxima petición.";
        }
        catch (Exception ex) when (ex is DomainException or NotFoundException)
        {
            Error = ex.Message;
        }

        return RedirectToPage();
    }

    private async Task CargarAsync(CancellationToken ct)
    {
        Tokens       = await sender.Send(new GetMisTokensMcpQuery(), ct);
        Asignaciones = await sender.Send(new GetMisAsignacionesQuery(), ct);
    }
}
