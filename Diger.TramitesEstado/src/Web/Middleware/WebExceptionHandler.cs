using Diger.TramitesEstado.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace Diger.TramitesEstado.Web.Middleware;

public sealed class WebExceptionHandler(
    ILogger<WebExceptionHandler> logger,
    IHostEnvironment env)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx, Exception ex, CancellationToken ct)
    {
        logger.LogError(ex, "Excepción no controlada: {Message}", ex.Message);

        var (status, title, detail) = ex switch
        {
            NotFoundException   nfe => (404, "Recurso no encontrado", nfe.Message),
            ValidationException ve  => (400, "Error de validación",
                string.Join("; ", ve.Errors.Select(e => e.ErrorMessage))),
            DomainException     de  => (409, "Regla de negocio", de.Message),
            _ when env.IsDevelopment() => (500, "Error interno",
                $"{ex.GetType().Name}: {ex.Message}"),
            _                       => (500, "Error interno", "Ocurrió un error inesperado.")
        };

        // Para solicitudes AJAX/API devolvemos JSON; para navegación normal redirigimos
        if (ctx.Request.Headers.Accept.Any(h => h!.Contains("application/json")))
        {
            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(
                new { title, detail, status }, ct);
            return true;
        }

        ctx.Response.Redirect($"/Error?code={status}&msg={Uri.EscapeDataString(detail)}");
        return true;
    }
}
