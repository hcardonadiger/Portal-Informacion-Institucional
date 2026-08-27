using Diger.TramitesEstado.Application;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Infrastructure;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Web.Hubs;
using Diger.TramitesEstado.Web.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// ── Diagnóstico: capturar cualquier excepción que mate el proceso ──────────
var crashLogPath = Path.Combine(AppContext.BaseDirectory, "crash_log.txt");
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    var ex = e.ExceptionObject as Exception;
    var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] UNHANDLED (IsTerminating={e.IsTerminating}): {ex}\n";
    File.AppendAllText(crashLogPath, msg);
};
TaskScheduler.UnobservedTaskException += (_, e) =>
{
    var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] UNOBSERVED TASK: {e.Exception}\n";
    File.AppendAllText(crashLogPath, msg);
    e.SetObserved();
};

// ── Parámetros generales cargados de appsettings (editables sin recompilar) ─
var uploadsMaxRequestMb = builder.Configuration.GetValue("Uploads:MaxRequestMb", 50);
var uploadsMaxFormValueMb = builder.Configuration.GetValue("Uploads:MaxFormValueMb", 10);
Diger.TramitesEstado.Web.Common.UploadsConfig.TicketsMaxBytes =
    builder.Configuration.GetValue("Uploads:TicketsMaxMb", 10) * 1024L * 1024L;
Diger.TramitesEstado.Web.Common.UploadsConfig.ReunionesMaxBytes =
    builder.Configuration.GetValue("Uploads:ReunionesMaxMb", 5) * 1024L * 1024L;
if (builder.Configuration.GetSection("Uploads:ExtensionesPermitidas").Get<string[]>() is { Length: > 0 } extsDoc)
    Diger.TramitesEstado.Web.Common.UploadsConfig.ExtensionesPermitidas = extsDoc;
if (builder.Configuration.GetSection("Uploads:ExtensionesImagenesPermitidas").Get<string[]>() is { Length: > 0 } extsImg)
    Diger.TramitesEstado.Web.Common.UploadsConfig.ExtensionesImagenesPermitidas = extsImg;

Diger.TramitesEstado.Application.Expedientes.Seguimiento.SemaforoAvance.UmbralAvanzado =
    builder.Configuration.GetValue("Expedientes:Semaforo:UmbralAvanzado", 70);
Diger.TramitesEstado.Application.Expedientes.Seguimiento.SemaforoAvance.UmbralEnProceso =
    builder.Configuration.GetValue("Expedientes:Semaforo:UmbralEnProceso", 20);

Diger.TramitesEstado.Application.Common.Models.Paginacion.TamanoDefecto =
    builder.Configuration.GetValue("Paginacion:TamanoDefecto", 20);
Diger.TramitesEstado.Application.Common.Models.Paginacion.TamanoMaximo =
    builder.Configuration.GetValue("Paginacion:TamanoMaximo", 100);

var devMainPort = builder.Configuration.GetValue("Ports:DevMain", 49175);
var devCertPort = builder.Configuration.GetValue("Ports:DevCert", 49176);
var devHttpPort = builder.Configuration.GetValue("Ports:DevHttp", 49177);

builder.WebHost.ConfigureKestrel((context, options) =>
{
    options.Limits.MaxRequestBodySize = uploadsMaxRequestMb * 1024L * 1024L;

    if (context.HostingEnvironment.IsDevelopment())
    {
        // Puerto HTTPS principal (para navegación sin alertas)
        options.ListenLocalhost(devMainPort, listenOptions =>
        {
            listenOptions.UseHttps(httpsOptions =>
            {
                httpsOptions.ClientCertificateMode = Microsoft.AspNetCore.Server.Kestrel.Https.ClientCertificateMode.NoCertificate;
            });
        });

        // Puerto HTTPS de Autenticación (para pedir el certificado)
        options.ListenLocalhost(devCertPort, listenOptions =>
        {
            listenOptions.UseHttps(httpsOptions =>
            {
                httpsOptions.ClientCertificateMode = Microsoft.AspNetCore.Server.Kestrel.Https.ClientCertificateMode.AllowCertificate;

                // Hacks para Tokens Físicos (Bit4Id, etc) que fallan en TLS 1.3 o sin internet para CRL
                httpsOptions.CheckCertificateRevocation = false;
                httpsOptions.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;

                // En desarrollo, permitimos certificados autofirmados (el certificado dev local)
                if (context.HostingEnvironment.IsDevelopment())
                {
                    httpsOptions.ClientCertificateValidation = (certificate2, validationChain, policyErrors) => true;
                }
            });
        });

        // Puerto HTTP local
        options.ListenLocalhost(devHttpPort);
    }
    else
    {
        // En producción (si Kestrel es el servidor de borde, no IIS/Proxy)
        options.ConfigureHttpsDefaults(httpsOptions =>
        {
            httpsOptions.ClientCertificateMode = Microsoft.AspNetCore.Server.Kestrel.Https.ClientCertificateMode.NoCertificate;
        });
    }
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = uploadsMaxRequestMb * 1024L * 1024L;
    options.ValueLengthLimit = uploadsMaxFormValueMb * 1024 * 1024;
});

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var dataProtectionKeysPath = builder.Configuration.GetValue<string>("Storage:DataProtectionKeysPath") ?? @"C:\PortalDigital_Keys";
builder.Services.AddDataProtection()
    .SetApplicationName("PortalDigital")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

