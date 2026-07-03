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

        var detail = ex is ValidationException ve
            ? string.Join("; ", ve.Errors.Select(e => e.ErrorMessage))
            : ex.Message;

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Title = title, Detail = detail }, ct);

        return true;
    }
}
