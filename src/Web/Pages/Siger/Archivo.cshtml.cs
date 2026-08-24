using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Pages.Siger;

/// <summary>Estado del archivo del SIGER original y disparador de la captura.</summary>
/// <remarks>
/// La captura es idempotente, así que esta pantalla se puede visitar y ejecutar cuantas veces
/// haga falta: sirve además como respuesta permanente a «¿ya se tomó la foto, y de cuántas?».
/// </remarks>
[Authorize]
[Permission("Siger", AccionModulo.Editar, "Capturar el archivo del SIGER original")]
public sealed class ArchivoModel(ISender sender, IApplicationDbContext ctx) : PageModel
{
    public int TotalFichas { get; private set; }
    public int ConFoto     { get; private set; }
    public int Pendientes  => TotalFichas - ConFoto;

    public ResultadoCapturaOriginal? Resultado { get; private set; }
    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        try
        {
            Resultado = await sender.Send(new CapturarFotosOriginalesCommand(), ct);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }

        await CargarAsync(ct);
        return Page();
    }

    private async Task CargarAsync(CancellationToken ct)
    {
        TotalFichas = await ctx.TramitesSiger.CountAsync(ct);
        ConFoto     = await ctx.FotosTramiteSiger
            .CountAsync(f => f.Version == OrigenFoto.VersionOriginal, ct);
    }
}
