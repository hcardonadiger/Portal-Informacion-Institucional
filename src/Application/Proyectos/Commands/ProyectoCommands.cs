using Diger.TramitesEstado.Application.Proyectos.Common;
using Diger.TramitesEstado.Application.Proyectos.Services;

namespace Diger.TramitesEstado.Application.Proyectos.Commands;

// ── Carga del árbol ───────────────────────────────────────────────────────
/// <summary>
/// Trae el proyecto con su estructura completa: entregables y las actividades de cada uno.
///
/// <para>Todo comando que pueda mover el avance pasa por acá. <c>Proyecto.RecalcularAvance</c> exige
/// los entregables por parámetro justamente para no depender de que alguien se acordara del
/// <c>Include</c>, y esta es la única forma de tenerlos de verdad: una consulta hecha después de
/// agregar entregables en memoria no los vería —no están en la base todavía— y el proyecto
/// terminaría recalculándose contra media estructura.</para>
/// </summary>
internal static class ProyectoConEstructura
{
    public static Task<Proyecto?> CargarAsync(IApplicationDbContext ctx, int id, CancellationToken ct) =>
        ctx.Proyectos
            .Include(p => p.Entregables).ThenInclude(e => e.Actividades)
                                        .ThenInclude(a => a.Predecesoras)
            .FirstOrDefaultAsync(p => p.Id == id, ct)!;
}

// ── Responsables ──────────────────────────────────────────────────────────
/// <summary>
/// Los responsables de entregables y actividades salen de los interesados del proyecto, no del
/// padrón completo de usuarios. Es lo que hace que la asignación signifique algo: ser interesado
/// da acceso al proyecto (ver el filtro de <c>Proyecto</c>), así que quien queda a cargo de una
/// actividad puede abrirla.
///
/// <para>El responsable <b>del proyecto</b> queda fuera de esta regla a propósito: ver la nota en
/// <see cref="Proyecto.ResponsableId"/>.</para>
///
/// <para>Se valida solo lo que <b>cambia</b>. Los entregables que vienen de la carga inicial
/// tienen responsables que nunca se registraron como interesados; exigirles la regla al guardar
/// convertiría cada edición de la ficha en un error que el usuario no provocó, y la salida
/// obvia —borrarles el responsable— perdería el dato.</para>
/// </summary>
internal static class ResponsablesProyecto
{
    public static async Task<HashSet<Guid>> InteresadosAsync(
        IApplicationDbContext ctx, int proyectoId, CancellationToken ct) =>
        (await ctx.ProyectoInteresados.AsNoTracking()
            .Where(i => i.ProyectoId == proyectoId)
            .Select(i => i.UsuarioId)
            .ToListAsync(ct)).ToHashSet();

    public static void Exigir(Guid? nuevo, Guid? actual, HashSet<Guid> interesados, string que, string nombre)
    {
        if (nuevo is null || nuevo == actual) return;
        if (interesados.Contains(nuevo.Value)) return;

        throw new DomainException(
            $"«{nombre}»: {que} solo puede quedar a cargo de un interesado del proyecto. " +
            "Agregue primero a esa persona en la sección Interesados y vuelva a intentarlo.");
    }
}

// ── Crear ─────────────────────────────────────────────────────────────────
public sealed record CrearProyectoCommand(
    string            Nombre,
    string?           Objetivo        = null,
    string?           AreaId          = null,
    string?           UnidadId        = null,
    Guid?             ResponsableId   = null,
    string?           Responsable     = null,
    PrioridadProyecto Prioridad       = PrioridadProyecto.Media,
    DateOnly?         FechaInicioPlan = null,
    DateOnly?         FechaFinPlan    = null) : IRequest<int>;

