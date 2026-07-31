using Diger.TramitesEstado.Application.Accesos;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Common;

/// <summary>
/// Resuelve a qué módulos/opciones del portal puede acceder el usuario actual (según su rol).
/// Scoped: carga los permisos una sola vez por request. Reglas:
/// Administrador → todo; módulos solo-admin → solo Administrador; resto → según la matriz rol × módulo.
/// </summary>
public sealed class AccesoModulosService(ICurrentUserService currentUser, IApplicationDbContext ctx)
{
    private HashSet<string>? _permitidos;

    public bool EsAdministrador => currentUser.Rol == "Administrador";

    private bool? _esSoporte;

    /// <summary>¿El usuario actual atiende el chat de soporte? Lo es el Administrador o
    /// cualquier usuario con al menos un tema asignado (<c>UsuarioTemas</c>). Solo controla
    /// la visibilidad del enlace del navbar. Se resuelve una vez por request.</summary>
    public async Task<bool> EsSoporteAsync(CancellationToken ct = default)
    {
        if (_esSoporte is bool cache) return cache;
        if (EsAdministrador) return (_esSoporte = true).Value;
        var uid = currentUser.UserId;
        if (uid is null) return (_esSoporte = false).Value;
        _esSoporte = await ctx.UsuarioTemas.AnyAsync(ut => ut.UsuarioId == uid.Value, ct);
        return _esSoporte.Value;
    }

    public async Task<bool> PuedeAsync(string modulo, CancellationToken ct = default)
    {
        if (ModulosPortal.EsSoloAdmin(modulo)) return EsAdministrador;
        if (EsAdministrador) return true;
        var set = await CargarAsync(ct);
        return set.Contains(modulo);
    }

    private async Task<HashSet<string>> CargarAsync(CancellationToken ct)
    {
        if (_permitidos is not null) return _permitidos;
        var rolStr = currentUser.Rol;
        if (rolStr is null || !Enum.TryParse<RolUsuario>(rolStr, out var rol)) return _permitidos = new();
        _permitidos = (await ctx.RolModuloAccesos
            .Where(g => g.Rol == rol)
            .Select(g => g.Modulo)
            .ToListAsync(ct)).ToHashSet();
        return _permitidos;
    }
}
