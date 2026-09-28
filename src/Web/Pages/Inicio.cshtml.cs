using Diger.TramitesEstado.Web.Common;

namespace Diger.TramitesEstado.Web.Pages;

/// <summary>
/// Adónde va cada quien al entrar: la primera pantalla que su rol puede abrir.
///
/// <para>El login mandaba a todos a Tableros. Un rol que solo trabaja en Honduras Fácil no tiene
/// Tableros.Ver, así que lo primero que veía al entrar era «Acceso denegado», y el «Volver al
/// inicio» de esa pantalla lo llevaba a Expedientes: otro acceso denegado.</para>
///
/// <para>Tableros va primero para que nadie que ya entraba ahí note el cambio; después, el orden
/// del menú. Es una página aparte y no una decisión dentro del login porque el login acaba de
/// emitir la cookie: los permisos del rol recién se pueden leer en la petición siguiente.</para>
///
/// <para>No lleva [Permission]: tiene que poder abrirla cualquiera con sesión, justamente porque
/// su trabajo es averiguar qué más puede abrir.</para>
/// </summary>
public sealed class InicioModel(AccesoModulosService acceso, ICurrentUserService usuario) : PageModel
{
    /// <summary>Clave que exige cada destino —la misma de su [Permission]— y la página.</summary>
    private static readonly (string Clave, string Pagina)[] Destinos =
    [
        ($"{ModulosPortal.Tableros}.Ver",               "/Tableros/Index"),
        ($"{ModulosPortal.Expedientes}.Ver",            "/Index"),
        ($"{ModulosPortal.Siger}.Ver",                  "/Siger/Index"),
        ("HondurasSimple.Conciliacion.Editar",          "/HondurasSimple/Conciliacion"),
        ("HondurasSimple.Publicacion.Ver",              "/HondurasSimple/Publicacion"),
        ("HondurasSimple.Llenado.Ver",                  "/HondurasSimple/Llenado"),
        ($"{ModulosPortal.HondurasSimple}.Ver",         "/HondurasSimple/Completitud"),
        ($"{ModulosPortal.HondurasSimple}.Editar",      "/HondurasSimple/CapturaLote"),
        ($"{ModulosPortal.Proyectos}.Ver",              "/Proyectos/Index"),
        ($"{ModulosPortal.Calendario}.Ver",             "/Calendario/Index"),
        ($"{ModulosPortal.Reuniones}.Ver",              "/Reuniones/Index"),
        ($"{ModulosPortal.Tickets}.Ver",                "/Tickets/Index"),
        ($"{ModulosPortal.Contactos}.Ver",              "/Contactos/Index"),
    ];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        // Sin asignación no hay rol, y sin rol el alcance se resuelve como global: preguntarle al
        // servicio de accesos diría que puede con todo. Se decide antes, igual que en el login.
        if (usuario.Rol is null)
            return RedirectToPage("/Cuenta/Perfil", new { sinAsignacion = true });

        foreach (var (clave, pagina) in Destinos)
            if (await acceso.PuedeClaveAsync(clave, ct))
                return RedirectToPage(pagina);

        // Un rol sin ningún módulo: el perfil siempre se puede ver.
        return RedirectToPage("/Cuenta/Perfil");
    }
}
