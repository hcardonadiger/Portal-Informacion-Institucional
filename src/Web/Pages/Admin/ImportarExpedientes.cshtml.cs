using Diger.TramitesEstado.Web.Import;

namespace Diger.TramitesEstado.Web.Pages.Admin;

[Authorize(Policy = "PuedeAdministrarCatalogo")]
public sealed class ImportarExpedientesModel(SupabaseExpedienteImporter importer, IWebHostEnvironment env) : PageModel
{
    public bool EsDesarrollo => env.IsDevelopment();
    public ImportarExpedientesResult? Resultado { get; private set; }
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
            Resultado = await importer.ImportarAsync(ct);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        return Page();
    }
}
