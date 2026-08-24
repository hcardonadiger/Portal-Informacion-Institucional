using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Diger.TramitesEstado.Presentation.Swagger;

/// <summary>
/// Marca con clave solo las operaciones que de verdad la exigen.
/// </summary>
/// <remarks>
/// Antes el requisito se declaraba global y <c>/api/v1/salud</c> salía con candado siendo
/// anónima. Una documentación que miente sobre el contrato es peor que no tenerla: el
/// consumidor programa la clave donde no hace falta y luego no entiende por qué su monitor
/// externo necesita un secreto.
/// </remarks>
public sealed class RequisitoApiKeyFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadatos = context.MethodInfo.DeclaringType is null
            ? []
            : context.MethodInfo.DeclaringType.GetCustomAttributes(true)
                .Concat(context.MethodInfo.GetCustomAttributes(true))
                .ToArray();

        // AllowAnonymous gana siempre, esté en el método o en el controlador — es la misma
        // regla que aplica ASP.NET al decidir si pide autenticación.
        if (metadatos.OfType<AllowAnonymousAttribute>().Any()) return;
        if (!metadatos.OfType<AuthorizeAttribute>().Any()) return;

        var esquema = new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id   = "ApiKey"
            }
        };

        operation.Security = [new OpenApiSecurityRequirement { [esquema] = [] }];

        operation.Responses.TryAdd("401", new OpenApiResponse
        {
            Description = "Falta la cabecera X-Api-Key, o la clave no es válida."
        });
    }
}
