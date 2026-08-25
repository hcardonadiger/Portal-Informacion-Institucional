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
    /// <summary>Catálogo de trámites publicados, paginado.</summary>
    /// <remarks>
    /// Devuelve la ficha resumida: lo justo para pintar una lista. Para el detalle completo
    /// —pasos, requisitos, entregables, lugares y enlaces— hay que pedir cada trámite por su
    /// código.
    ///
    /// Los filtros se combinan con Y, no con O: pedir institución y modalidad a la vez
    /// devuelve los que cumplen las dos cosas.
    /// </remarks>
    /// <param name="busqueda">Texto libre. Es coincidencia parcial sobre <b>nombre, descripción
    /// y objetivo</b>, y no distingue tildes ni mayúsculas: «migracion» encuentra «Migración».
    /// Esa insensibilidad la garantiza la colación de esas columnas, no el código.
    /// <b>No busca por código ni por institución</b>: para eso están el parámetro
    /// <c>institucion</c> y la ruta de detalle por código.</param>
    /// <param name="categoria">Id numérico de la categoría. La lista está en /api/v1/categorias.</param>
    /// <param name="institucion">Sigla de la institución, tal como sale en /api/v1/instituciones
    /// (por ejemplo INPREMA). No es el nombre largo.</param>
    /// <param name="modalidad">Presencial, Virtual o Hibrido — sin tilde y con esa grafía
    /// exacta. Cuidado con una asimetría deliberada: <c>modalidad=Virtual</c> devuelve
    /// **también los híbridos**, porque un trámite híbrido también se puede hacer en línea y
    /// filtrar solo por Virtual subestimaría cuántos trámites hay en línea.
    /// <c>modalidad=Hibrido</c>, en cambio, devuelve solo híbridos.</param>
    /// <param name="soloGratuitos">Solo los que no cuestan nada.</param>
    /// <param name="soloEnSol">Solo los que ya se pueden hacer en línea.</param>
    /// <param name="soloFichasCompletas">Solo los que tienen categoría, modalidad, tiempo y
    /// costo. Es el filtro que debe usar un portal de cara al ciudadano: sin él pueden salir
    /// fichas sin plazo ni costo, que al ciudadano le sirven de poco.</param>
    /// <param name="orden">Solo se reconoce <c>nombre</c>, que ordena de la A a la Z.
    /// Cualquier otro valor —y no mandar ninguno— pone primero los marcados como populares y
    /// dentro de cada grupo ordena por nombre. No existe orden por institución ni por tiempo:
    /// pedirlos no da error, simplemente cae en ese orden por omisión.</param>
    /// <param name="pagina">Desde 1. Un número menor se trata como 1.</param>
    /// <param name="tamano">Entre 1 y 100. Un valor fuera de ese intervalo no da error, pero
    /// tampoco se recorta: **vuelve al valor por omisión, 20**. Pedir 500 devuelve 20, no 100.</param>
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

    /// <summary>Ficha completa de un trámite, por su código.</summary>
    /// <remarks>
    /// La identidad pública es el **código** (por ejemplo 603-019), no el Id interno. El Id
    /// interno no viaja nunca: es un detalle de esta base y podría cambiar.
    ///
    /// Trae el detalle entero: pasos, requisitos, entregables, lugares de atención y enlaces.
    /// </remarks>
    /// <param name="codigo">Código del trámite, tal como aparece en el catálogo.</param>
    /// <response code="404">No existe, o no está publicado. **Son el mismo código a
    /// propósito**: distinguirlos permitiría averiguar qué códigos existen sin verlos.</response>
    [HttpGet("{codigo}")]
    [ProducesResponseType<TramiteDetallePublicoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TramiteDetallePublicoDto>> Detalle(string codigo, CancellationToken ct)
    {
        var dto = await sender.Send(new GetTramitePublicoQuery(codigo), ct);
        return dto is null ? NotFound() : Ok(dto);
    }
}
