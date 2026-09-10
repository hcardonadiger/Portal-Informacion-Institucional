using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Infrastructure.Security;

/// <summary>
/// Permite usar [Authorize(Policy = "Expedientes.Crear")] con claves de permiso dinámicas
/// (descubiertas en runtime, administrables desde /Accesos/Permisos) sin tener que
/// pre-registrar cada una con AddPolicy en Program.cs. Coexiste con las policies estáticas
/// legacy que ya existen (PuedeAdministrarCatalogo, etc.): si el nombre ya está registrado
/// se usa tal cual; si no, se construye una policy dinámica respaldada por
/// PermissionRequirement/PermissionAuthorizationHandler.
/// </summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var existente = await _fallback.GetPolicyAsync(policyName);
        if (existente is not null)
            return existente;

        return new AuthorizationPolicyBuilder()
            .AddRequirements(new PermissionRequirement(policyName))
            .Build();
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
}
