using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Security;

/// <summary>
/// Al arrancar, escanea los PageModel del ensamblado Web buscando handlers OnGet*/OnPost*
/// marcados con [Permission] (a nivel de método o de clase) y sincroniza el catálogo en la
/// tabla Permisos: agrega los nuevos, actualiza nombre/módulo de los existentes, desactiva
/// los que ya no aparecen. Corre en TODOS los ambientes (no solo Development) — el objetivo
/// es fallar visible: cualquier handler sin [Permission] ni [AllowAnonymous] queda logueado
/// como advertencia en vez de quedar silenciosamente fuera de control, que es justo lo que
/// le pasó con el tiempo al switch hardcodeado de ModuloAccesoMiddleware.
///
/// BindingFlags.DeclaredOnly es la clave para no repetir el bug de descubrimiento de la
/// implementación de referencia revisada (SGSEC), que usaba reflexión sin DeclaredOnly y
/// terminaba listando métodos heredados de la clase base (Ok(), Dispose(), etc.) como si
/// fueran acciones reales.
/// </summary>
public sealed class PermissionCatalogSyncService(
    IServiceScopeFactory scopeFactory,
    ILogger<PermissionCatalogSyncService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var encontrados = new Dictionary<string, PermissionAttribute>();
        var advertencias = new List<string>();

        var tiposPageModel = typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(PageModel).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var tipo in tiposPageModel)
        {
            var permisoClase = tipo.GetCustomAttribute<PermissionAttribute>();
            var anonimoClase = tipo.GetCustomAttribute<AllowAnonymousAttribute>() is not null;
            var sinPermisoClase = tipo.GetCustomAttribute<PermisoNoRequeridoAttribute>() is not null;

            var handlers = tipo.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name.StartsWith("OnGet", StringComparison.Ordinal)
                         || m.Name.StartsWith("OnPost", StringComparison.Ordinal));

            foreach (var handler in handlers)
            {
                var permisoHandler = handler.GetCustomAttribute<PermissionAttribute>();
                var anonimoHandler = handler.GetCustomAttribute<AllowAnonymousAttribute>() is not null;
                var sinPermisoHandler = handler.GetCustomAttribute<PermisoNoRequeridoAttribute>() is not null;
                var efectivo = permisoHandler ?? permisoClase;

                if (efectivo is not null)
                {
                    encontrados.TryAdd(efectivo.Clave, efectivo);
                }
                else if (!anonimoClase && !anonimoHandler && !sinPermisoClase && !sinPermisoHandler)
                {
                    advertencias.Add($"{tipo.FullName}.{handler.Name}");
                }
            }
        }

        foreach (var w in advertencias)
            logger.LogWarning("PERMISO NO DECLARADO: {Handler} no tiene [Permission] ni [AllowAnonymous].", w);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            var existentes = await ctx.Permisos.ToDictionaryAsync(p => p.Id, cancellationToken);

            foreach (var (clave, attr) in encontrados)
            {
                if (existentes.TryGetValue(clave, out var permiso))
                    permiso.Sincronizar(attr.Nombre, attr.Modulo, attr.Accion);
                else
                    ctx.Permisos.Add(Permiso.Crear(clave, attr.Nombre, attr.Modulo, attr.Accion));
            }

            foreach (var (clave, permiso) in existentes)
            {
                if (!encontrados.ContainsKey(clave) && permiso.Activo)
                    permiso.Desactivar();
            }

            await ctx.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Catálogo de permisos sincronizado: {N} claves, {W} advertencias.",
                encontrados.Count, advertencias.Count);
        }
        catch (Exception ex)
        {
            // No tumbar el arranque de la app si la BD aún no tiene las tablas nuevas
            // (ej. despliegue antes de aplicar la migración) — el efecto es fail-closed:
            // sin catálogo sincronizado, PermissionAuthorizationHandler simplemente no
            // encuentra el permiso para ningún rol no-Administrador, nunca lo abre de más.
            logger.LogError(ex, "No se pudo sincronizar el catálogo de permisos al arrancar.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