public sealed class CrearProyectoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser,
    IInteresadosAutomaticosSync sync)
    : IRequestHandler<CrearProyectoCommand, int>
{
    public async Task<int> Handle(CrearProyectoCommand cmd, CancellationToken ct)
    {
        if (cmd.FechaInicioPlan is { } ini && cmd.FechaFinPlan is { } fin && fin < ini)
            throw new DomainException("La fecha de cierre planificada no puede ser anterior a la de inicio.");

        var anio = (cmd.FechaInicioPlan?.Year) ?? DateTime.UtcNow.Year;
        var proyecto = Proyecto.Crear(await SiguienteCodigoAsync(ctx, anio, ct), cmd.Nombre, cmd.Objetivo);

        // El proyecto nace anclado a la institución activa de quien lo crea. Sin ancla quedaría
        // fuera del filtro de alcance y no lo vería nadie salvo su responsable.
        proyecto.InstitucionId   = currentUser.ActiveInstitucionId;
        proyecto.AreaId          = string.IsNullOrWhiteSpace(cmd.AreaId) ? null : cmd.AreaId.Trim();
        proyecto.UnidadId        = string.IsNullOrWhiteSpace(cmd.UnidadId) ? null : cmd.UnidadId.Trim();
        proyecto.ResponsableId   = cmd.ResponsableId;
        proyecto.Responsable     = string.IsNullOrWhiteSpace(cmd.Responsable) ? null : cmd.Responsable.Trim();
        proyecto.Prioridad       = cmd.Prioridad;
        proyecto.FechaInicioPlan = cmd.FechaInicioPlan;
        proyecto.FechaFinPlan    = cmd.FechaFinPlan;

        ctx.Proyectos.Add(proyecto);
        await ctx.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(proyecto.AreaId) || !string.IsNullOrWhiteSpace(proyecto.UnidadId))
            await sync.SincronizarProyectoAsync(proyecto.Id, ct);

        return proyecto.Id;
    }

    /// <summary>
    /// Código correlativo por año: PRY-2026-01. Cuenta ignorando el filtro de borrados para no
    /// reciclar el código de un proyecto eliminado — el índice único lo permitiría (está filtrado
    /// por IsDeleted), pero reusar un código confunde a quien lo citó en un informe.
    /// </summary>
    private static async Task<string> SiguienteCodigoAsync(IApplicationDbContext ctx, int anio, CancellationToken ct)
    {
        var prefijo = $"PRY-{anio}-";
        var usados  = await ctx.Proyectos.IgnoreQueryFilters()
            .Where(p => p.Codigo.StartsWith(prefijo))
            .Select(p => p.Codigo)
            .ToListAsync(ct);

        var siguiente = usados
            .Select(c => int.TryParse(c[prefijo.Length..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{prefijo}{siguiente:00}";
    }
}

// ── Actualizar ficha + estructura ─────────────────────────────────────────
public sealed record ActualizarProyectoCommand(
    int               Id,
    string            Nombre,
    string?           Objetivo,
    string?           AreaId,
    string?           UnidadId,
    Guid?             ResponsableId,
    string?           Responsable,
    PrioridadProyecto Prioridad,
    DateOnly?         FechaInicioPlan,
    DateOnly?         FechaFinPlan,
    IReadOnlyList<EntregableInput> Entregables) : IRequest<Unit>;

public sealed class ActualizarProyectoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser,
    IInteresadosAutomaticosSync sync)
    : IRequestHandler<ActualizarProyectoCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarProyectoCommand cmd, CancellationToken ct)
    {
        var proyecto = await ProyectoConEstructura.CargarAsync(ctx, cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Proyecto), cmd.Id);

        if (cmd.FechaInicioPlan is { } ini && cmd.FechaFinPlan is { } fin && fin < ini)
            throw new DomainException("La fecha de cierre planificada no puede ser anterior a la de inicio.");

        var nombre = (cmd.Nombre ?? "").Trim();
        if (nombre.Length == 0) throw new DomainException("El proyecto necesita un nombre.");

        // El diff se arma ANTES de tocar nada: después las propiedades ya son las nuevas.
        var cambiosFicha = DiffFicha(proyecto, cmd, nombre);

        // Se compara ANTES de mutar: una vez asignadas, proyecto.AreaId/UnidadId ya son los valores
        // nuevos y la comparación siempre daría "sin cambio".
        var areaOUnidadCambio =
               proyecto.AreaId   != (string.IsNullOrWhiteSpace(cmd.AreaId)   ? null : cmd.AreaId.Trim())
            || proyecto.UnidadId != (string.IsNullOrWhiteSpace(cmd.UnidadId) ? null : cmd.UnidadId.Trim());

        proyecto.Nombre          = nombre;
        proyecto.Objetivo        = string.IsNullOrWhiteSpace(cmd.Objetivo) ? null : cmd.Objetivo.Trim();
        // La institución NO se edita desde la ficha: mover un proyecto de institución es
        // sacárselo de las manos a quien lo ve hoy, y no es una casilla más del formulario.
        proyecto.AreaId          = string.IsNullOrWhiteSpace(cmd.AreaId) ? null : cmd.AreaId.Trim();
        proyecto.UnidadId        = string.IsNullOrWhiteSpace(cmd.UnidadId) ? null : cmd.UnidadId.Trim();
        proyecto.ResponsableId   = cmd.ResponsableId;
        proyecto.Responsable     = string.IsNullOrWhiteSpace(cmd.Responsable) ? null : cmd.Responsable.Trim();
        proyecto.Prioridad       = cmd.Prioridad;
        proyecto.FechaInicioPlan = cmd.FechaInicioPlan;
        proyecto.FechaFinPlan    = cmd.FechaFinPlan;

        var interesados = await ResponsablesProyecto.InteresadosAsync(ctx, proyecto.Id, ct);
        var resultado   = ReconciliarEstructura(proyecto, cmd.Entregables, interesados);

        // Las actividades que se van arrastran las imputaciones que las apuntan. Hay que soltarlas
        // a mano: su FK es NoAction —no puede ser SetNull sin chocar con el error 1785, ver la
        // configuración de AvanceProyecto— y el borrado fallaría con las entradas apuntándolas.
        if (resultado.ActividadesBorradas.Count > 0)
        {
            var imputados = await ctx.ProyectoAvances
                .Where(a => a.ActividadId != null && resultado.ActividadesBorradas.Contains(a.ActividadId.Value))
                .ToListAsync(ct);

            foreach (var avance in imputados)
                avance.DesimputarActividad();
        }

        // Las dependencias que apuntaban a una actividad que se acaba de quitar se sueltan en vez
        // de reventar: el usuario borró esa actividad a propósito y no tiene por qué enterarse de
        // que otra fila la referenciaba. Se limpian solo los Ids que se fueron en ESTE guardado —
        // cualquier otra referencia desconocida sí es un formulario viejo, y eso lo ataja Validar.
        if (resultado.ActividadesBorradas.Count > 0)
        {
            var muertas = resultado.ActividadesBorradas.ToHashSet();
            foreach (var actividad in DependenciasProyecto.Aplanar(proyecto))
            {
                if (!actividad.PredecesoraIds.Any(muertas.Contains)) continue;
                actividad.FijarPredecesoras(
                    actividad.PredecesoraIds.Where(id => !muertas.Contains(id)).ToList());
            }

            // Y las filas que ya estaban en la base apuntando a esas actividades. Del lado de la
            // sucesora se irían solas en cascada; del lado de la predecesora la FK es NoAction —no
            // puede ser otra cosa sin chocar con el error 1785— y el DELETE fallaría con ellas ahí.
            var dependencias = await ctx.ProyectoDependencias
                .Where(d => resultado.ActividadesBorradas.Contains(d.PredecesoraId)
                         || resultado.ActividadesBorradas.Contains(d.SucesoraId))
                .ToListAsync(ct);

            ctx.ProyectoDependencias.RemoveRange(dependencias);
        }

        // El grafo se valida entero y al final: los ciclos y las referencias fuera del proyecto no
        // se ven fila por fila, solo con la estructura ya reconciliada.
        DependenciasProyecto.Validar(proyecto);

        // El avance del proyecto es el del árbol: cambiar la estructura lo mueve, aunque nadie
        // haya reportado nada.
        proyecto.RecalcularAvance(proyecto.Entregables);

        // Auditoría: la ficha se puede reescribir entera, así que algo tiene que quedar fijo.
        // Solo se registra si algo cambió — un guardado sin cambios no ensucia el historial.
        var actor = currentUser.Nombre ?? "—";
        if (cambiosFicha is { Length: > 0 })
            ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
                proyecto.Id, TipoEventoProyecto.ModificacionFicha, cambiosFicha, actor));
        if (resultado.Resumen is { Length: > 0 })
            ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
                proyecto.Id, TipoEventoProyecto.ModificacionEstructura, resultado.Resumen, actor));

        await ctx.SaveChangesAsync(ct);

        if (areaOUnidadCambio)
            await sync.SincronizarProyectoAsync(cmd.Id, ct);

        return Unit.Value;
    }

    /// <summary>Resume qué campos de la ficha cambian, comparando contra el estado actual.</summary>
    private static string DiffFicha(Proyecto p, ActualizarProyectoCommand cmd, string nombreLimpio)
    {
        var partes = new List<string>();
        var objetivo = string.IsNullOrWhiteSpace(cmd.Objetivo) ? null : cmd.Objetivo.Trim();
        var responsable = string.IsNullOrWhiteSpace(cmd.Responsable) ? null : cmd.Responsable.Trim();

        if (p.Nombre != nombreLimpio)          partes.Add($"nombre: «{p.Nombre}» → «{nombreLimpio}»");
        if (p.Objetivo != objetivo)            partes.Add("objetivo actualizado");
        if (p.ResponsableId != cmd.ResponsableId)
            partes.Add($"responsable: {p.Responsable ?? "sin asignar"} → {responsable ?? "sin asignar"}");
        if (p.Prioridad != cmd.Prioridad)      partes.Add($"prioridad: {p.Prioridad} → {cmd.Prioridad}");

        // El alcance decide quién ve el proyecto: cambiarlo merece quedar registrado.
        var area   = string.IsNullOrWhiteSpace(cmd.AreaId)   ? null : cmd.AreaId.Trim();
        var unidad = string.IsNullOrWhiteSpace(cmd.UnidadId) ? null : cmd.UnidadId.Trim();
        if (p.AreaId   != area)   partes.Add($"área: {p.AreaId ?? "transversal"} → {area ?? "transversal"}");
        if (p.UnidadId != unidad) partes.Add($"unidad: {p.UnidadId ?? "transversal"} → {unidad ?? "transversal"}");
        if (p.FechaInicioPlan != cmd.FechaInicioPlan)
            partes.Add($"inicio planificado: {Fecha(p.FechaInicioPlan)} → {Fecha(cmd.FechaInicioPlan)}");
        if (p.FechaFinPlan != cmd.FechaFinPlan)
            partes.Add($"cierre planificado: {Fecha(p.FechaFinPlan)} → {Fecha(cmd.FechaFinPlan)}");

        return string.Join("; ", partes);
    }

    private static string Fecha(DateOnly? f) => f?.ToString("dd/MM/yyyy") ?? "sin fecha";

    /// <summary>Lo que dejó la reconciliación: el resumen para la bitácora y los Ids de las
    /// actividades que hay que desimputar antes de que EF las borre.</summary>
    private sealed record ResultadoEstructura(string Resumen, List<int> ActividadesBorradas);

    /// <summary>
    /// Reconcilia la estructura por Id en vez de reemplazarla en bloque.
    ///
    /// <para><b>Por qué no se reemplaza en bloque</b>, a diferencia del editor de expedientes: los
    /// entregables y las actividades son destino de FK desde <c>ProyectoAvances</c>. Borrar y
    /// recrear —aunque el contenido sea idéntico— le da a cada fila un Id nuevo y desimputa en
    /// silencio todos los avances que la referenciaban. No era un caso borde de "entregable
    /// eliminado": pasaba en cada guardado de la ficha, aunque no se tocara nada.</para>
    ///
    /// <para>El orden no se toca: los que ya existían conservan el suyo y los nuevos van al final.
    /// Reordenar es atribución del responsable del proyecto y tiene su propio comando.</para>
    /// </summary>
    private static ResultadoEstructura ReconciliarEstructura(
        Proyecto proyecto,
        IReadOnlyList<EntregableInput> entrada,
        HashSet<Guid> interesados)
    {
        var hoy       = DateOnly.FromDateTime(DateTime.UtcNow);
        var validos   = entrada.Where(e => !string.IsNullOrWhiteSpace(e.Nombre)).ToList();
        var idsSiguen = validos.Where(e => e.Id > 0).Select(e => e.Id).ToHashSet();
        var borradas  = new List<int>();

        // 1. Los entregables que el usuario quitó. Sus actividades se van en cascada, así que sus
        //    Ids entran a la lista de desimputación igual que los de una actividad borrada suelta.
        var quitados = proyecto.Entregables.Where(e => !idsSiguen.Contains(e.Id)).ToList();
        foreach (var sobrante in quitados)
        {
            borradas.AddRange(sobrante.Actividades.Select(a => a.Id).Where(id => id > 0));
            proyecto.QuitarEntregable(sobrante);
        }

        // 2. Los que siguen: se actualizan en su lugar, conservando Id y Orden.
        var porId    = proyecto.Entregables.ToDictionary(e => e.Id);
        var editados = 0;
        var detalleActividades = new List<string>();

        foreach (var input in validos.Where(e => e.Id > 0))
        {
            // Un Id que no está en el proyecto no se inventa: viene de un formulario viejo o de
            // otra pestaña. Se ignora en vez de crear un entregable fantasma.
            if (!porId.TryGetValue(input.Id, out var entregable)) continue;

            ResponsablesProyecto.Exigir(
                input.ResponsableId, entregable.ResponsableId, interesados, "un entregable", input.Nombre.Trim());

            var cambio = entregable.Definir(
                input.Nombre, input.Descripcion, input.FechaPlan, input.ResponsableId, input.Responsable);
            cambio |= entregable.CambiarEstado(input.Estado);

            var (cambioAct, borradasAct, detalle) =
                ReconciliarActividades(entregable, input.Actividades, interesados, hoy);

            borradas.AddRange(borradasAct);
            if (detalle is { Length: > 0 }) detalleActividades.Add($"«{entregable.Nombre}» → {detalle}");
            if (cambio || cambioAct) editados++;
        }

        // 3. Los nuevos, al final.
        var nuevos = validos.Where(e => e.Id <= 0).ToList();
        var orden  = proyecto.SiguienteOrden();
        foreach (var input in nuevos)
        {
            ResponsablesProyecto.Exigir(
                input.ResponsableId, null, interesados, "un entregable", input.Nombre.Trim());

            var entregable = EntregableProyecto.Crear(input.Nombre, orden++);
            entregable.Definir(
                input.Nombre, input.Descripcion, input.FechaPlan, input.ResponsableId, input.Responsable);
            entregable.CambiarEstado(input.Estado);
            ReconciliarActividades(entregable, input.Actividades, interesados, hoy);
            proyecto.Agregar(entregable);
        }

        var partes = new List<string>();
        if (nuevos.Count   > 0) partes.Add($"agregados: {string.Join(", ", nuevos.Select(n => $"«{n.Nombre.Trim()}»"))}");
        if (quitados.Count > 0) partes.Add($"quitados: {string.Join(", ", quitados.Select(q => $"«{q.Nombre}»"))}");
        if (editados       > 0) partes.Add($"{editados} entregable{(editados == 1 ? "" : "s")} modificado{(editados == 1 ? "" : "s")}");
        partes.AddRange(detalleActividades);

        return new ResultadoEstructura(string.Join("; ", partes), borradas);
    }

    /// <summary>Mismo criterio que con los entregables, un nivel más abajo.</summary>
    private static (bool Cambio, List<int> Borradas, string Detalle) ReconciliarActividades(
        EntregableProyecto entregable,
        IReadOnlyList<ActividadInput> entrada,
        HashSet<Guid> interesados,
        DateOnly hoy)
    {
        var validas   = (entrada ?? []).Where(a => !string.IsNullOrWhiteSpace(a.Nombre)).ToList();
        var idsSiguen = validas.Where(a => a.Id > 0).Select(a => a.Id).ToHashSet();
        var borradas  = new List<int>();

        var quitadas = entregable.Actividades.Where(a => !idsSiguen.Contains(a.Id)).ToList();
        foreach (var sobrante in quitadas)
        {
            if (sobrante.Id > 0) borradas.Add(sobrante.Id);
            entregable.QuitarActividad(sobrante);
        }

        var porId    = entregable.Actividades.ToDictionary(a => a.Id);
        var editadas = 0;
        var conDeps  = 0;
        foreach (var input in validas.Where(a => a.Id > 0))
        {
            if (!porId.TryGetValue(input.Id, out var actividad)) continue;

            ResponsablesProyecto.Exigir(
                input.ResponsableId, actividad.ResponsableId, interesados, "una actividad", input.Nombre.Trim());

            var (cambio, dependencias) = Aplicar(actividad, input, hoy);
            if (cambio)       editadas++;
            if (dependencias) conDeps++;
        }

        var nuevas = validas.Where(a => a.Id <= 0).ToList();
        var orden  = entregable.SiguienteOrdenActividad();
        foreach (var input in nuevas)
        {
            ResponsablesProyecto.Exigir(
                input.ResponsableId, null, interesados, "una actividad", input.Nombre.Trim());

            var actividad = ActividadProyecto.Crear(input.Nombre, orden++);
            Aplicar(actividad, input, hoy);
            entregable.Agregar(actividad);
        }

        var partes = new List<string>();
        if (nuevas.Count   > 0) partes.Add($"{nuevas.Count} actividad{(nuevas.Count == 1 ? "" : "es")} agregada{(nuevas.Count == 1 ? "" : "s")}");
        if (quitadas.Count > 0) partes.Add($"{quitadas.Count} quitada{(quitadas.Count == 1 ? "" : "s")}");
        if (editadas       > 0) partes.Add($"{editadas} modificada{(editadas == 1 ? "" : "s")}");
        // Aparte de «modificadas», aunque las incluya: cambiar de qué depende una actividad no se
        // ve en ninguna columna de la tabla y sin nombrarlo la auditoría no lo registraría.
        if (conDeps        > 0) partes.Add($"{conDeps} con dependencias cambiadas");

        return (nuevas.Count + quitadas.Count + editadas > 0, borradas, string.Join(", ", partes));
    }

    /// <summary>
    /// Vuelca una fila del editor sobre la actividad.
    ///
    /// <para><b>Cancelar congela el porcentaje</b> en lo último que se reportó: una actividad
    /// cancelada sale del promedio del entregable, así que su número deja de importar y
    /// sobrescribirlo con lo que quedó en el formulario solo perdería el dato de dónde se quedó.</para>
    /// </summary>
    /// <returns>Qué cambió: el segundo miembro dice si se movieron las dependencias, que la
    /// bitácora nombra aparte — cambiar de quién depende una actividad no es lo mismo que
    /// corregirle el nombre.</returns>
    private static (bool Cambio, bool Dependencias) Aplicar(
        ActividadProyecto actividad, ActividadInput input, DateOnly hoy)
    {
        var cambio = actividad.Definir(
            input.Nombre, input.Descripcion, input.FechaInicioPlan, input.FechaFinPlan,
            input.ResponsableId, input.Responsable);

        if (input.Cancelada)
        {
            cambio |= actividad.Cancelar();
        }
        else
        {
            // Reactivar primero: Reportar se niega a tocar una actividad cancelada.
            cambio |= actividad.Reactivar();
            cambio |= actividad.Reportar(input.AvancePct, hoy);
        }

        // Al final: Definir y Reportar no las tocan, y así el cambio de dependencias queda aislado
        // para poder nombrarlo en la bitácora.
        var dependencias = actividad.FijarPredecesoras(input.Predecesoras);

        return (cambio || dependencias, dependencias);
    }
}

