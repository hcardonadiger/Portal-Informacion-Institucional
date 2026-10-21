using Diger.TramitesEstado.Application.Proyectos.Common;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using Diger.TramitesEstado.Application.Proyectos.Queries.GetInformeProyecto;
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
public sealed class ProyectoModel(ISender sender, IInformeProyectoPdfService informePdf) : PageModel
{
    public TableroProyectoDto Data { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var d = await sender.Send(new GetTableroProyectoQuery(id), ct);
        if (d is null) return NotFound();

        Data = d;
        return Page();
    }

    /// <summary>Descarga el Informe de Estado (PDF, formato PMI) del proyecto. El alcance lo hace
    /// valer la consulta: un proyecto ajeno devuelve null y aquí se traduce en 404.</summary>
    public async Task<IActionResult> OnGetInformeAsync(int id, CancellationToken ct)
    {
        var dto = await sender.Send(new GetInformeProyectoQuery(id), ct);
        if (dto is null) return NotFound();

        var bytes  = informePdf.Generar(dto);
        var nombre = $"Informe_{dto.Ficha.Codigo}_{DateTime.Now:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", nombre);
    }
}
