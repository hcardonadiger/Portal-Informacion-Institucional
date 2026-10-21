using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Diger.TramitesEstado.Infrastructure.Security;

/// <summary>
/// Implementación singleton de IRolCatalogo: un diccionario inmutable que se reemplaza
/// completo en cada recarga (lectura sin locks desde cualquier hilo).
///
/// Falla cerrado: si el rol no está en el catálogo — porque no existe, está inactivo, o
/// el catálogo aún no se pudo cargar — Obtener devuelve null y quien consulta debe asumir
/// lo más restrictivo (ver CurrentUserService: sin bypass global, alcance de Unidad).
/// </summary>
public sealed class RolCatalogo(IServiceScopeFactory scopeFactory, ILogger<RolCatalogo> logger) : IRolCatalogo
{
    private volatile IReadOnlyDictionary<string, RolInfo> _roles =
        new Dictionary<string, RolInfo>(StringComparer.OrdinalIgnoreCase);

    public RolInfo? Obtener(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo) ? null
        : _roles.TryGetValue(codigo, out var info) ? info
        : null;

    public IReadOnlyList<RolInfo> Activos() =>
        _roles.Values.OrderBy(r => r.NivelAlcance).ThenBy(r => r.Nombre).ToList();

    public async Task RecargarAsync(CancellationToken ct = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            var roles = await ctx.Roles
                .Where(r => r.Activo)
                .Select(r => new RolInfo(
                    r.Id, r.Nombre, r.NivelAlcance,
                    r.EsAdministrador, r.EsSoloLectura, r.EsSupervisor, r.EsTecnicoSoporte,
                    r.EsJefeDeArea, r.EsPmo,
                    r.Color))
                .ToListAsync(ct);

            _roles = roles.ToDictionary(r => r.Codigo, StringComparer.OrdinalIgnoreCase);

            logger.LogInformation("Catálogo de roles cargado: {N} roles activos.", roles.Count);
        }
        catch (Exception ex)
        {
            // No se reemplaza el catálogo vigente: ante un fallo de recarga es preferible
            // seguir con el último bueno que dejar a todo el portal sin capacidades.
            logger.LogError(ex, "No se pudo cargar el catálogo de roles.");
        }
    }
}