// ── Cambiar estado ────────────────────────────────────────────────────────
public sealed record CambiarEstadoProyectoCommand(int Id, EstadoProyecto Nuevo) : IRequest<Unit>;

public sealed class CambiarEstadoProyectoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<CambiarEstadoProyectoCommand, Unit>
{
    public async Task<Unit> Handle(CambiarEstadoProyectoCommand cmd, CancellationToken ct)
    {
        var proyecto = await ctx.Proyectos.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Proyecto), cmd.Id);

        // La validación de la transición y el evento los pone el dominio.
        proyecto.CambiarEstado(cmd.Nuevo, currentUser.Nombre ?? "—");

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Reabrir ───────────────────────────────────────────────────────────────
/// <summary>
/// Devuelve a ejecución un proyecto cerrado o cancelado, con motivo obligatorio.
///
/// <para>Es un comando aparte y no una opción del selector de estado: revertir un cierre no es un
/// cambio de estado más, y el motivo es lo único que después permite entender por qué un proyecto
/// terminado volvió a estar abierto.</para>
/// </summary>
public sealed record ReabrirProyectoCommand(int Id, string Motivo) : IRequest<Unit>;

public sealed class ReabrirProyectoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<ReabrirProyectoCommand, Unit>
{
    public async Task<Unit> Handle(ReabrirProyectoCommand cmd, CancellationToken ct)
    {
        var proyecto = await ctx.Proyectos.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Proyecto), cmd.Id);

        var actor    = currentUser.Nombre ?? "—";
        var anterior = proyecto.Estado;

        // El dominio valida que haya algo que reabrir, exige el motivo y emite el evento.
        proyecto.Reabrir(cmd.Motivo, actor);

        ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
            proyecto.Id, TipoEventoProyecto.CambioEstado,
            $"Proyecto reabierto desde «{anterior}»: {cmd.Motivo.Trim()}", actor));

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Eliminar (lógico) ─────────────────────────────────────────────────────
public sealed record EliminarProyectoCommand(int Id) : IRequest<Unit>;

