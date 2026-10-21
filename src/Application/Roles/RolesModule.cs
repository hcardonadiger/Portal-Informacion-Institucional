using Diger.TramitesEstado.Application.Proyectos.Services;

namespace Diger.TramitesEstado.Application.Roles;

public sealed record RolListItemDto(
    string Codigo, string Nombre, string? Descripcion, string? Color,
    NivelAlcance NivelAlcance,
    bool EsAdministrador, bool EsSoloLectura, bool EsSupervisor, bool EsTecnicoSoporte,
    bool Activo, bool EsSistema, int UsuariosAsignados,
    bool EsJefeDeArea = false, bool EsPmo = false);

// ── Query: listado de roles ────────────────────────────────────────────────
public sealed record GetRolesQuery(bool SoloActivos = false) : IRequest<IReadOnlyList<RolListItemDto>>;

public sealed class GetRolesQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetRolesQuery, IReadOnlyList<RolListItemDto>>
{
    public async Task<IReadOnlyList<RolListItemDto>> Handle(GetRolesQuery q, CancellationToken ct)
    {
        var roles = await ctx.Roles
            .Where(r => !q.SoloActivos || r.Activo)
            .OrderBy(r => r.NivelAlcance).ThenBy(r => r.Nombre)
            .ToListAsync(ct);

        var conteos = await ctx.AsignacionesUsuario
            .GroupBy(a => a.Rol)
            .Select(g => new { Rol = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Rol, x => x.Total, ct);

        return roles.Select(r => new RolListItemDto(
            r.Id, r.Nombre, r.Descripcion, r.Color, r.NivelAlcance,
            r.EsAdministrador, r.EsSoloLectura, r.EsSupervisor, r.EsTecnicoSoporte,
            r.Activo, r.EsSistema,
            conteos.TryGetValue(r.Id, out var n) ? n : 0,
            r.EsJefeDeArea, r.EsPmo)).ToList();
    }
}

// ── Command: crear ─────────────────────────────────────────────────────────
public sealed record CrearRolCommand(
    string Codigo, string Nombre, NivelAlcance NivelAlcance, string? Descripcion, string? Color,
    bool EsAdministrador, bool EsSoloLectura, bool EsSupervisor, bool EsTecnicoSoporte,
    bool EsJefeDeArea = false, bool EsPmo = false) : IRequest<string>;

public sealed class CrearRolCommandHandler(IApplicationDbContext ctx, IRolCatalogo catalogo)
    : IRequestHandler<CrearRolCommand, string>
{
    public async Task<string> Handle(CrearRolCommand cmd, CancellationToken ct)
    {
        var codigo = (cmd.Codigo ?? "").Trim();
        if (await ctx.Roles.AnyAsync(r => r.Id == codigo, ct))
            throw new DomainException($"Ya existe un rol con el código '{codigo}'.");

        var rol = Rol.Crear(
            codigo, cmd.Nombre, cmd.NivelAlcance, cmd.Descripcion, cmd.Color,
            cmd.EsAdministrador, cmd.EsSoloLectura, cmd.EsSupervisor, cmd.EsTecnicoSoporte,
            esJefeDeArea: cmd.EsJefeDeArea, esPmo: cmd.EsPmo);

        ctx.Roles.Add(rol);
        await ctx.SaveChangesAsync(ct);
        await catalogo.RecargarAsync(ct);
        return rol.Id;
    }
}

// ── Command: actualizar ────────────────────────────────────────────────────
public sealed record ActualizarRolCommand(
    string Codigo, string Nombre, NivelAlcance NivelAlcance, string? Descripcion, string? Color,
    bool EsAdministrador, bool EsSoloLectura, bool EsSupervisor, bool EsTecnicoSoporte,
    bool Activo, bool EsJefeDeArea = false, bool EsPmo = false) : IRequest<Unit>;

public sealed class ActualizarRolCommandHandler(
    IApplicationDbContext ctx,
    IRolCatalogo catalogo,
    IInteresadosAutomaticosSync sync)
    : IRequestHandler<ActualizarRolCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarRolCommand cmd, CancellationToken ct)
    {
        var rol = await ctx.Roles.FirstOrDefaultAsync(r => r.Id == cmd.Codigo, ct)
            ?? throw new NotFoundException(nameof(Rol), cmd.Codigo);

        // Quitarle la capacidad de administrador al último que la tiene dejaría el portal
        // sin nadie capaz de volver a otorgarla.
        if (rol.EsAdministrador && !cmd.EsAdministrador)
            await ValidarNoEsUltimoAdministradorAsync(ctx, rol.Id, ct);

        // Desactivar también lo saca del catálogo, con el mismo efecto.
        if (rol.EsAdministrador && rol.Activo && !cmd.Activo)
            await ValidarNoEsUltimoAdministradorAsync(ctx, rol.Id, ct);

        // Los valores viejos se leen ANTES de Actualizar: después ya están pisados y no habría
        // con qué comparar.
        var cambioLaCapacidad = rol.EsJefeDeArea != cmd.EsJefeDeArea || rol.EsPmo != cmd.EsPmo;
        var cambioLaVigencia  = rol.Activo != cmd.Activo;

        rol.Actualizar(
            cmd.Nombre, cmd.NivelAlcance, cmd.Descripcion, cmd.Color,
            cmd.EsAdministrador, cmd.EsSoloLectura, cmd.EsSupervisor, cmd.EsTecnicoSoporte,
            cmd.EsJefeDeArea, cmd.EsPmo);

        if (cmd.Activo) rol.Activar(); else rol.Desactivar();

        await ctx.SaveChangesAsync(ct);
        await catalogo.RecargarAsync(ct);

        // EsJefeDeArea/EsPmo conceden acceso a proyectos a través de InteresadoProyecto, y hasta
        // acá esta pantalla era el único lugar del portal que las movía SIN avisarle al sync. El
        // efecto era doble y grave: destildar la casilla no revocaba nada —y las filas quedaban
        // irremovibles, sin ninguna salida desde el portal—, y tildarla no concedía nada hasta que
        // alguien volviera a guardar cada proyecto o la jerarquía de cada usuario.
        //
        // El orden importa y es obligatorio: RecargarAsync PRIMERO, porque el sync resuelve las
        // capacidades leyendo el catálogo, y el catálogo guarda una foto que solo cambia ahí. Si
        // se invirtiera, la reconciliación correría con las capacidades viejas y no haría nada.
        //
        // Desactivar el rol lo saca del catálogo, lo que equivale a quitarle todas sus
        // capacidades; reactivarlo se las devuelve. Por eso alcanza con que la vigencia cambie, en
        // cualquiera de las dos direcciones.
        //
        // EliminarRolCommandHandler no necesita nada equivalente: rechaza borrar un rol que tenga
        // usuarios asignados, así que por esa vía no puede quedar ninguna fila huérfana.
        if (cambioLaCapacidad || cambioLaVigencia)
        {
            var usuarios = await ctx.AsignacionesUsuario
                .Where(a => a.Rol == rol.Id)
                .Select(a => a.UsuarioId)
                .Distinct()
                .ToListAsync(ct);

            foreach (var usuarioId in usuarios)
                await sync.SincronizarUsuarioAsync(usuarioId, ct);
        }

        return Unit.Value;
    }

    internal static async Task ValidarNoEsUltimoAdministradorAsync(IApplicationDbContext ctx, string rolId, CancellationToken ct)
    {
        var otros = await ctx.Roles.CountAsync(r => r.Id != rolId && r.EsAdministrador && r.Activo, ct);
        if (otros == 0)
            throw new DomainException("Debe existir al menos un rol activo con capacidad de administrador.");
    }
}

