using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Diger.TramitesEstado.Web.Pages.Tramites;

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