public sealed class EliminarProyectoCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<EliminarProyectoCommand, Unit>
{
    public async Task<Unit> Handle(EliminarProyectoCommand cmd, CancellationToken ct)
    {
        var proyecto = await ctx.Proyectos.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Proyecto), cmd.Id);

        proyecto.IsDeleted = true;
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Guarda de propiedad ───────────────────────────────────────────────────
/// <summary>
/// Acciones reservadas al responsable del proyecto: reordenar la estructura y corregir la bitácora.
///
/// <para><b>Sin bypass de administrador, a propósito.</b> El resto del portal deja pasar a
/// <c>EsAdministrador</c> por código, pero acá se pidió expresamente que fueran del propietario y
/// de nadie más. Consecuencia a tener presente: un proyecto <b>sin responsable asignado</b> no
/// admite ninguna de las dos acciones — ni siquiera para un administrador — hasta que se le asigne
/// uno desde la ficha. El mensaje de error lo dice para que no parezca una falla.</para>
/// </summary>
internal static class PropiedadProyecto
{
    public static void Exigir(Proyecto proyecto, ICurrentUserService usuario)
    {
        if (proyecto.ResponsableId is null)
            throw new DomainException(
                "El proyecto no tiene responsable asignado. Asígnelo en la ficha para habilitar esta acción.");

        if (usuario.UserId is null || usuario.UserId != proyecto.ResponsableId)
            throw new DomainException(
                $"Solo el responsable del proyecto puede realizar esta acción" +
                (proyecto.Responsable is { Length: > 0 } r ? $" ({r})." : "."));
    }
}

