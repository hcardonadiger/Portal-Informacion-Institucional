using Diger.TramitesEstado.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

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

            // La base rechazando un dato es un error del usuario, no una falla del portal. Iba al
            // comodín y salía como «Ocurrió un error inesperado»: quien creaba un trámite SIGER con
            // un código ya usado perdía el formulario sin enterarse de qué campo corregir.
            DbUpdateConcurrencyException => (409, "No se pudo guardar",
                "Otro usuario modificó este registro mientras usted lo editaba. " +
                "Vuelva a abrirlo para ver los datos actuales."),
            DbUpdateException   due => (409, "No se pudo guardar", PorQueLoRechazoLaBase(due)),

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

    /// <summary>
    /// Traduce el rechazo de la base a algo accionable, sin filtrar el mensaje del motor.
    ///
    /// <para>Se lee el texto de la excepción interna porque el código de error viene tipado por
    /// proveedor —<c>SqlException.Number</c> 2601/2627 en SQL Server, <c>SqliteException</c> con
    /// otro código en las pruebas— y este manejador sirve a los dos. El texto de la restricción es
    /// el único denominador común.</para>
    ///
    /// <para>Es un mensaje genérico a propósito: no dice qué columna chocó. Cada formulario debe
    /// validar sus propias restricciones antes de llegar acá —como ya hace el editor SIGER con
    /// IdSiger y Código—; esto es la red que atrapa la carrera entre dos usuarios y los formularios
    /// que todavía no validan.</para>
    /// </summary>
    private static string PorQueLoRechazoLaBase(DbUpdateException ex)
    {
        var texto = ex.InnerException?.Message ?? ex.Message;

        bool Dice(string aguja) => texto.Contains(aguja, StringComparison.OrdinalIgnoreCase);

        if (Dice("UNIQUE") || Dice("duplicate key"))
            return "Ya existe un registro con ese valor en un campo que no admite repetidos. " +
                   "Revise los códigos e identificadores del formulario.";

        if (Dice("Cannot insert the value NULL") || Dice("NOT NULL"))
            return "Falta completar un campo obligatorio.";

        if (Dice("CHECK constraint") || Dice("conflicted with the CHECK"))
            return "Alguno de los datos no cumple una regla de la base. Revise el formulario.";

        if (Dice("REFERENCE constraint") || Dice("FOREIGN KEY"))
            return "El registro está relacionado con otros datos, o apunta a algo que ya no existe.";

        return "La base de datos rechazó el cambio. Revise los datos del formulario.";
    }
}
