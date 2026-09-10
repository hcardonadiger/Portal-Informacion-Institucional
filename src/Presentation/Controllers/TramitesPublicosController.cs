using Diger.TramitesEstado.Application.Siger.Publico;
using Diger.TramitesEstado.Application.Siger.Publico.Queries.GetCatalogoPublico;
using Diger.TramitesEstado.Application.Siger.Publico.Queries.GetTramitePublico;
using Microsoft.AspNetCore.Authorization;

namespace Diger.TramitesEstado.Presentation.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/tramites")]
public sealed class TramitesPublicosController(ISender sender) : ControllerBase
{
    /// <summary>GET /api/v1/tramites — catálogo paginado.</summary>
    [HttpGet]
    [ProducesResponseType<CatalogoPublicoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CatalogoPublicoDto>> Listar(
        [FromQuery] string? busqueda, [FromQuery] int? categoria, [FromQuery] string? institucion,
        [FromQuery] string? modalidad, [FromQuery] bool soloGratuitos = false, [FromQuery] bool soloEnSol = false,
        [FromQuery] bool soloFichasCompletas = false, [FromQuery] string? orden = null,
        [FromQuery] int pagina = 1, [FromQuery] int tamano = 20, CancellationToken ct = default)
    {
        var resultado = await sender.Send(new GetCatalogoPublicoQuery(
            busqueda, categoria, institucion, modalidad,
            soloGratuitos, soloEnSol, soloFichasCompletas, orden, pagina, tamano), ct);
        return Ok(resultado);
    }

    /// <summary>GET /api/v1/tramites/{codigo} — ficha completa. Por Codigo, no por Id.</summary>
    [HttpGet("{codigo}")]
    [ProducesResponseType<TramiteDetallePublicoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TramiteDetallePublicoDto>> Detalle(string codigo, CancellationToken ct)
    {
        var dto = await sender.Send(new GetTramitePublicoQuery(codigo), ct);
        return dto is null ? NotFound() : Ok(dto);
    }
}
