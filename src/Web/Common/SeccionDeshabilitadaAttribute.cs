using Microsoft.AspNetCore.Mvc.Filters;

namespace Diger.TramitesEstado.Web.Common;

/// <summary>
/// Deja una página fuera de servicio sin borrar su código: responde 404 en cualquier entorno y
/// para cualquier rol. Se usa en secciones que se trasladan a otro aplicativo o que aún no se
/// liberan; para reactivarlas basta con quitar el atributo y restaurar su enlace en el menú.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class SeccionDeshabilitadaAttribute : Attribute, IAsyncPageFilter
{
    public Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        context.Result = new NotFoundResult();
        return Task.CompletedTask;
    }

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;
}
