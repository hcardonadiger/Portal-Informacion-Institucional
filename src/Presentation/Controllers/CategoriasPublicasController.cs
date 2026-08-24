using Diger.TramitesEstado.Application.Siger.Publico;
using Diger.TramitesEstado.Application.Siger.Publico.Queries.GetCategoriasPublicas;
using Microsoft.AspNetCore.Authorization;

namespace Diger.TramitesEstado.Presentation.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/categorias")]
public sealed class CategoriasPublicasController(ISender sender) : ControllerBase
{
    /// <summary>Las categorías en que se agrupan los trámites, con cuántos hay publicados en cada una.</summary>
    /// <remarks>
    /// El conteo cuenta **solo lo publicado**, así que una categoría puede salir en 0 aunque
    /// existan trámites suyos sin publicar. El Id que devuelve es el que espera el parámetro
    /// <c>categoria</c> del catálogo.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoriaPublicaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoriaPublicaDto>>> Listar(CancellationToken ct)
    {
        var resultado = await sender.Send(new GetCategoriasPublicasQuery(), ct);
        return Ok(resultado);
    }
}
