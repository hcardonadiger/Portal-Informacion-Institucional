namespace Diger.TramitesEstado.Web.Pages;

// Ya está exenta por convención (AllowAnonymousToPage("/Error")); el atributo lo hace
// explícito para quien lea la clase y para el descubrimiento por reflexión.
[AllowAnonymous]
public sealed class ErrorModel : PageModel
{
    public int    Code    { get; private set; } = 500;
    public string Message { get; private set; } = "Ocurrió un error inesperado.";

    public void OnGet(int? code, string? msg)
    {
        Code    = code ?? 500;
        Message = string.IsNullOrWhiteSpace(msg) ? Message : msg;
    }
}
