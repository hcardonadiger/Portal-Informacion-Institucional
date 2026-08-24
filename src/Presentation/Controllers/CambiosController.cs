using Diger.TramitesEstado.Application.Siger.Publico;
using Diger.TramitesEstado.Application.Siger.Publico.Queries.GetCambiosPublicos;
using Diger.TramitesEstado.Application.Siger.Publico.Queries.GetCodigosPublicados;
using Microsoft.AspNetCore.Authorization;

namespace Diger.TramitesEstado.Presentation.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class CambiosController(ISender sender) : ControllerBase
{
    /// <summary>Códigos que cambiaron desde una fecha — sincronización incremental.</summary>
    /// <remarks>
    /// Devuelve solo los códigos, no las fichas: el consumidor pide luego el detalle de cada
    /// uno. Junto a los códigos viene `generadoEl`, **la hora del servidor**: guárdela y úsela
    /// como `desde` en la siguiente llamada. Usar el reloj propio pierde una franja de cambios
    /// en cada ciclo si los dos relojes no coinciden.
    ///
    /// **Esta ruta no reporta bajas.** Un trámite retirado no deja ninguna fila que devolver,
    /// así que hay que contrastar contra /api/v1/codigos-publicados para retirarlas.
    ///
    /// **Y tiene un punto ciego:** se apoya en la fecha de modificación, que un UPDATE directo
    /// contra la base no toca. Conviene forzar un ciclo completo cada cierto tiempo.
    /// </remarks>
    /// <param name="desde">Fecha ISO 8601, por ejemplo 2026-08-01T00:00:00Z. Es obligatorio.</param>
    /// <response code="400">Falta `desde`. Antes se enlazaba a 0001-01-01 y la ruta devolvía el
    /// catálogo entero en silencio: no perdía datos, pero convertía un error del cliente en
    /// tráfico invisible.</response>
    [HttpGet("cambios")]
    [ProducesResponseType<CambiosPublicosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CambiosPublicosDto>> Cambios([FromQuery] DateTime? desde, CancellationToken ct)
    {
        if (desde is null)
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title  = "Falta el parámetro 'desde'",
                Detail = "Indique desde qué fecha quiere los cambios, en formato ISO 8601 " +
                         "(por ejemplo 2026-08-01T00:00:00Z). Para traer el catálogo completo " +
                         "use /api/v1/codigos-publicados, que es la ruta pensada para eso."
            });

        var resultado = await sender.Send(new GetCambiosPublicosQuery(desde.Value), ct);
        return Ok(resultado);
    }

    /// <summary>Todos los códigos publicados ahora mismo. Es la lista de lo que está vivo.</summary>
    /// <remarks>
    /// Sirve para lo que /api/v1/cambios no puede hacer: **detectar bajas**. Lo que el
    /// consumidor tenga guardado y no aparezca aquí, ya no existe y debe retirarlo.
    ///
    /// Regla importante para quien la consuma: si esta ruta no responde, **no borre nada**.
    /// Confundir «no pude preguntar» con «ya no existe nada» vacía un portal entero por un
    /// fallo de red de dos segundos.
    /// </remarks>
    [HttpGet("codigos-publicados")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> CodigosPublicados(CancellationToken ct)
    {
        var resultado = await sender.Send(new GetCodigosPublicadosQuery(), ct);
        return Ok(resultado);
    }
}
