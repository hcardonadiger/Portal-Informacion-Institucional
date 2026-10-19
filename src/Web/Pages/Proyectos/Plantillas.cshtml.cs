using Diger.TramitesEstado.Application.Proyectos.Plantillas;
using Diger.TramitesEstado.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Pages.Proyectos;

/// <summary>
/// Plantillas de estructura de proyecto, para los que se repiten cada ciclo.
/// </summary>
/// <remarks>
/// <para>Vive en el módulo de Proyectos y no en Administración —donde están las de
/// trámite— porque quien las necesita es quien formula proyectos, no quien administra
/// el portal. Lleva su propia clave, <c>Proyectos.Plantillas</c>, para que mantener
/// plantillas no obligue a dar permiso de editar todos los proyectos.</para>
///
/// <para>El árbol llega del formulario en listas paralelas y no como JSON: así el
/// formulario sigue siendo un &lt;form&gt; que funciona sin JavaScript para el caso
/// simple, y el JS solo añade y quita filas. Ver <see cref="LeerArbol"/>.</para>
/// </remarks>
[Permission("Proyectos.Plantillas", AccionModulo.Ver, "Ver plantillas de proyecto")]
[Authorize(Policy = "Proyectos.Plantillas.Ver")]
public sealed class PlantillasModel(ISender sender, IApplicationDbContext ctx) : PageModel
{
    public IReadOnlyList<PlantillaProyectoResumenDto> Plantillas { get; private set; } = [];

    /// <summary>La plantilla que se está editando, o null si el formulario está en blanco.</summary>
    public PlantillaProyectoDto? EnEdicion { get; private set; }

    /// <summary>Áreas y unidades del ámbito activo, para el selector de visibilidad.</summary>
    public IReadOnlyList<(string Id, string Nombre)> Areas { get; private set; } = [];
    public IReadOnlyList<(string Id, string Nombre, string AreaId)> Unidades { get; private set; } = [];

    public async Task OnGetAsync(int? editar, CancellationToken ct)
    {
        await CargarAsync(editar, ct);
    }

    private async Task CargarAsync(int? editar, CancellationToken ct)
    {
        // SoloActivas: false — esta pantalla las administra, así que tiene que
        // ver también las desactivadas para poder reactivarlas.
        Plantillas = await sender.Send(new GetPlantillasProyectoQuery(SoloActivas: false), ct);

        if (editar is { } id)
            EnEdicion = await sender.Send(new GetPlantillaProyectoQuery(id), ct);

        // El filtro global del contexto ya acota estas dos al ámbito del usuario.
        Areas = await ctx.Areas.AsNoTracking()
            .OrderBy(a => a.Nombre)
            .Select(a => new ValueTuple<string, string>(a.Id, a.Nombre))
            .ToListAsync(ct);

        Unidades = await ctx.Unidades.AsNoTracking()
            .OrderBy(u => u.Nombre)
            .Select(u => new ValueTuple<string, string, string>(u.Id, u.Nombre, u.AreaId))
            .ToListAsync(ct);
    }

    [Permission("Proyectos.Plantillas", AccionModulo.Crear, "Crear y editar plantillas de proyecto")]
    public async Task<IActionResult> OnPostGuardarAsync(
        int? id, string nombre, string? descripcion, bool activa,
        NivelAlcance nivel, string? areaId, string? unidadId,
        CancellationToken ct)
    {
        var entregables = LeerArbol();

        try
        {
            await sender.Send(new GuardarPlantillaProyectoCommand(
                id, nombre, descripcion, activa, nivel, areaId, unidadId, entregables), ct);
            TempData["SuccessMsg"] = id is null ? "Plantilla creada." : "Plantilla actualizada.";
            return RedirectToPage();
        }
        catch (DomainException ex)
        {
            // Se vuelve a pintar la pantalla en vez de redirigir: un redirect
            // se lleva por delante lo que la persona acababa de teclear.
            TempData["ErrorMsg"] = ex.Message;
            await CargarAsync(id, ct);
            return Page();
        }
    }

    [Permission("Proyectos.Plantillas", AccionModulo.Eliminar, "Eliminar plantillas de proyecto")]
    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new EliminarPlantillaProyectoCommand(id), ct);
            TempData["SuccessMsg"] = "Plantilla eliminada.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        return RedirectToPage();
    }

    /// <summary>
    /// Reconstruye el árbol desde los campos del formulario.
    /// </summary>
    /// <remarks>
    /// Los entregables llegan como <c>ent_nombre</c> / <c>ent_desc</c>, y cada actividad
    /// como <c>act_nombre_{i}</c> / <c>act_desc_{i}</c>, donde {i} es la posición del
    /// entregable al que pertenece. Se eligió así, y no un JSON en un campo oculto,
    /// porque el <c>name</c> de cada input dice a qué pertenece: si el JS falla a medias,
    /// lo que llega sigue siendo interpretable en vez de una cadena rota.
    ///
    /// El índice {i} es la posición en el formulario, no un id: el orden definitivo lo
    /// pone el servidor al guardar, por la posición en la lista.
    /// </remarks>
    private List<EntregablePlantillaInput> LeerArbol()
    {
        var nombres = Request.Form["ent_nombre"];
        var descs   = Request.Form["ent_desc"];
        var lista   = new List<EntregablePlantillaInput>();

        for (var i = 0; i < nombres.Count; i++)
        {
            var nombre = nombres[i];
            if (string.IsNullOrWhiteSpace(nombre)) continue;

            var actNombres = Request.Form[$"act_nombre_{i}"];
            var actDescs   = Request.Form[$"act_desc_{i}"];
            var actividades = new List<ActividadPlantillaInput>();

            for (var j = 0; j < actNombres.Count; j++)
            {
                if (string.IsNullOrWhiteSpace(actNombres[j])) continue;
                actividades.Add(new ActividadPlantillaInput(
                    actNombres[j]!,
                    j < actDescs.Count ? actDescs[j] : null));
            }

            lista.Add(new EntregablePlantillaInput(
                nombre!,
                i < descs.Count ? descs[i] : null,
                actividades));
        }

        return lista;
    }

    public static string NivelTexto(NivelAlcance n) => n switch
    {
        NivelAlcance.Institucion => "Toda la institución",
        NivelAlcance.Area        => "Solo mi área",
        NivelAlcance.Unidad      => "Solo mi unidad",
        _                        => n.ToString()
    };
}
