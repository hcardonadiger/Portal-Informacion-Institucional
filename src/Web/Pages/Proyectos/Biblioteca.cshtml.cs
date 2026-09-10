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
    [BindProperty(SupportsGet = true)] public string?   SubidoPor   { get; set; }
    [BindProperty(SupportsGet = true)] public string?   Tipo        { get; set; }
    [BindProperty(SupportsGet = true)] public bool      ConHistorial { get; set; }
    [BindProperty(SupportsGet = true)] public bool      Estancados   { get; set; }

    /// <summary>Umbral de «sin actualizar». Tres meses: menos y marcaría documentación que
    /// simplemente no ha necesitado cambios.</summary>
    public const int DiasEstancado = 90;

    /// <summary>Cómo se presenta: carpetas navegables, todo agrupado a la vez, o tabla plana.
    /// La misma documentación, tres formas de entrar.</summary>
    public enum ModoVista { Carpetas, Agrupado, Lista }

    /// <summary>Por qué se agrupa. Es lo que hace útil la vista de carpetas para coordinación:
    /// «todos los convenios», «el expediente de este proyecto», «qué mantiene esta persona».</summary>
    public enum CriterioCarpeta { Categoria, Proyecto, Responsable }

    /// <summary>Lista por defecto, no carpetas. Dos razones: no cambiarle la pantalla a quien ya
    /// usa la biblioteca, y que el listado es lo único que muestra los títulos —de lo que dependen
    /// las pruebas de aislamiento por alcance para comprobar que no se cuela documentación ajena—.
    /// Las carpetas quedan a un clic.</summary>
    [BindProperty(SupportsGet = true)] public ModoVista       Modo      { get; set; } = ModoVista.Lista;
    [BindProperty(SupportsGet = true)] public CriterioCarpeta Organizar { get; set; } = CriterioCarpeta.Categoria;

    /// <summary>Carpeta abierta. Null = se muestra el mosaico.</summary>
    [BindProperty(SupportsGet = true)] public string? Carpeta { get; set; }

    public BibliotecaDto Datos { get; private set; } = new([], [], [], 0);

    /// <summary>Hay algún filtro puesto. Decide si se ofrece el «limpiar filtros» y si el vacío
    /// se lee como «no hay nada» o como «no hay nada que cumpla esto». El modo de vista no cuenta:
    /// mirar en carpetas no es filtrar.</summary>
    public bool ConFiltros =>
        CategoriaId is not null || ProyectoId is not null
        || !string.IsNullOrWhiteSpace(Q) || Desde is not null || Hasta is not null
        || !string.IsNullOrWhiteSpace(SubidoPor) || !string.IsNullOrWhiteSpace(Tipo)
        || ConHistorial || Estancados;

    /// <summary>La clave de carpeta de un documento, según el criterio elegido.</summary>
    public string ClaveCarpeta(DocumentoBibliotecaDto d) => Organizar switch
    {
        CriterioCarpeta.Proyecto    => $"{d.ProyectoCodigo} — {d.ProyectoNombre}",
        CriterioCarpeta.Responsable => d.SubidoPor,
        _                           => d.Categoria
    };

    public string EtiquetaCriterio => Organizar switch
    {
        CriterioCarpeta.Proyecto    => "Proyecto",
        CriterioCarpeta.Responsable => "Responsable de carga",
        _                           => "Categoría"
    };

    public async Task OnGetAsync(CancellationToken ct) =>
        Datos = await sender.Send(new GetBibliotecaQuery(
            CategoriaId, ProyectoId, Q, Desde, Hasta, SubidoPor, Tipo,
            ConHistorial, Estancados ? DiasEstancado : null), ct);

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
