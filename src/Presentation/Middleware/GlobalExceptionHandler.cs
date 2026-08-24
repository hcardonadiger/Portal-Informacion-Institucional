using Diger.TramitesEstado.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Diger.TramitesEstado.Presentation.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx,
        Exception ex,
        CancellationToken ct)
    {
        logger.LogError(ex, "Excepción no controlada: {Message}", ex.Message);

        var (status, title) = ex switch
        {
            NotFoundException   => (StatusCodes.Status404NotFound,           "Recurso no encontrado"),
            ValidationException => (StatusCodes.Status400BadRequest,         "Error de validación"),
            DomainException     => (StatusCodes.Status409Conflict,           "Regla de negocio"),
            _                   => (StatusCodes.Status500InternalServerError, "Error interno del servidor")
        };

        // El detalle de un 500 NO se publica. `ex.Message` de una excepción no controlada
        // trae el nombre del servidor, de la base o de la tabla — y esto es una API pública.
        // Las otras tres son excepciones de negocio: su mensaje está escrito para leerse.
        // El diagnóstico técnico va al log de arriba, junto al TraceId de la petición.
        var detail = ex switch
        {
            ValidationException ve => string.Join("; ", ve.Errors.Select(e => e.ErrorMessage)),
            NotFoundException or DomainException => ex.Message,
            _ => "Ocurrió un error interno. Comunique el identificador de la traza al administrador."
        };

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = status,
                Title  = title,
                Detail = detail,
                // Permite cruzar lo que ve el consumidor con lo que quedó en el log del servidor
                // sin contarle nada de la máquina.
                Extensions = { ["traceId"] = ctx.TraceIdentifier }
            }, ct);

        return true;
    }
}
