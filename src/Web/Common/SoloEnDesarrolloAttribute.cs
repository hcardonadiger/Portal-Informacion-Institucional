using Microsoft.AspNetCore.Mvc.Filters;

namespace Diger.TramitesEstado.Web.Common;

/// <summary>
/// Restringe una página al entorno de Desarrollo. Fuera de él responde 404 —como si la página
/// no existiera— para que las herramientas internas (migración/importación de datos) no queden
/// expuestas en producción ni escribiendo la URL a mano.
/// Se aplica sobre la clase del PageModel y cubre todos sus handlers (GET y POST).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class SoloEnDesarrolloAttribute : Attribute, IAsyncPageFilter
{
    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var env = context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();

        if (!env.IsDevelopment())
        {
            context.Result = new NotFoundResult();
            return;
        }

        await next();
    }

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;
}
