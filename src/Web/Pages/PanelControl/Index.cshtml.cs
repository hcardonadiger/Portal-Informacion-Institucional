namespace Diger.TramitesEstado.Web.Pages.PanelControl;

/// <summary>Portada del Panel de control: una tarjeta por submódulo. Hoy solo Notificaciones; aquí
/// van a ir los siguientes (tamaño de archivos, etc.), cada uno con su propia clave.</summary>
[Authorize]
[Permission(NotificacionesModel.Clave, AccionModulo.Ver, "Ver la configuración de notificaciones del Panel de control")]
public sealed class IndexModel : PageModel
{
    public void OnGet() { }
}
