using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Diger.TramitesEstado.Web.Common;

/// <summary>
/// Da granularidad por handler específico (OnGet/OnPost individual), no solo por página.
/// [Authorize(Policy=...)] de ASP.NET Core solo se puede aplicar a nivel de clase/endpoint;
/// este filtro corre DESPUÉS de seleccionar el handler (igual que ConsultorReadOnlyPageFilter,
/// mismo mecanismo) y por eso sí puede leer context.HandlerMethod.MethodInfo. Si el handler
/// tiene su propio [Permission], se usa esa clave; si no, se cae a la de la clase (si tiene).
/// Sin ninguna de las dos, este filtro no actúa — el gateo de la página, si existe, debe
/// venir de [Authorize] a nivel de clase como siempre.
/// </summary>
public sealed class PermissionPageFilter(IAuthorizationService authz) : IAsyncPageFilter
{
    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var method = context.HandlerMethod?.MethodInfo;
        var permiso = method?.GetCustomAttribute<PermissionAttribute>()
                   ?? method?.DeclaringType?.GetCustomAttribute<PermissionAttribute>();

        if (permiso is not null)
        {
            var resultado = await authz.AuthorizeAsync(context.HttpContext.User, permiso.Clave);
            if (!resultado.Succeeded)
            {
                // Se deja anotado qué permiso faltó para que /Cuenta/Denegado lo pueda decir.
                // Va por TempData porque ForbidResult provoca un redirect (AccessDeniedPath) y
                // el contexto del request original se pierde: el usuario llega a otra página.
                var tempData = context.HttpContext.RequestServices
                    .GetRequiredService<ITempDataDictionaryFactory>()
                    .GetTempData(context.HttpContext);

                tempData["PermisoRequerido"] = permiso.Clave;
                tempData["PermisoNombre"]    = permiso.Nombre;

                context.Result = new ForbidResult();
                return;
            }
        }

        await next();
    }

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;
}
