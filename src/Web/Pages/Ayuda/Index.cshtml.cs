using Diger.TramitesEstado.Application.Accesos;
using Diger.TramitesEstado.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Diger.TramitesEstado.Web.Pages.Ayuda;

[Authorize]
public sealed class IndexModel(AccesoModulosService acceso, ICurrentUserService currentUser) : PageModel
{
    public bool EsAdmin { get; private set; }
    public string UserNombre { get; private set; } = "";
    public string UserRol { get; private set; } = "";

    public bool PuedeTableros { get; private set; }
    public bool PuedeCalendario { get; private set; }
    public bool PuedeExpedientes { get; private set; }
    public bool PuedeReuniones { get; private set; }
    public bool PuedeTickets { get; private set; }
    public bool PuedeContactos { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        EsAdmin    = acceso.EsAdministrador || User.IsInRole("Administrador");
        UserNombre = currentUser.Nombre ?? User.Identity?.Name ?? "Usuario";
        UserRol    = currentUser.Rol ?? (EsAdmin ? "Administrador" : "Usuario");

        PuedeTableros    = await acceso.PuedeAsync(ModulosPortal.Tableros, ct);
        PuedeCalendario  = await acceso.PuedeAsync(ModulosPortal.Calendario, ct);
        PuedeExpedientes = await acceso.PuedeAsync(ModulosPortal.Expedientes, ct);
        PuedeReuniones   = await acceso.PuedeAsync(ModulosPortal.Reuniones, ct);
        PuedeTickets     = await acceso.PuedeAsync(ModulosPortal.Tickets, ct);
        PuedeContactos   = await acceso.PuedeAsync(ModulosPortal.Contactos, ct);
    }
}
