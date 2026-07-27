using Diger.TramitesEstado.Web.Import;

namespace Diger.TramitesEstado.Web.Pages.Admin;

[Authorize(Policy = "PuedeAdministrarCatalogo")]
public sealed class MigrarSupabaseModel(
    SupabaseMigracionScanner scanner,
    SupabaseExpedienteImporter expedienteImporter,
    SupabaseCatalogosImporter catalogosImporter,
    ISender sender,
    IWebHostEnvironment env) : PageModel
{
    public bool EsDesarrollo => env.IsDevelopment();

    public MigracionScan? Scan { get; private set; }
    public ImportarReunionesResult?  ResReuniones   { get; private set; }
    public ImportarExpedientesResult? ResExpedientes { get; private set; }
    public ImportarCatalogosResult?   ResCatalogos   { get; private set; }
    public string? Error   { get; private set; }
    public string? Mensaje { get; private set; }

    public async Task OnGetAsync(CancellationToken ct) => await RevisarAsync(ct);

    /// <summary>Vuelve a consultar el origen sin escribir nada.</summary>
    public async Task<IActionResult> OnPostRevisarAsync(CancellationToken ct)
    {
        await RevisarAsync(ct);
        return Page();
    }

    /// <summary>Trae lo pendiente de las fuentes con importador. Es idempotente:
    /// lo ya migrado se omite por <c>OrigenExternoId</c>.</summary>
    public async Task<IActionResult> OnPostMigrarAsync(CancellationToken ct)
    {
        if (!env.IsDevelopment())
        {
            Error = "La migración solo está disponible en entorno de Desarrollo.";
            await RevisarAsync(ct);
            return Page();
        }

        try
        {
            // Orden importante: primero el catálogo de instituciones, del que dependen
            // reuniones y expedientes para resolver su institución.
            ResCatalogos   = await catalogosImporter.ImportarAsync(ct);
            ResReuniones   = await sender.Send(new ImportarReunionesCommand(), ct);
            ResExpedientes = await expedienteImporter.ImportarAsync(ct);

            Mensaje = $"Migración ejecutada: {ResCatalogos.InstitucionesCreadas} institución(es), "
                    + $"{ResReuniones.Creadas} reunión(es), {ResExpedientes.Creados} expediente(s), "
                    + $"{ResCatalogos.LevantamientosCreados} levantamiento(s) y "
                    + $"{ResCatalogos.EventosCreados} evento(s) de calendario.";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }

        await RevisarAsync(ct);
        return Page();
    }

    private async Task RevisarAsync(CancellationToken ct)
    {
        try
        {
            Scan = await scanner.RevisarAsync(ct);
        }
        catch (Exception ex)
        {
            Error ??= $"No se pudo consultar el origen: {ex.Message}";
        }
    }
}
