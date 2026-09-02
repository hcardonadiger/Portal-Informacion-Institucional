namespace Diger.TramitesEstado.Application.Proyectos.Services;

/// <summary>
/// Mantiene sincronizadas las filas automáticas de InteresadoProyecto (ver
/// InteresadoProyecto.CrearAutomatico / Automatico) para las dos capacidades de rol que dan acceso
/// de oficio a un proyecto: EsJefeDeArea (por Proyecto.AreaId) y EsPmo (por Proyecto.UnidadId).
///
/// No toca filas manuales de otro usuario ni pisa una fila manual del mismo usuario — si alguien
/// ya figura como interesado (por el motivo que sea) su fila no se duplica ni se reemplaza.
/// </summary>
public interface IInteresadosAutomaticosSync
{
    /// <summary>Recalcula los interesados automáticos de UN proyecto. Llamar al crearlo o al
    /// cambiarle AreaId/UnidadId.</summary>
    Task SincronizarProyectoAsync(int proyectoId, CancellationToken ct = default);

    /// <summary>Recalcula los proyectos donde UN usuario debe figurar como interesado automático.
    /// Llamar cuando cambia su rol o su área/unidad asignada.</summary>
    Task SincronizarUsuarioAsync(Guid usuarioId, CancellationToken ct = default);
}

public sealed class InteresadosAutomaticosSyncService(IApplicationDbContext ctx, IRolCatalogo catalogo)
    : IInteresadosAutomaticosSync
{
    public async Task SincronizarProyectoAsync(int proyectoId, CancellationToken ct = default)
    {
        // IgnoreQueryFilters: esta sincronización tiene que ver el proyecto sin importar el alcance
        // de quien disparó la operación — el filtro de Proyectos está anclado a la institución/área/
        // unidad ACTIVA del usuario que hizo la petición (ver AppDbContext), y un jefe de área o un
        // administrador de jerarquía puede estar editando algo fuera de su propio alcance. Sin este
        // bypass la reconciliación quedaría incompleta en silencio para cualquier actor no-global.
        // Se repone el filtro de borrado lógico a mano (IgnoreQueryFilters también lo quita) para no
        // resucitar ni sincronizar un proyecto eliminado. Mismo motivo en SincronizarUsuarioAsync.
        var proyecto = await ctx.Proyectos.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == proyectoId && !p.IsDeleted, ct);
        if (proyecto is null) return;

        var deseados = await CalcularDeseadosPorProyectoAsync(proyecto.AreaId, proyecto.UnidadId, ct);
        var actuales = await ctx.ProyectoInteresados
            .Where(i => i.ProyectoId == proyectoId)
            .ToListAsync(ct);

        await AplicarAsync(proyectoId, deseados, actuales, ct);
    }

    public async Task SincronizarUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var asignaciones = await ctx.AsignacionesUsuario
            .Where(a => a.UsuarioId == usuarioId)
            .ToListAsync(ct);

        var deseados = new Dictionary<int, RolInteresado>();

        // Cada asignación se evalúa por su propio rol: un usuario puede tener varias asignaciones
        // (una por institución/área/unidad) con roles distintos entre sí, así que no basta con
        // mirar la primera — eso dejaría sin sincronizar cualquier asignación que no sea la primera.
        foreach (var asignacion in asignaciones)
        {
            var rolInfo = catalogo.Obtener(asignacion.Rol);
            if (rolInfo is null) continue;

            if (rolInfo.EsJefeDeArea && asignacion.AreaId is { } areaId)
            {
                var ids = await ctx.Proyectos.IgnoreQueryFilters()
                    .Where(p => !p.IsDeleted && p.AreaId == areaId)
                    .Select(p => p.Id)
                    .ToListAsync(ct);
                foreach (var id in ids) deseados[id] = RolInteresado.Patrocinador;
            }

            if (rolInfo.EsPmo && asignacion.UnidadId is { } unidadId)
            {
                var ids = await ctx.Proyectos.IgnoreQueryFilters()
                    .Where(p => !p.IsDeleted && p.UnidadId == unidadId)
                    .Select(p => p.Id)
                    .ToListAsync(ct);
                foreach (var id in ids) deseados[id] = RolInteresado.Ejecutor;
            }
        }

        var todosLosActuales = await ctx.ProyectoInteresados
            .Where(i => i.UsuarioId == usuarioId)
            .ToListAsync(ct);

        foreach (var fila in todosLosActuales)
            if (fila.Automatico && !deseados.ContainsKey(fila.ProyectoId))
                ctx.ProyectoInteresados.Remove(fila);

        var yaFiguraEn = todosLosActuales.Select(a => a.ProyectoId).ToHashSet();

        var usuario = await ctx.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct);
        if (usuario is not null && usuario.Activo)
        {
            foreach (var (proyectoId, rol) in deseados)
            {
                if (yaFiguraEn.Contains(proyectoId)) continue;
                ctx.ProyectoInteresados.Add(InteresadoProyecto.CrearAutomatico(
                    proyectoId, usuario.Id, usuario.Nombre, rol, usuario.Correo));
            }
        }

        await ctx.SaveChangesAsync(ct);
    }

    private async Task<Dictionary<Guid, RolInteresado>> CalcularDeseadosPorProyectoAsync(
        string? areaId, string? unidadId, CancellationToken ct)
    {
        var resultado = new Dictionary<Guid, RolInteresado>();

        if (!string.IsNullOrWhiteSpace(areaId))
        {
            var asignados = await ctx.AsignacionesUsuario
                .Where(a => a.AreaId == areaId)
                .Select(a => new { a.UsuarioId, a.Rol })
                .Distinct()
                .ToListAsync(ct);
            foreach (var a in asignados)
                if (catalogo.Obtener(a.Rol)?.EsJefeDeArea == true)
                    resultado[a.UsuarioId] = RolInteresado.Patrocinador;
        }

        if (!string.IsNullOrWhiteSpace(unidadId))
        {
            var asignados = await ctx.AsignacionesUsuario
                .Where(a => a.UnidadId == unidadId)
                .Select(a => new { a.UsuarioId, a.Rol })
                .Distinct()
                .ToListAsync(ct);
            foreach (var a in asignados)
                if (catalogo.Obtener(a.Rol)?.EsPmo == true)
                    resultado[a.UsuarioId] = RolInteresado.Ejecutor;
        }

        return resultado;
    }

    private async Task AplicarAsync(
        int proyectoId, Dictionary<Guid, RolInteresado> deseados,
        List<InteresadoProyecto> actuales, CancellationToken ct)
    {
        foreach (var fila in actuales)
            if (fila.Automatico && !deseados.ContainsKey(fila.UsuarioId))
                ctx.ProyectoInteresados.Remove(fila);

        var yaFiguran = actuales.Select(a => a.UsuarioId).ToHashSet();

        foreach (var (usuarioId, rol) in deseados)
        {
            if (yaFiguran.Contains(usuarioId)) continue;
            var usuario = await ctx.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct);
            if (usuario is null || !usuario.Activo) continue;
            ctx.ProyectoInteresados.Add(InteresadoProyecto.CrearAutomatico(
                proyectoId, usuario.Id, usuario.Nombre, rol, usuario.Correo));
        }

        await ctx.SaveChangesAsync(ct);
    }
}
