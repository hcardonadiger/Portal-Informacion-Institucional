namespace Diger.TramitesEstado.Application.Permisos;

public static class PermisosConstants
{
    /// <summary>Módulo de la propia pantalla de administración de permisos.</summary>
    public const string ModuloPermisos = "Accesos.Permisos";

    /// <summary>Clave reservada de la propia pantalla de administración de permisos —
    /// protegida contra quedar en cero roles por GuardarMatrizPermisosCommandHandler,
    /// para que nadie pueda dejar el sistema sin forma de deshacer un cambio de matriz.
    /// Se arma desde el módulo + la acción para que no pueda desincronizarse de la clave
    /// que emite PermissionAttribute.</summary>
    public const string GestionarPermisos = ModuloPermisos + "." + nameof(AccionModulo.Editar);
}

public sealed record PermisoDto(string Clave, string Nombre, string Modulo, AccionModulo Accion);

/// <summary>Un rol configurable, con lo que la pantalla necesita para presentarlo: el color
/// del distintivo y las capacidades que condicionan qué casillas tienen sentido
/// (un rol de solo lectura no puede recibir claves de mutación).</summary>
public sealed record RolColumnaDto(
    string RolId, string Nombre, string? Color, NivelAlcance NivelAlcance, bool EsSoloLectura);

public sealed record RolPermisosDto(string RolId, IReadOnlyList<string> Permisos);

public sealed record CatalogoPermisosDto(
    IReadOnlyList<RolColumnaDto> Roles,
    IReadOnlyList<PermisoDto>    Catalogo,
    IReadOnlyList<RolPermisosDto> Matriz);

// ── Query: catálogo + matriz actual ─────────────────────────────────────────
public sealed record GetCatalogoPermisosQuery : IRequest<CatalogoPermisosDto>;

public sealed class GetCatalogoPermisosQueryHandler(IApplicationDbContext ctx, IRolCatalogo catalogo)
    : IRequestHandler<GetCatalogoPermisosQuery, CatalogoPermisosDto>
{
    public async Task<CatalogoPermisosDto> Handle(GetCatalogoPermisosQuery q, CancellationToken ct)
    {
        var permisos = await ctx.Permisos
            .Where(p => p.Activo)
            .OrderBy(p => p.Modulo).ThenBy(p => p.Accion)
            .Select(p => new PermisoDto(p.Id, p.Nombre, p.Modulo, p.Accion))
            .ToListAsync(ct);

        var grants = await ctx.RolPermisos.ToListAsync(ct);

        var configurables = catalogo.Configurables();

        var roles = configurables
            .Select(r => new RolColumnaDto(r.Codigo, r.Nombre, r.Color, r.NivelAlcance, r.EsSoloLectura))
            .ToList();

        var matriz = configurables
            .Select(r => new RolPermisosDto(r.Codigo,
                grants.Where(g => string.Equals(g.RolId, r.Codigo, StringComparison.OrdinalIgnoreCase))
                      .Select(g => g.PermisoClave).ToList()))
            .ToList();

        return new CatalogoPermisosDto(roles, permisos, matriz);
    }
}

// ── Command: reemplaza toda la matriz en una sola transacción ──────────────
// (a diferencia de GuardarAccesosCommand, que es por rol) porque la guardia anti-bloqueo
// necesita ver el estado final completo de todos los roles a la vez.
public sealed record GuardarMatrizPermisosCommand(
    IReadOnlyDictionary<string, IReadOnlyList<string>> Grants) : IRequest<Unit>;