// Importador de expedientes desde el portal demo (Supabase) — usado por Admin/ImportarExpedientes
builder.Services.AddHttpClient<Diger.TramitesEstado.Web.Import.SupabaseExpedienteImporter>();
builder.Services.AddHttpClient<Diger.TramitesEstado.Web.Import.SupabaseMigracionScanner>();
builder.Services.AddHttpClient<Diger.TramitesEstado.Web.Import.SupabaseCatalogosImporter>();

// Configuración para permitir el reenvío de certificados si IIS actúa como Reverse Proxy
builder.Services.AddCertificateForwarding(options =>
{
    options.CertificateHeader = "X-ARR-ClientCert";
});

// ── Autenticación por cookie ──────────────────────────────────────────────
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opts =>
    {
        opts.LoginPath        = "/Cuenta/Login";
        opts.LogoutPath       = "/Cuenta/Logout";
        opts.AccessDeniedPath = "/Cuenta/Denegado";
        opts.ExpireTimeSpan   = TimeSpan.FromHours(builder.Configuration.GetValue("Auth:CookieExpirationHours", 8));
        opts.SlidingExpiration = true;
        
        // Compartir la cookie de sesión entre el subdominio cert.* y el dominio principal
        opts.Events = new CookieAuthenticationEvents
        {
            OnSigningIn = context =>
            {
                var host = context.Request.Host.Host;
                // Si el host es una IP pura (ej. 192.168.x.x), NO configuramos un dominio
                // porque los navegadores rechazan cookies de dominio ".192.168.x.x".
                // Las cookies por defecto se comparten entre puertos de la misma IP.
                if (host != "localhost" && host.Contains('.') && !System.Net.IPAddress.TryParse(host, out _))
                {
                    var mainDomain = host.StartsWith("cert.") ? host.Substring(5) : host;
                    context.CookieOptions.Domain = "." + mainDomain;
                }
                return Task.CompletedTask;
            }
        };
    });

// Todas las policies estáticas que vivían aquí (PuedeAdministrarCatalogo,
// PuedeGestionarContactos, PuedeGestionarTickets, PuedeAdministrarUsuarios,
// PuedeGestionarExpedientes, PuedeGestionarReuniones) ya se migraron a permisos dinámicos
// ([Permission] en cada página + matriz administrable en /Accesos/Permisos) — ver
// PermissionPolicyProvider: cualquier nombre de policy no registrado explícitamente se
// interpreta como una clave de Permiso (ej. "Expedientes.Crear") y se resuelve contra la
// matriz rol×permiso en BD.
// Catálogo de roles en memoria. Singleton porque AppDbContext lo consulta de forma
// SÍNCRONA al armar los filtros RLS; RolCatalogoLoader lo llena antes del primer request
// y debe quedar registrado ANTES de PermissionCatalogSyncService (los IHostedService
// arrancan en orden de registro y la sincronización del catálogo de permisos ya asume
// que los roles están cargados).
builder.Services.AddSingleton<Diger.TramitesEstado.Application.Common.Interfaces.IRolCatalogo,
                               Diger.TramitesEstado.Infrastructure.Security.RolCatalogo>();
builder.Services.AddHostedService<Diger.TramitesEstado.Web.Security.RolCatalogoLoader>();

