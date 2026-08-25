using Microsoft.AspNetCore.Diagnostics;

namespace Diger.TramitesEstado.Api.Middleware;

/// <summary>
/// La red de seguridad: cualquier excepción que llegue acá sale como un 500 con formato
/// <c>ProblemDetails</c> y sin decir nada de la máquina.
/// </summary>
/// <remarks>
/// <para>
/// <b>Antes distinguía cuatro tipos de excepción</b> —no encontrado, validación, regla de
/// negocio y el resto— porque venían de la capa de aplicación de PortalDigital. Esta API ya no
/// depende de esa capa y ninguna de las tres primeras puede ocurrir: no hay comandos, no hay
/// validadores y no hay reglas de negocio que romper. Lo único que queda es lo imprevisto.
/// </para>
/// <para>
/// <b>El «no encontrado» no pasa por acá y nunca pasó:</b> lo devuelve el controlador con un
/// <c>NotFound()</c> cuando el código no existe o no está publicado, que son la misma respuesta
/// a propósito.
/// </para>
/// <para>
/// <b>El detalle no se publica.</b> El mensaje de una excepción no controlada trae el nombre del
/// servidor, de la base o de la tabla — y esto es una API pública. El diagnóstico técnico va al
/// log, junto al identificador de traza que sí viaja en la respuesta: permite cruzar lo que vio
/// el consumidor con lo que quedó en el servidor sin contarle nada de la máquina.
/// </para>
/// </remarks>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        logger.LogError(ex, "Excepción no controlada: {Message}", ex.Message);

        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await ctx.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title  = "Error interno del servidor",
                Detail = "Ocurrió un error interno. Comunique el identificador de la traza al administrador.",
                Extensions = { ["traceId"] = ctx.TraceIdentifier }
            }, ct);

        return true;
    }
}
