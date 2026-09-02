using Diger.TramitesEstado.Application.Siger.Publico;
using Diger.TramitesEstado.Application.Siger.Publico.Queries.GetCategoriasPublicas;
using Microsoft.AspNetCore.Authorization;

namespace Diger.TramitesEstado.Presentation.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/categorias")]
public sealed class CategoriasPublicasController(ISender sender) : ControllerBase
{
    /// <summary>GET /api/v1/categorias — las ocho categorías, con conteo de trámites publicados.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoriaPublicaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoriaPublicaDto>>> Listar(CancellationToken ct)
    {
        var resultado = await sender.Send(new GetCategoriasPublicasQuery(), ct);
        return Ok(resultado);
    }
}
