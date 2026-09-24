using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Diger.TramitesEstado.Infrastructure.Security;

/// <summary>
/// Implementación en memoria de IPermissionCache. TTL corto (60s) como red de seguridad;
/// la invalidación explícita al guardar la matriz es lo que da revocación casi inmediata
/// en un despliegue de una sola instancia. Nota: IMemoryCache es por proceso — con más de
/// una instancia, la invalidación explícita solo limpia la que atendió el guardado y las
/// demás quedan sujetas al TTL.
/// </summary>
public sealed class PermissionCache(IMemoryCache cache, IServiceScopeFactory scopeFactory) : IPermissionCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    // Los roles ya no son un enum, así que InvalidarTodo no puede iterar Enum.GetValues:
    // se registran las claves emitidas para poder purgarlas.
    private readonly ConcurrentDictionary<string, byte> _clavesVivas = new(StringComparer.OrdinalIgnoreCase);

    // La entrada es por rol Y por ámbito: dos personas con el mismo rol en unidades distintas
    // pueden tener módulos distintos habilitados, así que compartir la entrada las mezclaría.
    private static string ClaveCache(string rolId, string? inst, string? area, string? unidad) =>
        $"permisos:rol:{rolId}|{inst}|{area}|{unidad}";

    public async Task<HashSet<string>> ObtenerAsync(
        string rolId, string? institucionId, string? areaId, string? unidadId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rolId)) return [];

        var clave = ClaveCache(rolId, institucionId, areaId, unidadId);
        if (cache.TryGetValue(clave, out HashSet<string>? cached) && cached is not null)
            return cached;

        await using var scope = scopeFactory.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var permisos = (await ctx.RolPermisos
            .Where(p => p.RolId == rolId)
            .Select(p => p.PermisoClave)
            .ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        permisos = AplicarAmbito(
            permisos,
            await ctx.ModuloAmbitos.AsNoTracking().ToListAsync(ct),
            institucionId, areaId, unidadId);

        cache.Set(clave, permisos, Ttl);
        _clavesVivas.TryAdd(clave, 0);
        return permisos;
    }

    /// <summary>
    /// Quita las claves cuyo módulo esté limitado a otras áreas o unidades.
    ///
    /// <para>Abierto por defecto: un módulo sin filas no se toca. Solo los que aparecen en
    /// <c>ModuloAmbitos</c> exigen una concesión que alcance al ámbito del usuario.</para>
    ///
    /// <para>Restringir el padre restringe al hijo: con <c>Siger</c> limitado,
    /// <c>Siger.Conciliacion.Editar</c> también cae, sin repetir la fila.</para>
    /// </summary>
    public static HashSet<string> AplicarAmbito(
        HashSet<string> claves, IReadOnlyList<ModuloAmbito> ambitos,
        string? institucionId, string? areaId, string? unidadId)
    {
        if (ambitos.Count == 0) return claves;

        var restringidos = ambitos
            .GroupBy(a => a.Modulo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        bool Permitida(string clave)
        {
            // "Siger.Conciliacion.Editar" -> se prueba "Siger.Conciliacion" y luego "Siger".
            var modulo = clave.Contains('.') ? clave[..clave.LastIndexOf('.')] : clave;
            while (true)
            {
                if (restringidos.TryGetValue(modulo, out var concesiones) &&
                    !concesiones.Any(c => c.Alcanza(institucionId, areaId, unidadId)))
                    return false;

                var punto = modulo.LastIndexOf('.');
                if (punto <= 0) return true;
                modulo = modulo[..punto];
            }
        }

        return claves.Where(Permitida).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    // Un rol tiene ahora una entrada por ámbito, no una sola, así que se guardan las claves
    // completas: sin esto Invalidar borraba una clave que ya no existe y la revocación se
    // quedaba esperando el TTL.
    public void Invalidar(string rolId)
    {
        if (string.IsNullOrWhiteSpace(rolId)) return;

        var prefijo = $"permisos:rol:{rolId}|";
        foreach (var clave in _clavesVivas.Keys)
        {
            if (!clave.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase)) continue;
            cache.Remove(clave);
            _clavesVivas.TryRemove(clave, out _);
        }
    }

    public void InvalidarTodo()
    {
        foreach (var clave in _clavesVivas.Keys)
        {
            cache.Remove(clave);
            _clavesVivas.TryRemove(clave, out _);
        }
    }
}
