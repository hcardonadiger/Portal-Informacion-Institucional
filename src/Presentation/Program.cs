using System.Threading.RateLimiting;
using Diger.TramitesEstado.Application;
using Diger.TramitesEstado.Infrastructure;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Presentation.Middleware;
using Diger.TramitesEstado.Presentation.Security;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
{
    opts.SwaggerDoc("v1", new() { Title = "DIGER — Trámites Estado API", Version = "v1" });
    var esquemaApiKey = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = ApiKeyAuthenticationOptions.HeaderName,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Clave estática del cliente de la API pública (v1).",
        Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "ApiKey" }
    };
    opts.AddSecurityDefinition("ApiKey", esquemaApiKey);
    // Sin esto, "Authorize" en la UI no adjunta la cabecera a las peticiones: declarar el
    // esquema no basta, hay que decirle a Swagger qué operaciones lo requieren.
    opts.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        { esquemaApiKey, Array.Empty<string>() }
    });
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// El catálogo de roles lo necesita CurrentUserService (Infrastructure) para construirse,
// aunque esta API no use cookies/roles — sin esto el host no arranca (ver RolCatalogoLoader).
builder.Services.AddSingleton<Diger.TramitesEstado.Application.Common.Interfaces.IRolCatalogo,
                               Diger.TramitesEstado.Infrastructure.Security.RolCatalogo>();
builder.Services.AddHostedService<Diger.TramitesEstado.Presentation.Security.RolCatalogoLoader>();
// El validador estricto del ServiceProvider construye TODOS los handlers registrados al
// arrancar, incluidos los de comandos de administración de permisos que esta API nunca
// invoca — pero igual necesita que sus dependencias resuelvan.
builder.Services.AddSingleton<Diger.TramitesEstado.Application.Common.Interfaces.IPermissionCache,
                               Diger.TramitesEstado.Infrastructure.Security.PermissionCache>();

// ── API pública v1 (Fase 4 del plan de la Ventanilla Digital) ──────────────
builder.Services
    .AddAuthentication(ApiKeyAuthenticationOptions.Scheme)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.Scheme, _ => { });
builder.Services.AddAuthorization();

builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    opts.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});

// Pública de hecho aunque lleve clave: un solo cliente conocido, límite generoso por si
// reintenta agresivamente durante la sincronización incremental.
builder.Services.AddRateLimiter(opts =>
{
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Request.Headers[ApiKeyAuthenticationOptions.HeaderName].ToString() is { Length: > 0 } clave ? clave : "anonimo",
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 300,
                QueueLimit = 0
            }));
});

var app = builder.Build();

// Auto-migrate en desarrollo — apagado por defecto (ver Datos:AplicarMigracionesAlArrancar
// en src/Web/Program.cs para la justificación completa).
if (app.Environment.IsDevelopment() && app.Configuration.GetValue("Datos:AplicarMigracionesAlArrancar", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseExceptionHandler();
app.UseResponseCompression();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
