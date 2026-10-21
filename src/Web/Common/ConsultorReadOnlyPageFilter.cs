using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Diger.TramitesEstado.Web.Common;

/// <summary>
/// Bloquea las peticiones de mutación para roles marcados como solo lectura en la tabla
/// Roles (antes era el rol "Consultor" hardcodeado). AppDbContext repite el bloqueo en
/// SaveChangesAsync como red de seguridad de última línea.
/// </summary>
public class ConsultorReadOnlyPageFilter(ICurrentUserService currentUser) : IAsyncPageFilter
{
    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var method = context.HttpContext.Request.Method;

        if (HttpMethods.IsPost(method) || HttpMethods.IsPut(method) ||
            HttpMethods.IsDelete(method) || HttpMethods.IsPatch(method))
        {
            if (currentUser.EsSoloLectura)
            {
                context.Result = new ForbidResult();
                return;
            }
        }

        await next();
    }

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
    {
        return Task.CompletedTask;
    }
}
