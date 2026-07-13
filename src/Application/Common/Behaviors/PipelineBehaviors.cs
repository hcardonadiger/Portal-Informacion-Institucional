using FluentValidation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Diger.TramitesEstado.Application.Common.Behaviors;

// ── Validation pipeline behavior ──────────────────────────────────────────
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!validators.Any())
            return await next();

        var ctx      = new ValidationContext<TRequest>(request);
        var failures = validators
            .Select(v => v.Validate(ctx))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next();
    }
}

// ── Logging pipeline behavior ─────────────────────────────────────────────
public sealed class LoggingBehavior<TRequest, TResponse>(
    Microsoft.Extensions.Logging.ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        logger.LogInformation("→ Handling {Request}", name);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await next();
            sw.Stop();
            logger.LogInformation("← {Request} completado en {Ms}ms", name, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "✗ {Request} falló después de {Ms}ms", name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}

// ── Caching pipeline behavior ─────────────────────────────────────────────
/// <summary>
/// Intercepta Queries que implementen <see cref="ICacheableQuery"/> y los cachea
/// en <see cref="IMemoryCache"/>. Solo se activa cuando TRequest implementa
/// ICacheableQuery; el compilador de genéricos garantiza que no afecta Commands
/// ni Queries sin la interfaz.
/// COMPATIBILIDAD CON ESCALABILIDAD: Incluir el contexto de usuario en CacheKey
/// cuando los datos son sensibles al RLS (ver ICacheableQuery.CacheKey doc).
/// </summary>
public sealed class CachingBehavior<TRequest, TResponse>(
    IMemoryCache cache,
    ILogger<CachingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICacheableQuery, IRequest<TResponse>
    where TResponse : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var key = request.CacheKey;

        if (cache.TryGetValue(key, out TResponse? cached) && cached is not null)
        {
            logger.LogInformation("🗃 Cache HIT: {Key}", key);
            return cached;
        }

        logger.LogInformation("🗃 Cache MISS: {Key} — consultando BD", key);
        var result = await next();

        var duration = request.CacheDuration ?? TimeSpan.FromMinutes(30);
        cache.Set(key, result, duration);
        logger.LogInformation("🗃 Cache SET: {Key} (TTL={Duration})", key, duration);

        return result;
    }
}
