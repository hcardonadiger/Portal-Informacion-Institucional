namespace Diger.TramitesEstado.Application.Usuarios.Common;

/// <summary>
/// Garantiza que el portal nunca se quede sin nadie capaz de administrarlo.
///
/// Es el espejo, del lado de los usuarios, de la guardia que RolesModule ya tiene del lado de
/// los roles: aquella asegura que exista un ROL con capacidad de administrador, esta asegura
/// que exista al menos un USUARIO ACTIVO asignado a alguno de esos roles. Sin las dos, se
/// puede cumplir la primera y quedar igual afuera — un rol administrador que nadie ejerce no
/// sirve de nada.
///
/// Se volvió necesaria al quitar el fallback de rol del login: antes, una cuenta sin
/// asignación entraba igual como "Empleado", así que un administrador que se quedaba sin
/// asignación conservaba una puerta (mala, pero puerta). Ahora no hay puerta, y la única
/// salida sería tocar la base a mano.
/// </summary>
public static class AdministradoresInvariante
{
    /// <summary>¿El rol asignado a este usuario tiene capacidad de administrador?</summary>
    public static async Task<bool> TieneRolAdministradorAsync(
        IApplicationDbContext ctx, IRolCatalogo catalogo, Guid usuarioId, CancellationToken ct)
    {
        var roles = await ctx.AsignacionesUsuario
            .Where(a => a.UsuarioId == usuarioId)
            .Select(a => a.Rol)
            .Distinct()
            .ToListAsync(ct);

        return roles.Any(r => catalogo.Obtener(r)?.EsAdministrador == true);
    }

    /// <summary>
    /// Lanza si, dejando de lado a <paramref name="usuarioId"/>, no queda ningún otro usuario
    /// activo asignado a un rol administrador. Llamar solo cuando la operación en curso le
    /// quita a ese usuario su condición de administrador (cambio de rol, borrado de
    /// asignaciones o desactivación).
    /// </summary>
    public static async Task ValidarNoEsElUltimoAdministradorAsync(
        IApplicationDbContext ctx, IRolCatalogo catalogo, Guid usuarioId, CancellationToken ct)
    {
        var rolesAdmin = catalogo.Activos()
            .Where(r => r.EsAdministrador)
            .Select(r => r.Codigo)
            .ToList();

        // Sin ningún rol administrador activo el problema es otro y lo cubre RolesModule;
        // acá no hay nada que preservar.
        if (rolesAdmin.Count == 0) return;

        var otros = await ctx.AsignacionesUsuario
            .Where(a => a.UsuarioId != usuarioId && rolesAdmin.Contains(a.Rol))
            .Where(a => ctx.Usuarios.Any(u => u.Id == a.UsuarioId && u.Activo))
            .Select(a => a.UsuarioId)
            .Distinct()
            .CountAsync(ct);

        if (otros == 0)
            throw new DomainException(
                "Es el único usuario activo con un rol administrador. Asigná ese rol a otra " +
                "persona antes de quitárselo o desactivarlo, o nadie podrá volver a administrar el portal.");
    }
}
