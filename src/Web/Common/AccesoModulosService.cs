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

    public bool EsAdministrador => currentUser.Rol == RolUsuario.Administrador;

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
        var rol = currentUser.Rol;
        if (rol is null) return _permitidos = new();
        _permitidos = (await ctx.RolModuloAccesos
            .Where(g => g.Rol == rol)
            .Select(g => g.Modulo)
            .ToListAsync(ct)).ToHashSet();
        return _permitidos;
    }
}
