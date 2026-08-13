using Microsoft.AspNetCore.Authorization;

namespace Diger.TramitesEstado.Infrastructure.Security;

/// <summary>Exige que el rol activo tenga la clave de permiso indicada — ver
/// PermissionAuthorizationHandler y PermissionPolicyProvider.</summary>
public sealed record PermissionRequirement(string Clave) : IAuthorizationRequirement;