// ── Command: eliminar ──────────────────────────────────────────────────────
public sealed record EliminarRolCommand(string Codigo) : IRequest<Unit>;

public sealed class EliminarRolCommandHandler(IApplicationDbContext ctx, IRolCatalogo catalogo)
    : IRequestHandler<EliminarRolCommand, Unit>
{
    public async Task<Unit> Handle(EliminarRolCommand cmd, CancellationToken ct)
    {
        var rol = await ctx.Roles.FirstOrDefaultAsync(r => r.Id == cmd.Codigo, ct)
            ?? throw new NotFoundException(nameof(Rol), cmd.Codigo);

        if (rol.EsSistema)
            throw new DomainException("Los roles del sistema no se pueden eliminar. Puede desactivarlos si ya no se usan.");

        var asignados = await ctx.AsignacionesUsuario.CountAsync(a => a.Rol == rol.Id, ct);
        if (asignados > 0)
            throw new DomainException($"No se puede eliminar el rol porque está asignado a {asignados} usuario(s).");

        if (rol.EsAdministrador)
            await ActualizarRolCommandHandler.ValidarNoEsUltimoAdministradorAsync(ctx, rol.Id, ct);

        // Las concesiones (RolPermisos / RolModuloAccesos) caen por FK en cascada;
        // PermisosAuditoria no tiene FK a propósito, para conservar la bitácora.
        ctx.Roles.Remove(rol);
        await ctx.SaveChangesAsync(ct);
        await catalogo.RecargarAsync(ct);
        return Unit.Value;
    }
}
