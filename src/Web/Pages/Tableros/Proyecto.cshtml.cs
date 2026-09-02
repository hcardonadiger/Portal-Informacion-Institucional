using Diger.TramitesEstado.Application.Proyectos.Queries;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.Tableros;

/// <summary>
/// El tablero de UN proyecto — el zoom del que lista el portafolio (<c>/Tableros/Proyectos</c>).
///
/// <para>Pide <c>Proyectos.Ver</c>, igual que su hermano y por la misma razón: quién ve un proyecto
/// lo decide el permiso del módulo, así que gatearlo con <c>Tableros.Ver</c> abriría por la ventana
/// lo que Proyectos cierra por la puerta. El alcance por institución lo pone la consulta: pedir el
/// tablero de un proyecto ajeno devuelve null y acá se traduce en 404, no en una página vacía.</para>
/// </summary>
[Authorize]
[Permission("Proyectos", AccionModulo.Ver, "Ver proyectos")]
public sealed class ProyectoModel(ISender sender) : PageModel
{
    public TableroProyectoDto Data { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var d = await sender.Send(new GetTableroProyectoQuery(id), ct);
        if (d is null) return NotFound();

        Data = d;
        return Page();
    }
}
