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

builder.WebHost.ConfigureKestrel((context, options) =>
{
    if (context.HostingEnvironment.IsDevelopment())
    {
        // Puerto HTTPS principal (para navegación sin alertas)
        options.ListenLocalhost(49175, listenOptions =>
        {
            listenOptions.UseHttps(httpsOptions =>
            {
                httpsOptions.ClientCertificateMode = Microsoft.AspNetCore.Server.Kestrel.Https.ClientCertificateMode.NoCertificate;
            });
        });

        // Puerto HTTPS de Autenticación (para pedir el certificado)
        options.ListenLocalhost(49176, listenOptions =>
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
        options.ListenLocalhost(49177);
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

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddDataProtection()
    .SetApplicationName("PortalDigital")
    .PersistKeysToFileSystem(new DirectoryInfo(@"C:\PortalDigital_Keys"));

// Importador de expedientes desde el portal demo (Supabase) — usado por Admin/ImportarExpedientes
builder.Services.AddHttpClient<Diger.TramitesEstado.Web.Import.SupabaseExpedienteImporter>();

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
        opts.ExpireTimeSpan   = TimeSpan.FromHours(8);
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

builder.Services.AddAuthorizationBuilder()
    // Gestión: Administrador, Jefes y Empleado pueden crear/editar; el ALCANCE institucional
    // (filtro global + validación al crear) limita sobre qué registros pueden actuar.
    .AddPolicy("PuedeGestionarExpedientes", p => p.RequireRole(
        nameof(RolUsuario.Administrador), nameof(RolUsuario.JefeInstitucion), nameof(RolUsuario.JefeArea), nameof(RolUsuario.JefeUnidad), nameof(RolUsuario.Empleado)))
    .AddPolicy("PuedeAdministrarCatalogo", p => p.RequireRole(
        nameof(RolUsuario.Administrador)))
    .AddPolicy("PuedeGestionarContactos", p => p.RequireRole(
        nameof(RolUsuario.Administrador), nameof(RolUsuario.JefeInstitucion), nameof(RolUsuario.JefeArea), nameof(RolUsuario.JefeUnidad), nameof(RolUsuario.Empleado)))
    .AddPolicy("PuedeGestionarReuniones", p => p.RequireRole(
        nameof(RolUsuario.Administrador), nameof(RolUsuario.JefeInstitucion), nameof(RolUsuario.JefeArea), nameof(RolUsuario.JefeUnidad), nameof(RolUsuario.Empleado)))
    .AddPolicy("PuedeGestionarTickets", p => p.RequireRole(
        nameof(RolUsuario.Administrador), nameof(RolUsuario.JefeInstitucion), nameof(RolUsuario.JefeArea), nameof(RolUsuario.JefeUnidad), nameof(RolUsuario.Empleado)))
    .AddPolicy("PuedeAdministrarUsuarios", p => p.RequireRole(
        nameof(RolUsuario.Administrador)));


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
app.UseRouting();

// Debe estar antes de UseAuthentication para que el certificado esté disponible
app.UseCertificateForwarding();

app.UseAuthentication();
app.UseAuthorization();

// Control de accesos por rol a las opciones del portal (bloquea por URL directa).
app.UseMiddleware<Diger.TramitesEstado.Web.Common.ModuloAccesoMiddleware>();

app.UseExceptionHandler();
app.MapRazorPages();
app.MapHub<SoporteHub>("/hubs/soporte");

await app.RunAsync();
