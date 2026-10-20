using Diger.TramitesEstado.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Security;

/// <summary>
/// Traslada a las claves nuevas las concesiones que los roles tenían bajo <c>Siger.*</c>
/// antes de que el trabajo de Honduras Simple se separara del inventario, el 3 de septiembre
/// de 2026.
///
/// <b>Sin esto, la separación le quita el trabajo a todo el mundo el día del despliegue.</b>
/// Las filas de <c>RolPermisos</c> guardan la clave, no una referencia; al renombrar
/// "Siger.Publicacion.Editar" a "HondurasSimple.Publicacion.Editar" la fila vieja queda
/// apuntando a un permiso que <see cref="PermissionCatalogSyncService"/> desactiva por no
/// encontrarlo en el código, y quien publicaba deja de poder publicar sin que nadie haya
/// decidido eso.
///
/// <b>Copia, no mueve.</b> Las filas viejas se dejan como están: son inertes en cuanto el
/// permiso queda desactivado, y borrarlas destruiría el rastro de qué tenía cada rol antes.
/// Un caso lo pide expresamente: <c>Siger.Ver</c> se queda —es el permiso del inventario— y
/// además engendra <c>HondurasSimple.Ver</c>, porque la pantalla de completitud, que hoy se
/// abre con el permiso de consulta del inventario, se fue a la sección nueva.
///
/// No es una migración EF por la misma razón que <see cref="PermisosSeedService"/> no lo es:
/// las migraciones corren antes que los IHostedService, y en ese momento las claves nuevas
/// todavía no existen en la tabla <c>Permisos</c> porque las escribe el sincronizador del
/// catálogo por reflexión. Va registrado después de él.
///
/// <b>Guarda de una sola vez.</b> Cada concesión trasladada deja una línea en la bitácora
/// —que es append-only— firmada por <see cref="Actor"/>. Si esa firma ya aparece, el trabajo
/// está hecho y no se repite. Esto importa más de lo que parece: sin la guarda, un
/// administrador que decida quitarle la publicación a un rol se la encontraría de vuelta en
/// el siguiente reinicio, y tardaría semanas en entender por qué.
/// </summary>
public sealed class MigracionHondurasSimpleService(
    IServiceScopeFactory scopeFactory,
    ILogger<MigracionHondurasSimpleService> logger) : IHostedService
{
    /// <summary>Firma en la bitácora. Es también la guarda de "una sola vez".</summary>
    private const string Actor = "separacion-honduras-simple";

    /// <summary>
    /// Clave vieja ⇒ clave nueva. Se listan también las claves que quizá no existan en una
    /// base concreta (p. ej. "Siger.Conciliacion.Ver", que nunca llegó a declararse): las que
    /// no estén en el catálogo se saltan solas más abajo.
    /// </summary>
    private static readonly (string Vieja, string Nueva)[] Mudanzas =
    [
        ("Siger.Ver",                 "HondurasSimple.Ver"),
        ("Siger.Editar",              "HondurasSimple.Editar"),
        ("Siger.Eliminar",            "HondurasSimple.Eliminar"),
        ("Siger.Llenado.Ver",         "HondurasSimple.Llenado.Ver"),
        ("Siger.Llenado.Editar",      "HondurasSimple.Llenado.Editar"),
        ("Siger.Conciliacion.Ver",    "HondurasSimple.Conciliacion.Ver"),
        ("Siger.Conciliacion.Editar", "HondurasSimple.Conciliacion.Editar"),
        ("Siger.Publicacion.Ver",     "HondurasSimple.Publicacion.Ver"),
        ("Siger.Publicacion.Editar",  "HondurasSimple.Publicacion.Editar"),
    ];

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            if (await ctx.PermisosAuditoria.AnyAsync(a => a.Actor == Actor, ct))
                return;

            // El catálogo manda: solo se otorga lo que el sincronizador ya escribió. Otorgar
            // una clave que no existe dejaría una fila que no autoriza nada y que además
            // aparecería como "sin clasificar" en la pantalla de administración.
            var nuevos = await ctx.Permisos
                .Where(p => p.Id.StartsWith("HondurasSimple"))
                .ToDictionaryAsync(p => p.Id, p => p.Nombre, StringComparer.OrdinalIgnoreCase, ct);

            if (nuevos.Count == 0)
            {
                logger.LogWarning(
                    "Traslado a Honduras Simple omitido: el catálogo todavía no tiene ninguna clave nueva. " +
                    "¿Se registró este servicio antes de PermissionCatalogSyncService?");
                return;
            }

            var concesiones = await ctx.RolPermisos.ToListAsync(ct);
            if (concesiones.Count == 0)
            {
                // Base recién creada: no hay nada que trasladar y PermisosSeedService ya se
                // encargó de repartir lo que corresponde. No se firma la bitácora, para que
                // el traslado siga disponible si esta base se puebla después.
                logger.LogInformation("Traslado a Honduras Simple omitido: la matriz de permisos está vacía.");
                return;
            }

            var yaOtorgado = concesiones
                .Select(c => (c.RolId, c.PermisoClave))
                .ToHashSet();

            var trasladadas = 0;

            foreach (var (vieja, nueva) in Mudanzas)
            {
                if (!nuevos.TryGetValue(nueva, out var nombre)) continue;

                foreach (var c in concesiones.Where(c =>
                             string.Equals(c.PermisoClave, vieja, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!yaOtorgado.Add((c.RolId, nueva))) continue;

                    ctx.RolPermisos.Add(RolPermiso.Crear(c.RolId, nueva));
                    ctx.PermisosAuditoria.Add(
                        PermisoAuditoria.Crear(c.RolId, nueva, nombre, AccionPermiso.Otorgado, Actor));
                    trasladadas++;
                }
            }

            if (trasladadas == 0)
            {
                logger.LogInformation("Traslado a Honduras Simple: no había concesiones de Siger.* que trasladar.");
                return;
            }

            await ctx.SaveChangesAsync(ct);
            logger.LogInformation(
                "Traslado a Honduras Simple: {N} concesiones copiadas desde las claves viejas de Siger.", trasladadas);
        }
        catch (Exception ex)
        {
            // Igual que la siembra: no se tumba el arranque. Si esto falla, la matriz de
            // Honduras Simple queda vacía y el administrador la llena en /Accesos/Permisos —
            // molesto, pero visible. Tumbar el portal entero no lo sería.
            logger.LogError(ex, "No se pudieron trasladar las concesiones a Honduras Simple.");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
