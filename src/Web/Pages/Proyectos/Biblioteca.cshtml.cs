using Diger.TramitesEstado.Application.Proyectos.Commands.RegistrarDescargaDocumento;
using Diger.TramitesEstado.Application.Proyectos.Common;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.Proyectos;

/// <summary>
/// La consulta transversal de la documentación del portafolio.
///
/// <para>Va con la clave del submódulo y no con <c>Proyectos.Ver</c>: consultar documentación es
/// una atribución propia. Quien la tiene ve la documentación de <b>los proyectos que puede ver</b>
/// —ni uno más—, y eso lo resuelve el filtro heredado en la consulta, no esta página.</para>
/// </summary>
[Authorize]
[Permission("Proyectos.Documentos", AccionModulo.Ver, "Ver y descargar documentos de proyecto")]
public sealed class BibliotecaModel(ISender sender, IWebHostEnvironment env) : PageModel
{
    [BindProperty(SupportsGet = true)] public int?      CategoriaId { get; set; }
    [BindProperty(SupportsGet = true)] public int?      ProyectoId  { get; set; }
    [BindProperty(SupportsGet = true)] public string?   Q           { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? Desde       { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? Hasta       { get; set; }

    public BibliotecaDto Datos { get; private set; } = new([], [], [], 0);

    /// <summary>Hay algún filtro puesto. Decide si se ofrece el «limpiar filtros» y si el vacío
    /// se lee como «no hay nada» o como «no hay nada que cumpla esto».</summary>
    public bool ConFiltros =>
        CategoriaId is not null || ProyectoId is not null
        || !string.IsNullOrWhiteSpace(Q) || Desde is not null || Hasta is not null;

    public async Task OnGetAsync(CancellationToken ct) =>
        Datos = await sender.Send(new GetBibliotecaQuery(CategoriaId, ProyectoId, Q, Desde, Hasta), ct);

    /// <summary>
    /// Sirve una versión.
    ///
    /// <para>Handler propio en vez de enlazar al de la ficha: si el archivo físico faltara, el de
    /// la ficha devolvería al usuario a un proyecto en el que quizá no estaba trabajando. La
    /// resolución de permisos es la misma —la consulta lleva su ancla— así que no se abre nada.</para>
    /// </summary>
    public async Task<IActionResult> OnGetDescargarAsync(int versionId, CancellationToken ct)
    {
        var meta = await sender.Send(new GetDescargaDocumentoQuery(versionId), ct);
        if (meta is null) return NotFound();

        var ruta = ArchivosProtegidos.Resolver(env, meta.ArchivoUrl);
        if (ruta is null)
        {
            TempData["ErrorMsg"] = $"«{meta.ArchivoNombre}» ya no está disponible en el servidor.";
            return RedirectToPage();
        }

        // Después de resolver la ruta y antes de servir: no se anota una descarga que no ocurrió
        // porque el archivo ya no estaba.
        await sender.Send(new RegistrarDescargaDocumentoCommand(versionId), ct);

        return PhysicalFile(ruta, ArchivosProtegidos.TipoContenido(meta.ArchivoNombre), meta.ArchivoNombre);
    }
}
