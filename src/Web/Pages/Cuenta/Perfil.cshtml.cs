using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MediatR;
using Diger.TramitesEstado.Application.Calendario.Ics;
using Diger.TramitesEstado.Application.Usuarios.Commands.ActualizarMiPerfil;
using Diger.TramitesEstado.Application.Usuarios.Queries.GetUsuarioById;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.Cuenta;

// [Authorize] explícito: la carpeta /Cuenta está exenta por convención (AllowAnonymousToFolder,
// necesario para login/logout/recuperación), así que sin esto la página de perfil quedaba
// alcanzable sin sesión.
[Authorize]
[PermisoNoRequerido("Autoservicio: cualquier usuario autenticado ve y edita su propio perfil.")]
public sealed class PerfilModel(ISender sender, ICurrentUserService currentUser) : PageModel
{
    [BindProperty] public string Nombre { get; set; } = string.Empty;
    [BindProperty] public string Correo { get; set; } = string.Empty;
    [BindProperty] public string? Telefono { get; set; }

    // Contexto institucional activo (solo lectura)
    public string RolActivo { get; private set; } = "";
    public string InstitucionActiva { get; private set; } = "";
    public string? AreaActiva { get; private set; }
    public string? UnidadActiva { get; private set; }

    /// <summary>La cuenta no tiene institución ni rol asignado, así que no puede usar ningún
    /// módulo. Es adonde el login la manda, porque el perfil es de lo poco que sí puede ver.</summary>
    public bool SinAsignacion { get; private set; }

    public async Task<IActionResult> OnGetAsync(bool sinAsignacion, CancellationToken ct)
    {
        if (currentUser.UserId == null) return RedirectToPage("/Cuenta/Login");

        // Se confía en el estado real, no en el parámetro: el aviso aparece también si el
        // usuario llega al perfil por su cuenta, y no se puede provocar con la URL.
        SinAsignacion = string.IsNullOrWhiteSpace(currentUser.Rol);

        var dto = await sender.Send(new GetUsuarioByIdQuery(currentUser.UserId.Value), ct);
        Nombre = dto.Nombre;
        Correo = dto.Correo;
        Telefono = dto.Telefono;

        CargarContexto();
        await CargarSuscripcionAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (currentUser.UserId == null) return RedirectToPage("/Cuenta/Login");

        SinAsignacion = string.IsNullOrWhiteSpace(currentUser.Rol);

        if (!ModelState.IsValid)
        {
            CargarContexto();
            return Page();
        }

        try
        {
            await sender.Send(new ActualizarMiPerfilCommand(
                currentUser.UserId.Value, Nombre, Correo, Telefono), ct);

            TempData["SuccessMessage"] = "Tu perfil ha sido actualizado correctamente.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            CargarContexto();
            return Page();
        }
    }

    // ── Suscripción al calendario ────────────────────────────────────────────
    // El enlace no se crea solo: aparece cuando la persona lo pide. Es un secreto de portador
    // —quien tenga la URL ve su agenda— y no corresponde generarle uno a quien nunca lo va a usar.

    /// <summary>URL de suscripción, ya armada. Null mientras la persona no la haya pedido.</summary>
    public string? UrlCalendario { get; private set; }

    private async Task CargarSuscripcionAsync(CancellationToken ct)
    {
        var token = await sender.Send(new ObtenerTokenCalendarioQuery(), ct);
        UrlCalendario = token is { } t
            ? $"{Request.Scheme}://{Request.Host}{Url.Page("/Calendario/Feed", new { token = t })}"
            : null;
    }

    public async Task<IActionResult> OnPostSuscripcionAsync(bool regenerar, CancellationToken ct)
    {
        if (currentUser.UserId == null) return RedirectToPage("/Cuenta/Login");

        if (regenerar)
        {
            await sender.Send(new RegenerarTokenCalendarioCommand(), ct);
            TempData["SuccessMessage"] = "Se generó un enlace nuevo. El anterior dejó de funcionar.";
        }
        else
        {
            await sender.Send(new ObtenerTokenCalendarioCommand(), ct);
            TempData["SuccessMessage"] = "Enlace de suscripción listo.";
        }

        return RedirectToPage();
    }

    private void CargarContexto()
    {
        RolActivo         = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                            ?? User.FindFirst(AppClaims.ActiveRol)?.Value ?? "";
        InstitucionActiva = User.FindFirst(AppClaims.ActiveInstitucion)?.Value ?? "";
        AreaActiva        = User.FindFirst(AppClaims.ActiveArea)?.Value;
        UnidadActiva      = User.FindFirst(AppClaims.ActiveUnidad)?.Value;
    }
}
