using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MediatR;
using Diger.TramitesEstado.Application.Usuarios.Commands.ActualizarMiPerfil;
using Diger.TramitesEstado.Application.Usuarios.Queries.GetUsuarioById;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.CuentaMicrosoft;
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

    public MiConexionOutlookDto Outlook { get; private set; } = new(false, false, null, null, false);

    /// <summary>Resultado del ida y vuelta con Microsoft (lo deja OutlookCallback).</summary>
    [TempData] public string? OutlookAviso { get; set; }

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
        Outlook = await sender.Send(new GetMiConexionOutlookQuery(), ct);
        return Page();
    }

    public async Task<IActionResult> OnPostConectarOutlookAsync(CancellationToken ct)
    {
        var urlRetorno = Url.PageLink("/Cuenta/OutlookCallback")!;
        try
        {
            var inicio = await sender.Send(new IniciarConexionOutlookQuery(urlRetorno), ct);

            // Estado y verificador PKCE viajan en TempData (cookie cifrada con Data Protection):
            // nunca a la base ni a un log.
            TempData["OutlookEstado"]      = inicio.Estado;
            TempData["OutlookVerificador"] = inicio.Verificador;
            return Redirect(inicio.Url);
        }
        catch (DomainException ex)
        {
            // Sin app registrada (o MSAL caído) no hay adónde mandarlo: se avisa en la tarjeta.
            OutlookAviso = ex.Message;
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostDesconectarOutlookAsync(CancellationToken ct)
    {
        await sender.Send(new DesconectarOutlookCommand(), ct);
        // "SuccessMessage" es la clave que pinta _CuentaLayout; "SuccessMsg" no se mostraría aquí.
        TempData["SuccessMessage"] = "Outlook desconectado. Las citas ya enviadas siguen en su calendario.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (currentUser.UserId == null) return RedirectToPage("/Cuenta/Login");

        SinAsignacion = string.IsNullOrWhiteSpace(currentUser.Rol);

        if (!ModelState.IsValid)
        {
            CargarContexto();
            Outlook = await sender.Send(new GetMiConexionOutlookQuery(), ct);
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
            Outlook = await sender.Send(new GetMiConexionOutlookQuery(), ct);
            return Page();
        }
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