// ── Reordenar entregables ─────────────────────────────────────────────────
/// <summary>Recibe los Ids de los entregables en el orden deseado. Ver
/// <see cref="Proyecto.ReordenarEntregables"/> para por qué exige la lista completa.</summary>
public sealed record ReordenarEntregablesCommand(
    int ProyectoId,
    IReadOnlyList<int> EntregableIdsEnOrden) : IRequest<Unit>;

public sealed class ReordenarEntregablesCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<ReordenarEntregablesCommand, Unit>
{
    public async Task<Unit> Handle(ReordenarEntregablesCommand cmd, CancellationToken ct)
    {
        var proyecto = await ctx.Proyectos.Include(p => p.Entregables)
            .FirstOrDefaultAsync(p => p.Id == cmd.ProyectoId, ct)
            ?? throw new NotFoundException(nameof(Proyecto), cmd.ProyectoId);

        PropiedadProyecto.Exigir(proyecto, currentUser);

        if (proyecto.Estado is EstadoProyecto.Cerrado or EstadoProyecto.Cancelado)
            throw new DomainException($"El proyecto está «{proyecto.Estado}» y ya no admite cambios.");

        proyecto.ReordenarEntregables(cmd.EntregableIdsEnOrden);

        ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
            proyecto.Id, TipoEventoProyecto.ModificacionEstructura,
            "Se reordenaron los entregables: " +
            string.Join(" → ", proyecto.Entregables.OrderBy(e => e.Orden).Select(e => $"«{e.Nombre}»")),
            currentUser.Nombre ?? "—"));

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Reordenar actividades ─────────────────────────────────────────────────
/// <summary>Reordena las actividades dentro de un entregable. Misma atribución que reordenar
/// entregables: el cronograma es del responsable del proyecto.</summary>
public sealed record ReordenarActividadesCommand(
    int ProyectoId,
    int EntregableId,
    IReadOnlyList<int> ActividadIdsEnOrden) : IRequest<Unit>;

