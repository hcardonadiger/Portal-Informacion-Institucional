using System.Reflection;
using Diger.TramitesEstado.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Diger.TramitesEstado.Web.Common;

/// <summary>
/// Bloquea las peticiones de mutación para roles marcados como solo lectura en la tabla
/// Roles (antes era el rol "Consultor" hardcodeado). AppDbContext repite el bloqueo en
/// SaveChangesAsync como red de seguridad de última línea.
///
/// <para><b>Con dos excepciones declaradas</b>, y solo esas dos. El filtro decidía únicamente por
/// el verbo HTTP, de modo que devolvía <c>Forbid</c> a todo POST — incluido el de
/// <c>/Cuenta/Logout</c>, que no guarda nada: un rol de solo lectura no podía ni cerrar sesión.
/// El mismo corte alcanzaba a las diez páginas de autoservicio marcadas con
/// <c>[PermisoNoRequerido]</c>, así que tampoco podía cambiar su propia contraseña.</para>
///
/// <para>Los marcadores se leen igual que en <see cref="PermissionPageFilter"/> —del handler y,
/// si no, de su clase— para que las dos capas de seguridad decidan sobre lo mismo y no haya que
/// razonar dos modelos distintos. Que la excepción exija un atributo <b>escrito a propósito</b>
/// es lo que la mantiene estrecha: una página nueva que mute datos nace cerrada.</para>
/// </summary>
public class ConsultorReadOnlyPageFilter(ICurrentUserService currentUser) : IAsyncPageFilter
{
    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var method = context.HttpContext.Request.Method;

        if (HttpMethods.IsPost(method) || HttpMethods.IsPut(method) ||
            HttpMethods.IsDelete(method) || HttpMethods.IsPatch(method))
        {
            if (currentUser.EsSoloLectura && !EsExcepcionDeclarada(context))
            {
                context.Result = new ForbidResult();
                return;
            }
        }

        await next();
    }

    /// <summary>
    /// Las dos salidas: <c>[AllowAnonymous]</c> —lo que no necesita sesión no puede depender del
    /// rol de la sesión: login, logout, restablecer contraseña— y <c>[PermisoNoRequerido]</c>
    /// —autoservicio de la propia cuenta—.
    ///
    /// <para>Se mira el atributo, no la convención: <c>AllowAnonymousToFolder("/Cuenta")</c>
    /// exime a la carpeta entera en el pipeline de autorización, y apoyarse en eso abriría de
    /// golpe páginas que nadie decidió abrir.</para>
    /// </summary>
    private static bool EsExcepcionDeclarada(PageHandlerExecutingContext context)
    {
        var handler = context.HandlerMethod?.MethodInfo;
        if (handler is null) return false;

        return Tiene<AllowAnonymousAttribute>(handler)
            || Tiene<PermisoNoRequeridoAttribute>(handler);
    }

    private static bool Tiene<T>(MethodInfo handler) where T : Attribute =>
        (handler.GetCustomAttribute<T>() ?? handler.DeclaringType?.GetCustomAttribute<T>()) is not null;

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
    {
        return Task.CompletedTask;
    }
}
