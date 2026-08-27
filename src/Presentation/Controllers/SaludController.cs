using Diger.TramitesEstado.Application.Siger.Publico;
using Microsoft.AspNetCore.Authorization;

namespace Diger.TramitesEstado.Presentation.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/salud")]
public sealed class SaludController(AppDbContext db) : ControllerBase
{
    /// <summary>GET /api/v1/salud — sin clave: es lo primero que consulta un monitor externo.</summary>
    [HttpGet]
    [ProducesResponseType<SaludPublicaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<SaludPublicaDto>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SaludPublicaDto>> Get(CancellationToken ct)
    {
        var baseDeDatos = await db.Database.CanConnectAsync(ct);
        var dto = new SaludPublicaDto(baseDeDatos ? "ok" : "degradado", baseDeDatos, DateTime.UtcNow);
        return baseDeDatos ? Ok(dto) : StatusCode(StatusCodes.Status503ServiceUnavailable, dto);
    }
}
