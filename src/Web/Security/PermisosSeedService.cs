using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Security;

/// <summary>
/// Siembra la matriz rol×permiso la primera vez, traduciendo el control de accesos por
/// módulo (RolModuloAccesos) al motor de permisos por acción. Sin esto, al pasar el gateo a
/// [Permission] todos los roles no administradores se quedarían con la matriz vacía, es
/// decir sin acceso a nada.
///
/// No es una migración EF a propósito: las migraciones corren antes que los IHostedService,
/// y en ese momento la tabla Permisos todavía está vacía porque la llena
/// PermissionCatalogSyncService por reflexión. Una migración habría funcionado en una base
/// ya usada y no habría hecho nada en una base nueva — justo al revés de lo que se necesita.
/// Por eso va acá, registrado DESPUÉS del sync del catálogo.
///
/// Guarda de "una sola vez": solo siembra si RolPermisos y PermisosAuditoria están ambas
/// vacías. La bitácora es append-only, así que si alguien alguna vez editó la matriz queda
/// rastro y no se vuelve a sembrar — ni siquiera si dejó todas las casillas desmarcadas a
/// propósito.
/// </summary>
public sealed class PermisosSeedService(
    IServiceScopeFactory scopeFactory,
    ILogger<PermisosSeedService> logger) : IHostedService
{
    /// <summary>
    /// Módulos que antes NO estaban en la matriz rol×módulo ni en el switch de
    /// ModuloAccesoMiddleware: cualquier usuario autenticado los alcanzaba. Se otorgan a
    /// todos los roles para no quitar accesos que la gente ya tenía; el administrador puede
    /// ajustarlos después en /Accesos/Permisos.
    /// </summary>
    private static readonly HashSet<string> AbiertosAntes =
        new(StringComparer.OrdinalIgnoreCase)
        { "Siger", "PlanTrabajo", "Informes", "Recursos", "Chat", "Tramites" };

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            if (await ctx.RolPermisos.AnyAsync(ct) || await ctx.PermisosAuditoria.AnyAsync(ct))
                return;

            var roles = await ctx.Roles
                .Where(r => r.Activo && !r.EsAdministrador)
                .ToListAsync(ct);

            var permisos = await ctx.Permisos.Where(p => p.Activo).ToListAsync(ct);
            var accesos  = await ctx.RolModuloAccesos.ToListAsync(ct);

            if (roles.Count == 0 || permisos.Count == 0)
            {
                logger.LogInformation("Siembra de permisos omitida: aún no hay roles o catálogo que traducir.");
                return;
            }

            var otorgados = 0;

            foreach (var rol in roles)
            {
                // Los módulos que este rol tenía habilitados en la matriz vieja.
                var modulosDelRol = accesos
                    .Where(a => string.Equals(a.RolId, rol.Id, StringComparison.OrdinalIgnoreCase))
                    .Select(a => a.Modulo)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var permiso in permisos)
                {
                    // Un rol de solo lectura recibe únicamente las claves de consulta, aunque
                    // el módulo estuviera habilitado: antes lo frenaba el bloqueo duro del
                    // Consultor en SaveChanges, ahora además no se le otorga.
                    if (rol.EsSoloLectura && permiso.Accion != AccionModulo.Ver)
                        continue;

                    // "Tickets.Temas" pertenece al módulo "Tickets"; "Siger.Conciliacion" a
                    // "Siger". Los módulos de administración (Accesos.*, Admin.*, Areas,
                    // Unidades, Instituciones, Usuarios) no caen en ninguna de las dos ramas
                    // de abajo y quedan sin otorgar, que es exactamente como estaban: solo
                    // los alcanzaba el Administrador, y ese aprueba por código.
                    var punto = permiso.Modulo.IndexOf('.');
                    var raiz  = punto < 0 ? permiso.Modulo : permiso.Modulo[..punto];

                    if (!AbiertosAntes.Contains(raiz) && !modulosDelRol.Contains(raiz))
                        continue;

                    ctx.RolPermisos.Add(RolPermiso.Crear(rol.Id, permiso.Id));
                    otorgados++;
                }
            }

            await ctx.SaveChangesAsync(ct);

            logger.LogInformation(
                "Matriz de permisos sembrada desde el control de accesos por módulo: {N} concesiones para {R} roles.",
                otorgados, roles.Count);
        }
        catch (Exception ex)
        {
            // Fail-closed y sin tumbar el arranque: sin siembra la matriz queda vacía y el
            // administrador (que aprueba por código) puede llenarla a mano en /Accesos/Permisos.
            logger.LogError(ex, "No se pudo sembrar la matriz de permisos.");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
