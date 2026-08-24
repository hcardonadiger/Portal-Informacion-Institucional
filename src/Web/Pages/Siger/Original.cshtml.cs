namespace Diger.TramitesEstado.Web.Pages.Siger;

/// <summary>Visita la foto original de una ficha: cómo estaba antes de que el portal la tocara.</summary>
[Authorize]
[Permission("Siger", AccionModulo.Ver, "Ver el archivo del SIGER original")]
public sealed class OriginalModel(ISender sender) : PageModel
{
    public int Id { get; private set; }
    public FotoOriginalDto? Foto { get; private set; }

    public async Task OnGetAsync(int id, CancellationToken ct)
    {
        Id = id;
        Foto = await sender.Send(new GetFotoOriginalQuery(id), ct);
    }
}
