using Diger.TramitesEstado.Application.Siger.Historial.Queries.GetHistorialFicha;

namespace Diger.TramitesEstado.Web.Pages.Siger;

/// <summary>
/// El historial de una ficha: qué versiones se han archivado y qué decía cada una.
/// </summary>
/// <remarks>
/// Nació en la Fase 2 enseñando solo la versión 0 —el inventario tal como llegó de SIGER— y la
/// Fase 9 la generalizó, porque desde que existe el pase desde un expediente hay más de una
/// versión que mirar. La ruta se conserva para no romper el enlace que ya existe en el detalle
/// de la ficha; sin versión pedida, sigue enseñando la original.
/// </remarks>
[Authorize]
[Permission("Siger", AccionModulo.Ver, "Ver el archivo del SIGER original")]
public sealed class OriginalModel(ISender sender) : PageModel
{
    public int Id { get; private set; }

    /// <summary>La versión que se está mirando.</summary>
    public int Version { get; private set; }

    public FotoOriginalDto? Foto { get; private set; }

    /// <summary>Todas las versiones archivadas, de la más nueva a la más vieja.</summary>
    public IReadOnlyList<VersionFichaDto> Versiones { get; private set; } = [];

    public async Task OnGetAsync(int id, int? version, CancellationToken ct)
    {
        Id        = id;
        Versiones = await sender.Send(new GetHistorialFichaQuery(id), ct);
        Version   = version ?? OrigenFoto.VersionOriginal;
        Foto      = await sender.Send(new GetFotoOriginalQuery(id, Version), ct);
    }
}
