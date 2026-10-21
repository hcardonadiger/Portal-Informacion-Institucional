using Diger.TramitesEstado.Application.Siger.Publico;
using Diger.TramitesEstado.Application.Siger.Publico.Queries.GetInstitucionesPublicas;
using Microsoft.AspNetCore.Authorization;

namespace Diger.TramitesEstado.Presentation.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/instituciones")]
public sealed class InstitucionesPublicasController(ISender sender) : ControllerBase
{
    /// <summary>GET /api/v1/instituciones — listado con contacto y conteos.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<InstitucionPublicaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InstitucionPublicaDto>>> Listar(CancellationToken ct)
    {
        var resultado = await sender.Send(new GetInstitucionesPublicasQuery(), ct);
        return Ok(resultado);
    }
}
