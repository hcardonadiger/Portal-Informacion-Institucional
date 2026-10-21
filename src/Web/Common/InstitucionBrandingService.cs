using Diger.TramitesEstado.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Common;

public sealed record InstitucionBranding(
    string Nombre,
    string? NombreCorto,
    string? LogoUrl,
    string? Color);

public interface IInstitucionBrandingService
{
    Task<InstitucionBranding?> GetAsync(string? institucionId, CancellationToken ct = default);
    void InvalidarCache(string institucionId);
}

public sealed class InstitucionBrandingService(
    IApplicationDbContext ctx,
    IMemoryCache cache) : IInstitucionBrandingService
{
    private static string Key(string id) => $"inst-branding:{id}";

    public async Task<InstitucionBranding?> GetAsync(string? id, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(id)) return null;

        return await cache.GetOrCreateAsync(Key(id), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return await ctx.Instituciones
                .AsNoTracking()
                .Where(i => i.Id == id)
                .Select(i => new InstitucionBranding(i.Nombre, i.NombreCorto, i.LogoUrl, i.Color))
                .FirstOrDefaultAsync(ct);
        });
    }

    public void InvalidarCache(string id) => cache.Remove(Key(id));
}
