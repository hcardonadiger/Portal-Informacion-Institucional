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

    /// <summary>
    /// Quién está HOY habilitado de oficio sobre este proyecto por su capacidad de rol, y con
    /// qué papel. Vacío si el proyecto no existe o está borrado.
    ///
    /// <para>Es la única fuente de verdad de esa pregunta, y por eso es pública: la usan la
    /// guarda de <c>QuitarInteresadoCommand</c> —que rechaza quitar a quien lo tiene, lleve su
    /// fila la bandera Automatico o no— y la consulta que llena la ficha, que es la que pinta el
    /// botón de quitar. Si cada una preguntara por su lado, el botón volvería a ofrecer una
    /// acción que el comando rechaza, que es justo el callejón sin salida que esto cierra.</para>
    ///
    /// <para>Se responde por derecho vigente y no por la bandera a propósito: una fila manual
    /// preexistente nunca se promueve a automática (el sync salta a quien ya figura), así que la
    /// bandera diría que se puede quitar cuando esa fila es lo único que sostiene el acceso que
    /// la capacidad garantiza. Y a la inversa: una fila automática huérfana —de una capacidad ya
    /// revocada— sí se puede quitar a mano, cosa que antes no tenía ninguna salida desde el
    /// portal.</para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, RolInteresado>> CalcularDerechoVigenteAsync(
        int proyectoId, CancellationToken ct = default);
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
        // OrderBy: no cambia el resultado (ver más abajo por qué), pero evita depender del orden
        // en que el motor de base de datos decida devolver las filas.
        var asignaciones = await ctx.AsignacionesUsuario
            .Where(a => a.UsuarioId == usuarioId)
            .OrderBy(a => a.Id)
            .ToListAsync(ct);

        var deseados = new Dictionary<int, RolInteresado>();

        // Precedencia FIJA cuando un mismo proyecto califica por los dos caminos: primero se
        // aplican todas las áreas (JefeDeArea -> Patrocinador) y RECIÉN DESPUÉS todas las unidades
        // (Pmo -> Ejecutor), que sobrescriben. Es el mismo orden de bloques que
        // CalcularDeseadosPorProyectoAsync (área primero, unidad después) — a propósito: si no
        // coinciden, SincronizarProyectoAsync y SincronizarUsuarioAsync pueden calcular un Rol
        // distinto para el mismo par (usuario, proyecto) y no hay ninguna razón de negocio para que
        // el resultado dependa de cuál de los dos métodos se llamó, ni de en qué orden EF devuelva
        // las AsignacionesUsuario del usuario (antes: se iteraba asignación por asignación y ganaba
        // la última que trajera la base — no determinístico).
        var areas = asignaciones
            .Where(a => !string.IsNullOrWhiteSpace(a.AreaId) && catalogo.Obtener(a.Rol)?.EsJefeDeArea == true)
            .Select(a => a.AreaId!)
            .Distinct()
            .ToList();
        if (areas.Count > 0)
        {
            var ids = await ctx.Proyectos.IgnoreQueryFilters()
                .Where(p => !p.IsDeleted && p.AreaId != null && areas.Contains(p.AreaId))
                .Select(p => p.Id)
                .ToListAsync(ct);
            foreach (var id in ids) deseados[id] = RolInteresado.Patrocinador;
        }

        var unidades = asignaciones
            .Where(a => !string.IsNullOrWhiteSpace(a.UnidadId) && catalogo.Obtener(a.Rol)?.EsPmo == true)
            .Select(a => a.UnidadId!)
            .Distinct()
            .ToList();
        if (unidades.Count > 0)
        {
            var ids = await ctx.Proyectos.IgnoreQueryFilters()
                .Where(p => !p.IsDeleted && p.UnidadId != null && unidades.Contains(p.UnidadId))
                .Select(p => p.Id)
                .ToListAsync(ct);
            foreach (var id in ids) deseados[id] = RolInteresado.Ejecutor;
        }

        var usuario = await ctx.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct);

        // Se resuelve ANTES de la baja, no después: una cuenta desactivada no califica para nada,
        // así que sus filas automáticas tienen que caer por la rama de baja de más abajo. Mirar el
        // Activo solo al dar de alta —como se hacía— dejaba la fila viva, y desde que la guarda de
        // QuitarInteresado pregunta por el derecho vigente, además imborrable. Mismo corte que
        // CalcularDeseadosPorProyectoAsync hace para el camino por proyecto.
        if (usuario is null || !usuario.Activo) deseados.Clear();

        var todosLosActuales = await ctx.ProyectoInteresados
            .Where(i => i.UsuarioId == usuarioId)
            .ToListAsync(ct);

        // La bitácora se escribe en las dos direcciones, igual que en los dos caminos manuales
        // (ver AgregarInteresadoCommand / QuitarInteresadoCommand): esto concede y revoca acceso a
        // un proyecto, no anota una gestión. Es además el mecanismo que más filas mueve, así que
        // sin esto la pregunta de por qué alguien ve un proyecto se queda sin nada que leer.
        foreach (var fila in todosLosActuales)
            if (fila.Automatico && !deseados.ContainsKey(fila.ProyectoId))
            {
                ctx.ProyectoInteresados.Remove(fila);
                ctx.BitacorasProyecto.Add(Baja(fila.ProyectoId, fila.Nombre, fila.Rol));
            }

        var yaFiguraEn = todosLosActuales.Select(a => a.ProyectoId).ToHashSet();

        // El usuario ya está resuelto arriba. La guarda sigue acá porque `deseados` vacío y
        // usuario nulo son cosas distintas para el compilador, no porque se vuelva a preguntar.
        if (usuario is not null && usuario.Activo)
        {
            foreach (var (proyectoId, rol) in deseados)
            {
                if (yaFiguraEn.Contains(proyectoId)) continue;
                ctx.ProyectoInteresados.Add(InteresadoProyecto.CrearAutomatico(
                    proyectoId, usuario.Id, usuario.Nombre, rol, usuario.Correo));
                ctx.BitacorasProyecto.Add(Alta(proyectoId, usuario.Nombre, rol));
            }
        }

        await ctx.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, RolInteresado>> CalcularDerechoVigenteAsync(
        int proyectoId, CancellationToken ct = default)
    {
        // Mismo IgnoreQueryFilters y misma reposición de !IsDeleted que SincronizarProyectoAsync,
        // por la misma razón: a quién le corresponde este proyecto de oficio no depende del
        // alcance de quien lo pregunta.
        var proyecto = await ctx.Proyectos.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == proyectoId && !p.IsDeleted, ct);
        if (proyecto is null) return new Dictionary<Guid, RolInteresado>();

        return await CalcularDeseadosPorProyectoAsync(proyecto.AreaId, proyecto.UnidadId, ct);
    }

    /// <summary>Por qué le corresponde el proyecto, en las palabras que va a leer alguien en la
    /// bitácora. El Rol es el que codifica el camino —área → Patrocinador, unidad → Ejecutor, ver
    /// la precedencia fija de más arriba—, así que las dos mitades del servicio escriben lo mismo
    /// sin tener que arrastrar el AreaId/UnidadId hasta acá.</summary>
    private static string Motivo(RolInteresado rol) =>
        rol == RolInteresado.Ejecutor ? "su rol de PMO de la unidad" : "su rol de jefe de área";

    private static BitacoraProyecto Alta(int proyectoId, string nombre, RolInteresado rol) =>
        BitacoraProyecto.Crear(
            proyectoId, TipoEventoProyecto.Interesado,
            $"Interesado automático agregado: {nombre} ({rol}), por {Motivo(rol)}. Puede ver el proyecto.",
            InteresadoProyecto.ActorSistema);

    private static BitacoraProyecto Baja(int proyectoId, string nombre, RolInteresado rol) =>
        BitacoraProyecto.Crear(
            proyectoId, TipoEventoProyecto.Interesado,
            $"Interesado automático quitado: {nombre}. Ya no le corresponde por {Motivo(rol)}; " +
            "pierde el acceso que le daba este registro.",
            InteresadoProyecto.ActorSistema);

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

        // Una cuenta desactivada no califica, y el corte va acá y no en la rama de alta a
        // propósito: este diccionario es también lo que decide la BAJA (AplicarAsync quita las
        // filas automáticas de quien no figure en él) y lo que responde CalcularDerechoVigenteAsync,
        // que es la guarda del borrado manual. Filtrando solo al dar de alta, al desactivar a
        // alguien su fila sobrevivía —seguía calificando por su asignación, que no se toca al
        // desactivar la cuenta— y encima quedaba imborrable desde la ficha. Contradecía a
        // AgregarInteresadoCommandHandler, que sí rechaza a un usuario inactivo.
        if (resultado.Count > 0)
        {
            var candidatos = resultado.Keys.ToList();
            var activos = await ctx.Usuarios
                .Where(u => candidatos.Contains(u.Id) && u.Activo)
                .Select(u => u.Id)
                .ToListAsync(ct);

            foreach (var id in candidatos.Except(activos))
                resultado.Remove(id);
        }

        return resultado;
    }

    private async Task AplicarAsync(
        int proyectoId, Dictionary<Guid, RolInteresado> deseados,
        List<InteresadoProyecto> actuales, CancellationToken ct)
    {
        foreach (var fila in actuales)
            if (fila.Automatico && !deseados.ContainsKey(fila.UsuarioId))
            {
                ctx.ProyectoInteresados.Remove(fila);
                ctx.BitacorasProyecto.Add(Baja(proyectoId, fila.Nombre, fila.Rol));
            }

        var yaFiguran = actuales.Select(a => a.UsuarioId).ToHashSet();

        foreach (var (usuarioId, rol) in deseados)
        {
            if (yaFiguran.Contains(usuarioId)) continue;
            var usuario = await ctx.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct);
            if (usuario is null || !usuario.Activo) continue;
            ctx.ProyectoInteresados.Add(InteresadoProyecto.CrearAutomatico(
                proyectoId, usuario.Id, usuario.Nombre, rol, usuario.Correo));
            ctx.BitacorasProyecto.Add(Alta(proyectoId, usuario.Nombre, rol));
        }

        await ctx.SaveChangesAsync(ct);
    }
}
