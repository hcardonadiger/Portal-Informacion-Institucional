using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.Proyectos;

/// <summary>
/// La bandeja de trabajo de una persona: todas sus actividades, de todos sus proyectos, con el
/// reporte de avance en la misma fila.
///
/// <para><b>Por qué existe.</b> Reportar exigía abrir el proyecto, buscar la actividad en el árbol
/// y llenar el formulario de la bitácora — una vez por proyecto. Con once frentes en ejecución y el
/// seguimiento repartido, el resultado medido fue que no se reportaba: proyectos con meses sin una
/// sola entrada. Esta pantalla no agrega ninguna regla nueva; solo pone el mismo comando al alcance
/// de la mano.</para>
///
/// <para>Va con <c>Proyectos.Ver</c> para mirar y <c>Proyectos.Avance.Crear</c> para reportar, las
/// mismas claves que la ficha. Quien puede reportar en el proyecto puede reportar acá, y quien no,
/// tampoco — la comprobación de fondo la hace el comando.</para>
/// </summary>
[Authorize]
[Permission("Proyectos", AccionModulo.Ver, "Ver proyectos")]
public sealed class MisActividadesModel(
    ISender sender,
    IWebHostEnvironment env,
    AccesoModulosService acceso) : PageModel
{
    [BindProperty(SupportsGet = true)] public bool VerTerminadas { get; set; }

    public MisActividadesDto Datos { get; private set; } = new([], 0, 0, 0, 0, 0, 0);
    public bool PuedeReportar { get; private set; }

    // ── Formulario de reporte ───────────────────────────────────────────────
    [BindProperty] public string?    Descripcion { get; set; }
    [BindProperty] public int?       Porcentaje  { get; set; }
    [BindProperty] public string?    Bloqueo     { get; set; }
    [BindProperty] public IFormFile? Evidencia   { get; set; }

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    private async Task CargarAsync(CancellationToken ct)
    {
        Datos = await sender.Send(new GetMisActividadesQuery(VerTerminadas), ct);
        PuedeReportar = HttpContext.CanMutate()
                        && await acceso.PuedeClaveAsync("Proyectos.Avance.Crear", ct);
    }

    /// <summary>
    /// Reporta el avance de una actividad sin salir de la bandeja.
    ///
    /// <para>Manda exactamente el mismo <see cref="RegistrarAvanceCommand"/> que la ficha, con la
    /// actividad ya imputada: mueve el porcentaje, deja la entrada en la bitácora del proyecto,
    /// adjunta la evidencia y registra el bloqueo si lo hay. Duplicar la lógica acá habría sido
    /// crear una segunda forma de reportar que con el tiempo diría otra cosa.</para>
    /// </summary>
    [Permission("Proyectos.Avance", AccionModulo.Crear, "Registrar avances de proyecto")]
    public async Task<IActionResult> OnPostReportarAsync(
        int proyectoId, int entregableId, int actividadId, CancellationToken ct)
    {
        try
        {
            string? nombre = null, url = null; long? tamano = null;

            if (Evidencia is { Length: > 0 })
            {
                // Mismo almacenamiento y mismas validaciones que la ficha.
                var guardados = await AdjuntoStorage.GuardarAsync([Evidencia], env, ct, carpeta: "proyectos");
                if (guardados.Count > 0)
                {
                    nombre = guardados[0].Nombre;
                    url    = guardados[0].Url;
                    tamano = guardados[0].Tamano;
                }
            }

            await sender.Send(new RegistrarAvanceCommand(
                proyectoId, Descripcion ?? "",
                entregableId, actividadId, Porcentaje,
                Bloqueo, nombre, url, tamano), ct);

            TempData["SuccessMsg"] = Porcentaje is not null
                ? "Avance registrado y actividad actualizada."
                : "Avance registrado.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return RedirectToPage(new { VerTerminadas });
    }
}
