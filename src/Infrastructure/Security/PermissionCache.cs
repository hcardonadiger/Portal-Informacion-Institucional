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

    private static string ClaveCache(string rolId) => $"permisos:rol:{rolId}";

    public async Task<HashSet<string>> ObtenerAsync(string rolId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rolId)) return [];

        var clave = ClaveCache(rolId);
        if (cache.TryGetValue(clave, out HashSet<string>? cached) && cached is not null)
            return cached;

        await using var scope = scopeFactory.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var permisos = (await ctx.RolPermisos
            .Where(p => p.RolId == rolId)
            .Select(p => p.PermisoClave)
            .ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        cache.Set(clave, permisos, Ttl);
        _clavesVivas.TryAdd(rolId, 0);
        return permisos;
    }

    public void Invalidar(string rolId)
    {
        if (string.IsNullOrWhiteSpace(rolId)) return;
        cache.Remove(ClaveCache(rolId));
        _clavesVivas.TryRemove(rolId, out _);
    }

    public void InvalidarTodo()
    {
        foreach (var rolId in _clavesVivas.Keys)
            Invalidar(rolId);
    }
}
