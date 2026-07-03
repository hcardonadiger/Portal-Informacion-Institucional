using Microsoft.AspNetCore.Authorization;

namespace Diger.TramitesEstado.Web.Pages.Cuenta;

[AllowAnonymous]
public sealed class AccessDeniedModel : PageModel
{
    public void OnGet() { }
}