public sealed class ReordenarActividadesCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<ReordenarActividadesCommand, Unit>
{
    public async Task<Unit> Handle(ReordenarActividadesCommand cmd, CancellationToken ct)
    {
        var proyecto = await ProyectoConEstructura.CargarAsync(ctx, cmd.ProyectoId, ct)
            ?? throw new NotFoundException(nameof(Proyecto), cmd.ProyectoId);

        PropiedadProyecto.Exigir(proyecto, currentUser);

        if (proyecto.Estado is EstadoProyecto.Cerrado or EstadoProyecto.Cancelado)
            throw new DomainException($"El proyecto está «{proyecto.Estado}» y ya no admite cambios.");

        var entregable = proyecto.Entregables.FirstOrDefault(e => e.Id == cmd.EntregableId)
            ?? throw new DomainException("El entregable indicado no pertenece a este proyecto.");

        entregable.ReordenarActividades(cmd.ActividadIdsEnOrden);

        ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
            proyecto.Id, TipoEventoProyecto.ModificacionEstructura,
            $"Se reordenaron las actividades de «{entregable.Nombre}»: " +
            string.Join(" → ", entregable.Actividades.OrderBy(a => a.Orden).Select(a => $"«{a.Nombre}»")),
            currentUser.Nombre ?? "—"));

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Corregir una entrada de la bitácora ───────────────────────────────────
public sealed record ActualizarAvanceCommand(
    int     AvanceId,
    string  Descripcion,
    string? Bloqueo      = null,
    int?    EntregableId = null,
    int?    ActividadId  = null) : IRequest<Unit>;

