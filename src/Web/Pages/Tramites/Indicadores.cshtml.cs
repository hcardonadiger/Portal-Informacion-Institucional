using Diger.TramitesEstado.Web.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Diger.TramitesEstado.Web.Pages.Tramites;

// Los indicadores de trámites pasan a otro aplicativo. Se conserva el código, pero la sección
// queda sin acceso (404) y sin enlace en el menú. Para reactivarla: quitar el atributo y
// restaurar el enlace en Pages/Shared/_Layout.cshtml.
[SeccionDeshabilitada]
public sealed class IndicadoresModel(IWebHostEnvironment environment) : PageModel
{
    public void OnGet() { }

    public IActionResult OnGetDatos()
    {
        var file = Path.Combine(environment.ContentRootPath, "App_Data", "tramites-indicadores.json");
        if (!System.IO.File.Exists(file))
            return NotFound(new { error = "No se encontró el archivo de indicadores. Ejecute scripts/procesar_tramites.py." });

        Response.Headers.CacheControl = "private, max-age=300";
        return PhysicalFile(file, "application/json; charset=utf-8");
    }
}