public sealed class GuardarMatrizPermisosCommandHandler(
    IApplicationDbContext ctx, IPermissionCache cache, ICurrentUserService currentUser, IRolCatalogo catalogo)
    : IRequestHandler<GuardarMatrizPermisosCommand, Unit>
{
    public async Task<Unit> Handle(GuardarMatrizPermisosCommand cmd, CancellationToken ct)
    {
        var actor = currentUser.Nombre ?? currentUser.Correo ?? "—";

        // Fail-closed: solo se acepta lo que el catálogo reconoce como rol activo y
        // configurable. Un rol inexistente, inactivo o administrador se rechaza en vez de
        // escribir concesiones que nadie podría ver ni deshacer desde la matriz.
        foreach (var rolId in cmd.Grants.Keys)
        {
            var info = catalogo.Obtener(rolId)
                ?? throw new DomainException($"El rol '{rolId}' no existe o está inactivo.");

            if (info.EsAdministrador)
                throw new DomainException($"El acceso del rol '{info.Nombre}' no es configurable: tiene capacidad de administrador.");
        }

        var permisosActivos = await ctx.Permisos
            .Where(p => p.Activo)
            .Select(p => new { p.Id, p.Nombre, p.Accion })
            .ToDictionaryAsync(p => p.Id, p => new { p.Nombre, p.Accion }, ct);

        // Un rol de solo lectura no puede quedar con claves de mutación. La pantalla ya las
        // deshabilita, pero eso es una ayuda de UI: sin este chequeo un POST armado a mano
        // (o un formulario quedado de antes de marcar el rol como solo lectura) las escribía
        // igual, y la única barrera restante sería el bloqueo duro de SaveChangesAsync.
        foreach (var (rolId, claves) in cmd.Grants)
        {
            if (catalogo.Obtener(rolId) is not { EsSoloLectura: true } soloLectura) continue;

            var mutaciones = (claves ?? [])
                .Where(c => permisosActivos.TryGetValue(c, out var p) && p.Accion != AccionModulo.Ver)
                .ToList();

            if (mutaciones.Count > 0)
                throw new DomainException(
                    $"El rol '{soloLectura.Nombre}' es de solo lectura: no puede recibir permisos de {string.Join(", ", mutaciones.Select(m => m.Split('.').Last()).Distinct())}.");
        }

        var actuales = await ctx.RolPermisos.ToListAsync(ct);

        // Guardia anti-bloqueo: que nunca quede nadie capaz de deshacer un cambio de matriz.
        //
        // Un rol con EsAdministrador aprueba TODAS las claves por código y jamás aparece como
        // fila en RolPermisos, así que mientras exista uno activo el portal no se puede
        // cerrar sobre sí mismo. Y RolesModule ya garantiza que siempre exista al menos uno
        // (ver ValidarNoEsUltimoAdministradorAsync).
        //
        // Sin esta primera condición la guardia bloqueaba TODO guardado: ningún rol
        // configurable tiene Accesos.Permisos.Editar —los administradores no la necesitan— y
        // la pantalla quedaba inutilizable. El caso que sigue cubriendo es el borde real:
        // una base sin ningún rol administrador activo, donde la única forma de administrar
        // permisos es que un rol configurable conserve la clave.
        var hayAdministradorActivo = catalogo.Activos().Any(r => r.EsAdministrador);

        if (!hayAdministradorActivo)
        {
            // El estado final es lo que trae el payload para los roles incluidos, más lo que
            // ya había para los que no vienen (la pantalla envía un rol por vez).
            var enPayload = cmd.Grants.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var quedaCubierto =
                cmd.Grants.Any(kv => (kv.Value ?? []).Contains(PermisosConstants.GestionarPermisos))
                || actuales.Any(g => !enPayload.Contains(g.RolId)
                                  && g.PermisoClave == PermisosConstants.GestionarPermisos);

            if (!quedaCubierto)
                throw new DomainException("No puede quitar el último rol con permiso para administrar permisos.");
        }

        foreach (var (rolId, clavesDeseadas) in cmd.Grants)
        {
            var validas = (clavesDeseadas ?? [])
                .Where(permisosActivos.ContainsKey)
                .Distinct()
                .ToList();

            var actualesDelRol = actuales
                .Where(g => string.Equals(g.RolId, rolId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var clavesActuales = actualesDelRol.Select(g => g.PermisoClave).ToHashSet();

            foreach (var clave in validas.Where(c => !clavesActuales.Contains(c)))
            {
                ctx.RolPermisos.Add(RolPermiso.Crear(rolId, clave));
                ctx.PermisosAuditoria.Add(PermisoAuditoria.Crear(
                    rolId, clave, permisosActivos[clave].Nombre, AccionPermiso.Otorgado, actor));
            }

            foreach (var g in actualesDelRol.Where(g => !validas.Contains(g.PermisoClave)))
            {
                ctx.RolPermisos.Remove(g);
                var nombre = permisosActivos.TryGetValue(g.PermisoClave, out var n) ? n.Nombre : g.PermisoClave;
                ctx.PermisosAuditoria.Add(PermisoAuditoria.Crear(
                    rolId, g.PermisoClave, nombre, AccionPermiso.Revocado, actor));
            }
        }

        await ctx.SaveChangesAsync(ct);

        foreach (var rolId in cmd.Grants.Keys)
            cache.Invalidar(rolId);

        return Unit.Value;
    }
}