public sealed class ActualizarAvanceCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<ActualizarAvanceCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarAvanceCommand cmd, CancellationToken ct)
    {
        var avance = await ctx.ProyectoAvances.FirstOrDefaultAsync(a => a.Id == cmd.AvanceId, ct)
            ?? throw new NotFoundException(nameof(AvanceProyecto), cmd.AvanceId);

        var proyecto = await ctx.Proyectos.FirstOrDefaultAsync(p => p.Id == avance.ProyectoId, ct)
            ?? throw new NotFoundException(nameof(Proyecto), avance.ProyectoId);

        PropiedadProyecto.Exigir(proyecto, currentUser);

        if (proyecto.Estado is EstadoProyecto.Cerrado or EstadoProyecto.Cancelado)
            throw new DomainException($"El proyecto está «{proyecto.Estado}» y su bitácora quedó cerrada.");

        // Misma regla que al registrar: la imputación no puede salirse del proyecto.
        if (cmd.EntregableId is { } entregableId)
        {
            var pertenece = await ctx.ProyectoEntregables
                .AnyAsync(e => e.Id == entregableId && e.ProyectoId == avance.ProyectoId, ct);
            if (!pertenece)
                throw new DomainException("El entregable indicado no pertenece a este proyecto.");

            if (cmd.ActividadId is { } actividadId)
            {
                var suya = await ctx.ProyectoActividades
                    .AnyAsync(a => a.Id == actividadId && a.EntregableId == entregableId, ct);
                if (!suya)
                    throw new DomainException("La actividad indicada no pertenece a ese entregable.");
            }
        }

        avance.Actualizar(cmd.Descripcion, cmd.Bloqueo, cmd.EntregableId, cmd.ActividadId, currentUser.Nombre ?? "—");

        // El avance guarda su propio sello de edición; esto deja además el rastro en la auditoría,
        // que es la que no se puede tocar.
        ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
            proyecto.Id, TipoEventoProyecto.CorreccionBitacora,
            $"Se corrigió la entrada de bitácora del {avance.Fecha:dd/MM/yyyy} reportada por {avance.Autor}.",
            currentUser.Nombre ?? "—"));

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Registrar avance (la bitácora de ejecución) ───────────────────────────
/// <summary>
/// Reporta qué se hizo y, si el reporte se imputa a una actividad, cuánto lleva esa actividad.
///
/// <para><b>El porcentaje es de la actividad, no del proyecto.</b> Hasta el 2026-08-25 este comando
/// recibía el avance del proyecto entero y lo guardaba tal cual; ahora el número sube por el árbol
/// —actividad, entregable, proyecto— y el reporte sin actividad es simplemente una nota de
/// ejecución, que sigue siendo válida y no mueve ningún indicador.</para>
/// </summary>
public sealed record RegistrarAvanceCommand(
    int     ProyectoId,
    string  Descripcion,
    int?    EntregableId  = null,
    int?    ActividadId   = null,

    /// <summary>Nuevo porcentaje de la actividad imputada. Null = el reporte no mueve el número.</summary>
    int?    PorcentajeActividad = null,

    string? Bloqueo       = null,
    string? ArchivoNombre = null,
    string? ArchivoUrl    = null,
    long?   ArchivoTamano = null,

    /// <summary>Da por cumplido el entregable al que se imputa el reporte. Sigue existiendo para
    /// los entregables sin desglosar: los que tienen actividades se cierran solos cuando todas
    /// llegan al 100 %.</summary>
    bool    CompletarEntregable = false,

    /// <summary>Riesgo del registro que este bloqueo confirma. Al vincularlo, el riesgo pasa a
    /// «Materializado»: deja de ser algo que podría pasar y se vuelve algo que está pasando.</summary>
    int?    RiesgoId      = null) : IRequest<int>;

