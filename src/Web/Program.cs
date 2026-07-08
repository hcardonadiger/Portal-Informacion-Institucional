using Diger.TramitesEstado.Application;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Infrastructure;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Web.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// Importador de expedientes desde el portal demo (Supabase) — usado por Admin/ImportarExpedientes
builder.Services.AddHttpClient<Diger.TramitesEstado.Web.Import.SupabaseExpedienteImporter>();

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
    });

builder.Services.AddAuthorizationBuilder()
    // Gestión: Admin/Coordinador/Técnico pueden crear/editar; el ALCANCE institucional
    // (filtro global + validación al crear) limita sobre qué registros pueden actuar.
    .AddPolicy("PuedeGestionarExpedientes", p => p.RequireRole(
        nameof(RolUsuario.Administrador), nameof(RolUsuario.Coordinador), nameof(RolUsuario.Tecnico)))
    .AddPolicy("PuedeAdministrarCatalogo", p => p.RequireRole(
        nameof(RolUsuario.Administrador)))
    .AddPolicy("PuedeGestionarContactos", p => p.RequireRole(
        nameof(RolUsuario.Administrador), nameof(RolUsuario.Coordinador), nameof(RolUsuario.Tecnico)))
    .AddPolicy("PuedeGestionarReuniones", p => p.RequireRole(
        nameof(RolUsuario.Administrador), nameof(RolUsuario.Coordinador), nameof(RolUsuario.Tecnico)))
    .AddPolicy("PuedeGestionarTickets", p => p.RequireRole(
        nameof(RolUsuario.Administrador), nameof(RolUsuario.Coordinador), nameof(RolUsuario.Tecnico)))
    .AddPolicy("PuedeAdministrarUsuarios", p => p.RequireRole(
        nameof(RolUsuario.Administrador)));


builder.Services.AddRazorPages(opts =>
{
    opts.RootDirectory = "/Pages";
    opts.Conventions.AuthorizeFolder("/");           // todo requiere sesión…
    opts.Conventions.AllowAnonymousToFolder("/Cuenta"); // …salvo login/logout
    opts.Conventions.AllowAnonymousToFolder("/Asistencia"); // …y el auto-registro público
    opts.Conventions.AllowAnonymousToPage("/Error");
});

builder.Services.AddScoped<Diger.TramitesEstado.Web.Common.AccesoModulosService>();
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
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Control de accesos por rol a las opciones del portal (bloquea por URL directa).
app.UseMiddleware<Diger.TramitesEstado.Web.Common.ModuloAccesoMiddleware>();

app.UseExceptionHandler();
app.MapRazorPages();

await app.RunAsync();
