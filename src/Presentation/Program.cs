using System.Threading.RateLimiting;
using Diger.TramitesEstado.Application;
using Diger.TramitesEstado.Infrastructure;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Presentation.Middleware;
using Diger.TramitesEstado.Presentation.Security;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ── Puertos de desarrollo, con red de seguridad ──────────────────────────────
// Si nadie dijo en qué puertos escuchar —porque Visual Studio no aplicó el perfil
// de launchSettings.json, algo que pasa cuando su caché queda vieja— ASP.NET usa
// el 5000 por omisión. Y como HondurasÁgil haría lo mismo, los dos acaban en el
// mismo puerto y el segundo no arranca.
//
// Esto lo evita: sin URLs explícitas, se fijan las de este proyecto. Con URLs
// explícitas (perfil aplicado, --urls, ASPNETCORE_URLS) no se toca nada.
// Fuera de Development tampoco: en IIS manda el módulo, no Kestrel.
// NO se condiciona a IsDevelopment(), y la razon importa: cuando Visual Studio no
// aplica el perfil tampoco define ASPNETCORE_ENVIRONMENT, asi que la aplicacion se
// cree en Production — justo el caso que hay que cubrir. Medido el 18-08-2026.
// Lo que si se respeta es IIS: alli manda el modulo y Kestrel no elige puertos.
var hospedadoEnIis =
    !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_IIS_PHYSICAL_PATH")) ||
    !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_PORT"));

if (!hospedadoEnIis && string.IsNullOrWhiteSpace(builder.Configuration["urls"]))
{
    var puertoHttps = builder.Configuration.GetValue("Ports:DevHttps", 7199);
    var puertoHttp  = builder.Configuration.GetValue("Ports:DevHttp",  5199);

    builder.WebHost.ConfigureKestrel(opciones =>
    {
        opciones.ListenLocalhost(puertoHttp);
        opciones.ListenLocalhost(puertoHttps, listen => listen.UseHttps());
    });
}

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
{
    opts.SwaggerDoc("v1", new()
    {
        Title   = "DIGER — API pública de trámites (v1)",
        Version = "v1",
        Description =
            "Publica el inventario oficial de trámites del Estado para que otros sistemas lo " +
            "consuman. Hoy su único consumidor es HondurasÁgil.\n\n" +
            "**Solo lectura.** No hay POST, PUT ni DELETE, y no los habrá en la v1: los trámites " +
            "se editan en PortalDigital, no por aquí.\n\n" +
            "**Solo lo publicado.** Un trámite sin publicar no existe para esta API. Por eso un " +
            "código no publicado devuelve 404 igual que uno inexistente: distinguirlos delataría " +
            "qué códigos hay.\n\n" +
            "### Cómo autenticarse\n" +
            "Pulse **Authorize** y pegue la clave. Viaja en la cabecera `X-Api-Key`. " +
            "`/api/v1/salud` es la única ruta que no la pide, para que un monitor externo no " +
            "tenga que custodiar un secreto.\n\n" +
            "### Cómo sincronizar\n" +
            "Son dos rutas que se complementan, y hacen falta las dos:\n\n" +
            "- `/cambios?desde=` trae lo que cambió. Es el día a día.\n" +
            "- `/codigos-publicados` trae todos los códigos vivos. Sirve para **retirar bajas**, " +
            "que es lo que `/cambios` no puede reportar: un trámite dado de baja no deja ninguna " +
            "fila que devolver.\n\n" +
            "Conviene además forzar un ciclo completo cada tanto. `/cambios` se apoya en la fecha " +
            "de modificación, así que una publicación hecha con un UPDATE directo contra la base " +
            "no la toca y sería invisible para siempre."
    });

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
    // Va por filtro y no global a propósito: puesto global, /api/v1/salud aparecía con
    // candado siendo anónima, y la documentación mentía sobre el contrato.
    opts.OperationFilter<Diger.TramitesEstado.Presentation.Swagger.RequisitoApiKeyFilter>();

    // La documentación de los controladores. Sin esto Swagger enseña las rutas pero no
    // explica ninguna, que es justo lo que hace inútil un Swagger.
    var xml = Path.Combine(AppContext.BaseDirectory,
                           $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml)) opts.IncludeXmlComments(xml, includeControllerXmlComments: true);
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

// Qué base está sirviendo esta API. En el mismo servidor conviven Ensayo y Producción, y una
// API sin interfaz no tiene dónde enseñarlo: el arranque es el único momento en que se ve.
// SqlConnectionStringBuilder en vez de partir la cadena a mano, para no arrastrar la contraseña.
var baseDeDatos = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
    app.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty).InitialCatalog;
if (string.IsNullOrWhiteSpace(baseDeDatos)) baseDeDatos = "sin configurar";
app.Logger.LogInformation("API de PortalDigital sirviendo la base {BaseDeDatos}", baseDeDatos);

// Auto-migrate en desarrollo — apagado por defecto (ver Datos:AplicarMigracionesAlArrancar
// en src/Web/Program.cs para la justificación completa).
if (app.Environment.IsDevelopment() && app.Configuration.GetValue("Datos:AplicarMigracionesAlArrancar", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// Swagger publica el mapa completo de la superficie. En Development siempre; fuera de
// Development solo si alguien lo enciende a propósito — para que el consumidor pueda
// consultarlo en un entorno de integración sin que quede expuesto en producción por olvido.
var publicarSwagger = app.Environment.IsDevelopment() ||
                      app.Configuration.GetValue("PortalDigitalApi:PublicarSwagger", false);

if (publicarSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(ui =>
    {
        ui.SwaggerEndpoint("/swagger/v1/swagger.json", $"API pública de trámites v1 — base {baseDeDatos}");
        ui.DocumentTitle = "DIGER — API pública de trámites (v1)";

        // Las rutas salen plegadas: son siete y así se ven todas de un vistazo, que es
        // justo lo que uno quiere al abrir esto por primera vez.
        ui.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
        ui.DefaultModelsExpandDepth(-1);
        ui.EnableTryItOutByDefault();
        // Deja la clave puesta entre recargas: sin esto hay que volver a pegarla cada vez.
        ui.EnablePersistAuthorization();
    });

    // Entrar al puerto a secas lleva a la documentación. Antes la raíz devolvía un 404
    // vacío y parecía que no hubiera nada montado — que es exactamente la impresión que
    // da un servidor caído.
    app.MapGet("/", () => Results.Redirect("/swagger"))
       .ExcludeFromDescription()
       .AllowAnonymous();
}

app.UseExceptionHandler();
app.UseResponseCompression();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
