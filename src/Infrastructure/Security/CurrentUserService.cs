using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Diger.TramitesEstado.Infrastructure.Security;

/// <summary>Claims usados para la sesión (cookie de autenticación).</summary>
public static class AppClaims
{
    public const string UserId            = "diger:uid";
    public const string ActiveRol         = "diger:rol";
    public const string ActiveInstitucion = "diger:inst";
    public const string ActiveArea        = "diger:area";
    public const string ActiveUnidad      = "diger:unidad";
    public const string AsignacionesJson  = "diger:asignaciones";
}

public sealed class CurrentUserService(IHttpContextAccessor accessor, IRolCatalogo rolCatalogo) : ICurrentUserService
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    /// <summary>Capacidades del rol activo, o null si el rol no está en el catálogo
    /// (inexistente, inactivo, o catálogo no cargado) — en ese caso se asume lo más restrictivo.</summary>
    private RolInfo? RolActual => rolCatalogo.Obtener(Rol);

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public Guid? UserId
    {
        get
        {
            var val = User?.FindFirstValue(AppClaims.UserId);
            return Guid.TryParse(val, out var id) ? id : null;
        }
    }

    public string? Nombre => User?.FindFirstValue(ClaimTypes.Name);

    public string? Correo => User?.FindFirstValue(ClaimTypes.Email);

    public string? Rol =>
        User?.FindFirstValue(AppClaims.ActiveRol);

    public string? ActiveInstitucionId => User?.FindFirstValue(AppClaims.ActiveInstitucion);
    public string? ActiveAreaId        => User?.FindFirstValue(AppClaims.ActiveArea);
    public string? ActiveUnidadId      => User?.FindFirstValue(AppClaims.ActiveUnidad);

    // ── Alcance institucional ──────────────────────────────────────────────
    // Sin usuario (procesos del sistema/seed) ⇒ acceso global. Con usuario, lo decide
    // la capacidad EsAdministrador del rol (tabla Roles), no un nombre hardcodeado.
    public bool EsGlobal => !IsAuthenticated || Rol is null || RolActual?.EsAdministrador == true;

    // Falla cerrado: rol no resuelto ⇒ el alcance más restrictivo y sin capacidades.
    public NivelAlcance NivelAlcance  => RolActual?.NivelAlcance ?? NivelAlcance.Unidad;
    public bool EsSoloLectura         => RolActual?.EsSoloLectura == true;
    public bool EsSupervisor          => RolActual?.EsSupervisor == true;
    public bool EsTecnicoSoporte      => RolActual?.EsTecnicoSoporte == true;
    
    public IReadOnlyCollection<string> InstitucionesAsignadas
    {
        get
        {
            var json = User?.FindFirstValue(AppClaims.AsignacionesJson);
            if (string.IsNullOrWhiteSpace(json)) return [];
            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.EnumerateArray()
                    .Select(e => e.GetProperty("InstitucionId").GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)
                    .ToHashSet();
            }
            catch { return []; }
        }
    }

    // En la nueva estructura, un Jefe de Institución o Administrador puede acceder a la institución.
    // También si el active context está en esa institución.
    public bool PuedeAccederInstitucion(string? institucionId) =>
        EsGlobal || (!string.IsNullOrWhiteSpace(institucionId) && ActiveInstitucionId == institucionId);
}
