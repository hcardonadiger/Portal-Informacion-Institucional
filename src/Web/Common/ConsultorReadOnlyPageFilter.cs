using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Diger.TramitesEstado.Web.Common;

public class ConsultorReadOnlyPageFilter : IAsyncPageFilter
{
    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var method = context.HttpContext.Request.Method;

        if (HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || 
            HttpMethods.IsDelete(method) || HttpMethods.IsPatch(method))
        {
            var user = context.HttpContext.User;
            var activeRol = user.FindFirst("diger:rol")?.Value;

            if (activeRol == "Consultor")
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
