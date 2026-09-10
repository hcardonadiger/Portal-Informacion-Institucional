using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Levanta el portal completo en memoria para las pruebas de integración.
///
/// Tres decisiones que vale la pena entender antes de tocar esto:
///
/// 1. **SQLite en memoria, no el provider InMemory.** Los filtros RLS de AppDbContext usan
///    subconsultas (<c>Areas.Any(...)</c>) que el provider InMemory no traduce. Además hace
///    falta mantener viva la conexión: con SQLite en memoria, la base existe mientras haya
///    una conexión abierta.
///
/// 2. **Ambiente "Testing".** Program.cs corre <c>MigrateAsync</c> y el seed de usuarios solo
///    en Development. Con otro ambiente no se ejecutan, y el esquema lo crea acá
///    <c>EnsureCreated</c> — las migraciones son de SQL Server y no aplican sobre SQLite.
///
/// 3. **PermissionCatalogSyncService se deja correr.** Es el que descubre las claves por
///    reflexión sobre los PageModel, así que las pruebas se ejecutan contra el catálogo REAL
///    del código, no contra una lista escrita a mano que se desactualizaría. En cambio
///    PermisosSeedService se quita: sembraría desde RolModuloAccesos (vacío acá) y dejaría
///    concesiones impredecibles; cada prueba otorga lo que necesita.
/// </summary>
public sealed class PortalFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _conexion;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // No alcanza con quitar DbContextOptions: desde EF Core 9, AddDbContext guarda
            // también la acción de configuración como IDbContextOptionsConfiguration<T>, y esa
            // sigue aplicando UseSqlServer sobre las mismas opciones. Si queda, EF ve dos
            // providers registrados y falla al primer query.
            var registrosDelContexto = services.Where(s =>
                    s.ServiceType == typeof(DbContextOptions<AppDbContext>)
                 || s.ServiceType == typeof(DbContextOptions)
                 || s.ServiceType == typeof(AppDbContext)
                 || (s.ServiceType.IsGenericType
                     && s.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration")
                     && s.ServiceType.GenericTypeArguments.Contains(typeof(AppDbContext))))
                .ToList();

            foreach (var registro in registrosDelContexto)
                services.Remove(registro);

            _conexion = new SqliteConnection("DataSource=:memory:");
            _conexion.Open();

            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_conexion));

            // La siembra automática de permisos haría impredecible el estado inicial.
            var siembra = services.FirstOrDefault(s =>
                s.ServiceType == typeof(IHostedService) &&
                s.ImplementationType == typeof(Diger.TramitesEstado.Web.Security.PermisosSeedService));

            if (siembra is not null) services.Remove(siembra);

            services.AddAuthentication(TestAuthHandler.Esquema)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Esquema, _ => { });
        });
    }

    /// <summary>Id del usuario sembrado para cada rol; la clave vacía es el usuario sin rol.</summary>
    private readonly Dictionary<string, Guid> _usuarios = new(StringComparer.OrdinalIgnoreCase);

    private const string SinRol = "";

    /// <summary>Crea el esquema y siembra los roles base y un usuario por rol. Llamar una vez
    /// por fixture.</summary>
    public async Task PrepararAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.EnsureCreatedAsync();

        if (!await db.Roles.AnyAsync())
        {
            db.Roles.AddRange(
                Rol.Crear("Administrador", "Administrador", NivelAlcance.Global,
                    esAdministrador: true, esSupervisor: true, esTecnicoSoporte: true, esSistema: true),
                Rol.Crear("JefeArea", "Jefe de Área", NivelAlcance.Area,
                    esSupervisor: true, esTecnicoSoporte: true, esSistema: true),
                Rol.Crear("Empleado", "Empleado", NivelAlcance.Unidad,
                    esTecnicoSoporte: true, esSistema: true),
                Rol.Crear("Consultor", "Consultor", NivelAlcance.Unidad,
                    esSoloLectura: true, esSistema: true));

            await db.SaveChangesAsync();
        }

        // Usuarios reales: varias páginas (Perfil, por ejemplo) consultan al usuario de la
        // sesión, así que un id inventado en la cabecera no alcanza.
        if (_usuarios.Count == 0)
        {
            foreach (var rol in new[] { "Administrador", "JefeArea", "Empleado", "Consultor", SinRol })
            {
                var etiqueta = rol == SinRol ? "sin-rol" : rol.ToLowerInvariant();
                var usuario = Usuario.Crear($"Usuario {etiqueta}", $"{etiqueta}@pruebas.gob.hn", "hash");
                db.Usuarios.Add(usuario);

                // El usuario sin rol queda deliberadamente sin asignación: es el caso que el
                // fallback de login tapaba.
                if (rol != SinRol)
                    db.AsignacionesUsuario.Add(AsignacionUsuario.Crear(usuario.Id, "DIGER", null, null, rol));

                _usuarios[rol] = usuario.Id;
            }

            await db.SaveChangesAsync();
        }

        // El catálogo se cargó al arrancar, cuando todavía no había roles.
        await scope.ServiceProvider.GetRequiredService<IRolCatalogo>().RecargarAsync();
    }

    /// <summary>Otorga claves a un rol y limpia la caché para que apliquen de inmediato.</summary>
    public async Task OtorgarAsync(string rolId, params string[] claves)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var clave in claves)
        {
            if (!await db.RolPermisos.AnyAsync(p => p.RolId == rolId && p.PermisoClave == clave))
                db.RolPermisos.Add(RolPermiso.Crear(rolId, clave));
        }

        await db.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<IPermissionCache>().InvalidarTodo();
    }

    /// <summary>Cliente que habla como el rol indicado. Sin rol (null) simula la cuenta sin
    /// asignación; sin usuario, una petición anónima.</summary>
    public HttpClient ClienteComo(string? rolId, string institucion = "DIGER")
    {
        var cliente = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var usuarioId = _usuarios[rolId ?? SinRol];

        cliente.DefaultRequestHeaders.Add(TestAuthHandler.CabeceraUsuarioId, usuarioId.ToString());
        cliente.DefaultRequestHeaders.Add(TestAuthHandler.CabeceraInstitucion, institucion);

        if (rolId is not null)
            cliente.DefaultRequestHeaders.Add(TestAuthHandler.CabeceraRol, rolId);

        return cliente;
    }

    public HttpClient ClienteAnonimo() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _conexion?.Dispose();
    }
}
