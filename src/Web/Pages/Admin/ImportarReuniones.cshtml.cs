using Diger.TramitesEstado.Web.Common;

namespace Diger.TramitesEstado.Web.Pages.Admin;

[Permission("Admin.Importaciones", AccionModulo.Crear, "Importar reuniones desde Supabase")]
[Authorize(Policy = "Admin.Importaciones.Crear")]
[SoloEnDesarrollo]   // misma importación desde Supabase: tampoco debe existir en producción
public sealed class ImportarReunionesModel(ISender sender, IWebHostEnvironment env) : PageModel
{
    public bool EsDesarrollo => env.IsDevelopment();
    public ImportarReunionesResult? Resultado { get; private set; }
    public string? Error { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!env.IsDevelopment())
        {
            Error = "La importación solo está disponible en entorno de Desarrollo.";
            return Page();
        }

        try
        {
            Resultado = await sender.Send(new ImportarReunionesCommand(), ct);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        return Page();
    }
}