builder.Services.AddAuthorization();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider,
                               Diger.TramitesEstado.Infrastructure.Security.PermissionPolicyProvider>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
                            Diger.TramitesEstado.Infrastructure.Security.PermissionAuthorizationHandler>();
builder.Services.AddSingleton<Diger.TramitesEstado.Application.Common.Interfaces.IPermissionCache,
                               Diger.TramitesEstado.Infrastructure.Security.PermissionCache>();
builder.Services.AddHostedService<Diger.TramitesEstado.Web.Security.PermissionCatalogSyncService>();
// DESPUÉS del sync: la siembra traduce la matriz por módulo a permisos por acción y necesita
// el catálogo ya poblado. Solo actúa la primera vez (ver la guarda en el propio servicio).
builder.Services.AddHostedService<Diger.TramitesEstado.Web.Security.PermisosSeedService>();


builder.Services.AddRazorPages(opts =>
{
    opts.RootDirectory = "/Pages";
    opts.Conventions.AuthorizeFolder("/");           // todo requiere sesión…
    opts.Conventions.AllowAnonymousToFolder("/Cuenta"); // …salvo login/logout
    opts.Conventions.AllowAnonymousToFolder("/Asistencia"); // …y el auto-registro público
    opts.Conventions.AllowAnonymousToPage("/Error");
})
.AddMvcOptions(options =>
{
    options.Filters.Add<Diger.TramitesEstado.Web.Common.ConsultorReadOnlyPageFilter>();
    options.Filters.Add<Diger.TramitesEstado.Web.Common.PermissionPageFilter>();
});

builder.Services.AddSignalR();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<Diger.TramitesEstado.Web.Common.AccesoModulosService>();
builder.Services.AddScoped<Diger.TramitesEstado.Web.Common.JerarquiaUiService>();
builder.Services.AddScoped<Diger.TramitesEstado.Web.Common.IInstitucionBrandingService,
                            Diger.TramitesEstado.Web.Common.InstitucionBrandingService>();
builder.Services.AddExceptionHandler<WebExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await DbSeeder.SeedUsuariosAsync(db, hasher);
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// ── Cabeceras de seguridad HTTP ───────────────────────────────────────────
// Previene XSS (CSP), Clickjacking (X-Frame-Options) y MIME sniffing.
// La CSP permite 'unsafe-inline' porque Razor Pages usa scripts/estilos inline;
// se puede endurecer progresivamente con nonces cuando se migre a AJAX/Fetch.
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"]        = "DENY";
    ctx.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self' wss: ws:;";
    await next();
});

app.UseStaticFiles();

// App_Data/uploads NO se publica como archivos estáticos.
//
// Hasta el 2026-08-26 acá había un UseStaticFiles montado en /uploads, y estaba ANTES de
// UseAuthentication: cualquiera con la URL bajaba el archivo sin sesión. Medido, no supuesto —
// un fetch con credentials:'omit' devolvió 200 y el contenido. Los nombres son GUID, que es
// oscuridad y no seguridad, y alcanzaba a los adjuntos de tickets, reuniones, compromisos y
// expedientes por igual.
//
// Ahora cada módulo sirve lo suyo por un handler que resuelve la entidad con su consulta normal
// —y por lo tanto pasa por el filtro de alcance— antes de tocar el disco. Ver ArchivosProtegidos.
// La carpeta se sigue creando acá porque es donde se guardan las subidas.
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data", "uploads"));

app.UseRouting();

// Debe estar antes de UseAuthentication para que el certificado esté disponible
app.UseCertificateForwarding();

app.UseAuthentication();
app.UseAuthorization();

// El bloqueo por URL directa lo hacía ModuloAccesoMiddleware, con un switch de 9 prefijos
// escrito a mano que se fue quedando atrás (11 de los 20 módulos del portal ya no estaban
// en la lista). Ahora lo hace PermissionPageFilter en cada handler, con el catálogo que se
// descubre por reflexión al arrancar — no hay lista que mantener.

app.UseExceptionHandler();
app.MapRazorPages();
app.MapHub<SoporteHub>("/hubs/soporte");

await app.RunAsync();

/// <summary>
/// Las top-level statements generan una clase Program interna, invisible desde otro ensamblado.
/// Esta declaración parcial la hace pública para que WebApplicationFactory&lt;Program&gt; pueda
/// levantar la aplicación en las pruebas de integración. No agrega comportamiento.
/// </summary>
public partial class Program { }