public sealed class RegistrarAvanceCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<RegistrarAvanceCommand, int>
{
    public async Task<int> Handle(RegistrarAvanceCommand cmd, CancellationToken ct)
    {
        var proyecto = await ProyectoConEstructura.CargarAsync(ctx, cmd.ProyectoId, ct)
            ?? throw new NotFoundException(nameof(Proyecto), cmd.ProyectoId);

        if (proyecto.Estado is EstadoProyecto.Cerrado or EstadoProyecto.Cancelado)
            throw new DomainException($"El proyecto está «{proyecto.Estado}» y ya no admite reportes de avance.");

        var actor = currentUser.Nombre ?? "—";
        var hoy   = DateOnly.FromDateTime(DateTime.UtcNow);

        // La imputación se resuelve contra el árbol cargado: así una actividad de otro proyecto
        // no encuentra dónde encajar, sin necesidad de una consulta aparte por cada nivel.
        EntregableProyecto? entregable = null;
        ActividadProyecto?  actividad  = null;

        if (cmd.ActividadId is { } actividadId)
        {
            entregable = proyecto.Entregables.FirstOrDefault(e => e.Actividades.Any(a => a.Id == actividadId))
                ?? throw new DomainException("La actividad indicada no pertenece a este proyecto.");
            actividad = entregable.Actividades.First(a => a.Id == actividadId);

            // El entregable que venga en el comando tiene que ser el de la actividad: si no lo es,
            // el formulario y el árbol dejaron de estar de acuerdo y guardar sería adivinar.
            if (cmd.EntregableId is { } declarado && declarado != entregable.Id)
                throw new DomainException("La actividad indicada no pertenece a ese entregable.");
        }
        else if (cmd.EntregableId is { } entregableId)
        {
            entregable = proyecto.Entregables.FirstOrDefault(e => e.Id == entregableId)
                ?? throw new DomainException("El entregable indicado no pertenece a este proyecto.");
        }

        if (cmd.PorcentajeActividad is not null && actividad is null)
            throw new DomainException("Para reportar un porcentaje hay que imputar el avance a una actividad.");

        if (cmd.CompletarEntregable && entregable is null)
            throw new DomainException("Para dar un entregable por cumplido hay que imputarle el reporte.");

        // Igual que con la actividad: el riesgo tiene que ser del mismo proyecto.
        RiesgoProyecto? riesgo = null;
        if (cmd.RiesgoId is { } riesgoId)
        {
            riesgo = await ctx.ProyectoRiesgos
                .FirstOrDefaultAsync(r => r.Id == riesgoId && r.ProyectoId == cmd.ProyectoId, ct);
            if (riesgo is null)
                throw new DomainException("El riesgo indicado no pertenece a este proyecto.");
        }

        // El porcentaje se aplica antes de escribir la entrada: si el dominio lo rechaza —una
        // actividad cancelada, un valor fuera de rango— no queda una bitácora que diga que se
        // movió algo que no se movió.
        if (cmd.PorcentajeActividad is { } pct && actividad is not null)
            actividad.Reportar(pct, hoy);

        var avance = AvanceProyecto.Crear(
            cmd.ProyectoId,
            entregable?.Id,
            actividad?.Id,
            cmd.Descripcion,
            cmd.PorcentajeActividad,
            actor,
            cmd.Bloqueo,
            cmd.ArchivoNombre,
            cmd.ArchivoUrl,
            cmd.ArchivoTamano,
            cmd.RiesgoId);

        ctx.ProyectoAvances.Add(avance);

        // Cerrar el entregable es un cambio del cronograma, no del reporte: va a la bitácora del
        // proyecto aparte, y solo si el entregable realmente cambió de estado.
        if (cmd.CompletarEntregable && entregable is not null && entregable.Completar(hoy))
        {
            ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
                cmd.ProyectoId, TipoEventoProyecto.ModificacionEstructura,
                $"Entregable «{entregable.Nombre}» dado por cumplido al registrar el avance.",
                actor));
        }
        // El entregable desglosado se cierra solo: si sus actividades llegaron todas al 100 %, no
        // hay nada más que entregar y esperar a que alguien lo marque a mano es lo que antes dejaba
        // el cronograma quieto mientras el avance subía.
        else if (entregable is not null && entregable.ActividadesTerminadas() && entregable.Completar(hoy))
        {
            ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
                cmd.ProyectoId, TipoEventoProyecto.ModificacionEstructura,
                $"Entregable «{entregable.Nombre}» quedó cumplido: todas sus actividades llegaron al 100 %.",
                actor));
        }

        // El avance del proyecto sale del árbol, ya con el porcentaje nuevo aplicado.
        proyecto.RecalcularAvance(proyecto.Entregables);

        // El riesgo que se cumple deja de ser una previsión. No se toca si ya estaba materializado
        // ni si alguien lo había cerrado: reabrir un riesgo cerrado es una decisión, no un efecto
        // secundario de reportar un avance.
        if (riesgo is not null && riesgo.Estado is EstadoRiesgo.Abierto or EstadoRiesgo.EnTratamiento)
        {
            var anterior = riesgo.Estado;
            riesgo.CambiarEstado(EstadoRiesgo.Materializado);

            ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
                cmd.ProyectoId, TipoEventoProyecto.Riesgo,
                $"Riesgo «{Recortar(riesgo.Descripcion)}» pasó de {anterior} a Materializado: " +
                "se reportó como bloqueo de la ejecución.",
                actor));
        }

        await ctx.SaveChangesAsync(ct);
        return avance.Id;
    }

    /// <summary>La descripción de un riesgo llega hasta 500 caracteres y la bitácora la cita:
    /// entera taparía el resto de la entrada.</summary>
    private static string Recortar(string texto, int max = 70) =>
        texto.Length <= max ? texto : texto[..max].TrimEnd() + "…";
}
