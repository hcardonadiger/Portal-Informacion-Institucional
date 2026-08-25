using Microsoft.AspNetCore.Authorization;

namespace Diger.TramitesEstado.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/salud")]
public sealed class SaludController(ApiDbContext db) : ControllerBase
{
    /// <summary>Si la API responde y si alcanza su base de datos.</summary>
    /// <remarks>
    /// **La única ruta sin clave**, y es a propósito: un monitor externo no debería tener que
    /// custodiar un secreto para comprobar que el servicio está en pie.
    ///
    /// Devuelve también <c>horaServidor</c>. Un consumidor debe usar esa hora, y no la suya,
    /// como referencia para sincronizar.
    ///
    /// Si <c>baseDeDatos</c> viene en <c>false</c> el problema es la cadena de conexión, no
    /// la API: el proceso está vivo pero no puede servir datos.
    /// </remarks>
    /// <response code="503">La API está en pie pero no alcanza su base de datos.</response>
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
