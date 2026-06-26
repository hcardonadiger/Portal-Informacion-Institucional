using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Diger.TramitesEstado.Infrastructure.Security;

/// <summary>Claims usados para la sesión (cookie de autenticación).</summary>
public static class AppClaims
{
    public const string UserId      = "diger:uid";
    public const string Rol         = "diger:rol";
    public const string Institucion = "diger:inst"; // un claim por institución asignada
}

public sealed class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public int? UserId =>
        int.TryParse(User?.FindFirstValue(AppClaims.UserId), out var id) ? id : null;

    public string? Nombre => User?.FindFirstValue(ClaimTypes.Name);

    public string? Correo => User?.FindFirstValue(ClaimTypes.Email);

    public RolUsuario? Rol =>
        Enum.TryParse<RolUsuario>(User?.FindFirstValue(AppClaims.Rol), out var rol) ? rol : null;

    // ── Alcance institucional ──────────────────────────────────────────────
    // Sin usuario (procesos del sistema/seed) o Administrador ⇒ acceso global.
    public bool EsGlobal => !IsAuthenticated || Rol is null or RolUsuario.Administrador;

    public IReadOnlyCollection<int> InstitucionesAsignadas =>
        User?.FindAll(AppClaims.Institucion)
             .Select(c => int.TryParse(c.Value, out var id) ? id : 0)
             .Where(id => id > 0)
             .ToHashSet()
        ?? [];

    public bool PuedeAccederInstitucion(int? institucionId) =>
        EsGlobal || (institucionId is int id && InstitucionesAsignadas.Contains(id));
}
