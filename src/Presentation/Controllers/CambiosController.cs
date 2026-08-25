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
    /// <summary>GET /api/v1/cambios?desde= — códigos a refrescar (sincronización incremental).</summary>
    [HttpGet("cambios")]
    [ProducesResponseType<CambiosPublicosDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CambiosPublicosDto>> Cambios([FromQuery] DateTime desde, CancellationToken ct)
    {
        var resultado = await sender.Send(new GetCambiosPublicosQuery(desde), ct);
        return Ok(resultado);
    }

    /// <summary>GET /api/v1/codigos-publicados — todos los códigos vivos (para retirar bajas).</summary>
    [HttpGet("codigos-publicados")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> CodigosPublicados(CancellationToken ct)
    {
        var resultado = await sender.Send(new GetCodigosPublicadosQuery(), ct);
        return Ok(resultado);
    }
}
