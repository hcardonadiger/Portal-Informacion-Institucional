using Diger.TramitesEstado.Application.Siger.Publico;
using Diger.TramitesEstado.Application.Siger.Publico.Queries.GetInstitucionesPublicas;
using Microsoft.AspNetCore.Authorization;

namespace Diger.TramitesEstado.Presentation.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/instituciones")]
public sealed class InstitucionesPublicasController(ISender sender) : ControllerBase
{
    /// <summary>Las instituciones activas, con sus datos de contacto y cuántos trámites publican.</summary>
    /// <remarks>
    /// Solo salen las **activas**. Una institución dada de alta pero aún sin aprobar no
    /// aparece aquí, aunque sus trámites sí puedan estar publicados.
    ///
    /// La sigla que devuelve (por ejemplo INPREMA) es la que espera el parámetro
    /// <c>institucion</c> del catálogo.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<InstitucionPublicaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InstitucionPublicaDto>>> Listar(CancellationToken ct)
    {
        var resultado = await sender.Send(new GetInstitucionesPublicasQuery(), ct);
        return Ok(resultado);
    }
}
