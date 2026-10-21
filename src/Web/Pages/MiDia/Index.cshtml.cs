using Diger.TramitesEstado.Application.MiDia.Common;
using Diger.TramitesEstado.Application.MiDia.Queries.GetMiDia;
using Diger.TramitesEstado.Application.Reuniones.Commands.CambiarEstadoCompromiso;

namespace Diger.TramitesEstado.Web.Pages.MiDia;

/// <summary>
/// La bandeja personal del día. Va con <see cref="PermisoNoRequeridoAttribute"/> por la misma razón
/// que Mi Tablero: solo muestra el trabajo de quien está conectado —el handler lo resuelve del
/// contexto, no de la petición—, y condicionarla a una casilla de la matriz dejaría a alguien sin
/// poder ver sus propios pendientes.
/// </summary>
[Authorize]
[PermisoNoRequerido("Autoservicio: resume el trabajo asignado al propio usuario, no datos de otros.")]
public sealed class IndexModel(ISender sender, AccesoModulosService acceso) : PageModel
{
    public MiDiaDto Datos { get; private set; } = MiDiaDto.Vacio;

    /// <summary>Si puede avanzar compromisos desde acá. Es el permiso del destino, no el de esta
    /// página: quien no edita reuniones ve la fila pero sin el botón.</summary>
    public bool PuedeCerrarCompromisos { get; private set; }

    [BindProperty(SupportsGet = true)] public bool VerTodo { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Datos = await sender.Send(new GetMiDiaQuery(VerTodo), ct);
        PuedeCerrarCompromisos = await acceso.PuedeEditarAsync(ModulosPortal.Reuniones, ct);
    }

    /// <summary>
    /// «Lo terminé» sobre un compromiso: lo deja <see cref="EstadoCompromiso.EnRevision"/>, no
    /// cumplido. Es la regla del módulo —quien no es administrador no puede auto-aprobar su propio
    /// compromiso—, y replicarla acá evita ofrecer un botón que el comando iba a rechazar.
    /// </summary>
    [Permission("Reuniones", AccionModulo.Editar, "Crear y editar reuniones")]
    public async Task<IActionResult> OnPostListoAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new CambiarEstadoCompromisoCommand(
                id, EstadoCompromiso.EnRevision, "Marcado como terminado desde Mi día."), ct);
            TempData["SuccessMsg"] = "Compromiso enviado a revisión.";
        }
        catch (DomainException ex)
        {
            TempData["ErrorMsg"] = ex.Message;
        }

        return RedirectToPage(new { VerTodo });
    }
}
