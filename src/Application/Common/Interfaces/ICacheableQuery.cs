using Microsoft.Extensions.Caching.Memory;

namespace Diger.TramitesEstado.Application.Common.Interfaces;

/// <summary>
/// Marca un IRequest como cacheable. El CachingBehavior del pipeline de MediatR
/// intercepta cualquier Query que implemente esta interfaz y la cachea en IMemoryCache.
/// COMPATIBILIDAD CON ESCALABILIDAD: El CacheKey debe incluir el contexto de usuario
/// (InstitucionId, etc.) cuando los datos son sensibles al RLS, para evitar servir
/// datos de una institución a usuarios de otra.
/// Ejemplo: $"instituciones-activas-{usuarioId}" o $"temas-activos-{institucionId}"
/// </summary>
public interface ICacheableQuery
{
    /// <summary>Clave única que identifica este resultado en el caché.</summary>
    string CacheKey { get; }

    /// <summary>Tiempo de vida del caché. Null = usa el valor por defecto (30 min).</summary>
    TimeSpan? CacheDuration { get; }
}
