using Diger.TramitesEstado.Application.Proyectos.Common;

namespace Diger.TramitesEstado.Application.Proyectos.Commands;

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
    ICurrentUserService currentUser)
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

// ── Actualizar ficha + hitos ──────────────────────────────────────────────
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
    IReadOnlyList<HitoInput> Hitos) : IRequest<Unit>;

public sealed class ActualizarProyectoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<ActualizarProyectoCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarProyectoCommand cmd, CancellationToken ct)
    {
        var proyecto = await ctx.Proyectos.Include(p => p.Hitos)
            .FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Proyecto), cmd.Id);

        if (cmd.FechaInicioPlan is { } ini && cmd.FechaFinPlan is { } fin && fin < ini)
            throw new DomainException("La fecha de cierre planificada no puede ser anterior a la de inicio.");

        var nombre = (cmd.Nombre ?? "").Trim();
        if (nombre.Length == 0) throw new DomainException("El proyecto necesita un nombre.");

        // El diff se arma ANTES de tocar nada: después las propiedades ya son las nuevas.
        var cambiosFicha = DiffFicha(proyecto, cmd, nombre);

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

        var cambiosHitos = ReconciliarHitos(proyecto, cmd.Hitos);

        // Auditoría: la ficha se puede reescribir entera, así que algo tiene que quedar fijo.
        // Solo se registra si algo cambió — un guardado sin cambios no ensucia el historial.
        var actor = currentUser.Nombre ?? "—";
        if (cambiosFicha is { Length: > 0 })
            ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
                proyecto.Id, TipoEventoProyecto.ModificacionFicha, cambiosFicha, actor));
        if (cambiosHitos is { Length: > 0 })
            ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
                proyecto.Id, TipoEventoProyecto.ModificacionHitos, cambiosHitos, actor));

        await ctx.SaveChangesAsync(ct);
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

    /// <summary>
    /// Reconcilia los hitos por Id en vez de reemplazarlos en bloque.
    ///
    /// <para><b>Por qué no se reemplaza en bloque</b>, a diferencia del editor de expedientes: los
    /// hitos son el destino de una FK desde <c>ProyectoAvances</c>, configurada en SetNull. Borrar
    /// y recrear —aunque el contenido sea idéntico— le da a cada hito un Id nuevo y desimputa en
    /// silencio todos los avances que lo referenciaban. No era un caso borde de "hito eliminado":
    /// pasaba en cada guardado de la ficha, aunque no se tocara ningún hito.</para>
    ///
    /// <para>El orden no se toca: los que ya existían conservan el suyo y los nuevos van al final.
    /// Reordenar es atribución del responsable del proyecto y tiene su propio comando.</para>
    /// </summary>
    /// <returns>Resumen de lo que cambió, para la bitácora. Cadena vacía si no cambió nada.</returns>
    private static string ReconciliarHitos(Proyecto proyecto, IReadOnlyList<HitoInput> entrada)
    {
        var validos    = entrada.Where(h => !string.IsNullOrWhiteSpace(h.Nombre)).ToList();
        var idsQueSiguen = validos.Where(h => h.Id > 0).Select(h => h.Id).ToHashSet();

        // 1. Los que el usuario quitó de la tabla.
        var quitados = proyecto.Hitos.Where(h => !idsQueSiguen.Contains(h.Id)).ToList();
        foreach (var sobrante in quitados)
            proyecto.QuitarHito(sobrante);

        // 2. Los que siguen: se actualizan en su lugar, conservando Id y Orden.
        var porId = proyecto.Hitos.ToDictionary(h => h.Id);
        var editados = 0;
        foreach (var h in validos.Where(h => h.Id > 0))
        {
            // Un Id que no está en el proyecto no se inventa: viene de un formulario viejo o de
            // otra pestaña. Se ignora en vez de crear un hito fantasma.
            if (!porId.TryGetValue(h.Id, out var hito)) continue;
            if (Aplicar(hito, h)) editados++;
        }

        // 3. Los nuevos, al final.
        var nuevos = validos.Where(h => h.Id <= 0).ToList();
        var orden  = proyecto.SiguienteOrden();
        foreach (var h in nuevos)
        {
            var hito = new HitoProyecto { Orden = orden++ };
            Aplicar(hito, h);
            proyecto.Agregar(hito);
        }

        var partes = new List<string>();
        if (nuevos.Count   > 0) partes.Add($"agregados: {string.Join(", ", nuevos.Select(n => $"«{n.Nombre.Trim()}»"))}");
        if (quitados.Count > 0) partes.Add($"quitados: {string.Join(", ", quitados.Select(q => $"«{q.Nombre}»"))}");
        if (editados       > 0) partes.Add($"{editados} hito{(editados == 1 ? "" : "s")} modificado{(editados == 1 ? "" : "s")}");
        return string.Join("; ", partes);
    }

    /// <returns><c>true</c> si algún campo cambió — lo usa el resumen de bitácora.</returns>
    private static bool Aplicar(HitoProyecto hito, HitoInput h)
    {
        var descripcion = string.IsNullOrWhiteSpace(h.Descripcion) ? null : h.Descripcion.Trim();
        var responsable = string.IsNullOrWhiteSpace(h.Responsable) ? null : h.Responsable.Trim();

        var cambio = hito.Nombre        != h.Nombre.Trim()
                  || hito.Descripcion   != descripcion
                  || hito.FechaPlan     != h.FechaPlan
                  || hito.FechaReal     != h.FechaReal
                  || hito.Estado        != h.Estado
                  || hito.ResponsableId != h.ResponsableId
                  || hito.Responsable   != responsable;

        hito.Nombre        = h.Nombre.Trim();
        hito.Descripcion   = string.IsNullOrWhiteSpace(h.Descripcion) ? null : h.Descripcion.Trim();
        hito.FechaPlan     = h.FechaPlan;
        hito.FechaReal     = h.FechaReal;
        hito.Estado        = h.Estado;
        hito.ResponsableId = h.ResponsableId;
        hito.Responsable   = responsable;
        return cambio;
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
/// Acciones reservadas al responsable del proyecto: reordenar hitos y corregir la bitácora.
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

// ── Reordenar hitos ───────────────────────────────────────────────────────
/// <summary>Recibe los Ids de los hitos en el orden deseado. Ver <see cref="Proyecto.ReordenarHitos"/>
/// para por qué exige la lista completa.</summary>
public sealed record ReordenarHitosCommand(
    int ProyectoId,
    IReadOnlyList<int> HitoIdsEnOrden) : IRequest<Unit>;

public sealed class ReordenarHitosCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<ReordenarHitosCommand, Unit>
{
    public async Task<Unit> Handle(ReordenarHitosCommand cmd, CancellationToken ct)
    {
        var proyecto = await ctx.Proyectos.Include(p => p.Hitos)
            .FirstOrDefaultAsync(p => p.Id == cmd.ProyectoId, ct)
            ?? throw new NotFoundException(nameof(Proyecto), cmd.ProyectoId);

        PropiedadProyecto.Exigir(proyecto, currentUser);

        if (proyecto.Estado is EstadoProyecto.Cerrado or EstadoProyecto.Cancelado)
            throw new DomainException($"El proyecto está «{proyecto.Estado}» y ya no admite cambios.");

        proyecto.ReordenarHitos(cmd.HitoIdsEnOrden);

        ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
            proyecto.Id, TipoEventoProyecto.ModificacionHitos,
            "Se reordenaron los hitos: " +
            string.Join(" → ", proyecto.Hitos.OrderBy(h => h.Orden).Select(h => $"«{h.Nombre}»")),
            currentUser.Nombre ?? "—"));

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Corregir una entrada de la bitácora ───────────────────────────────────
public sealed record ActualizarAvanceCommand(
    int     AvanceId,
    string  Descripcion,
    string? Bloqueo = null,
    int?    HitoId  = null) : IRequest<Unit>;

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

        // Misma regla que al registrar: un avance solo se imputa a un hito del mismo proyecto.
        if (cmd.HitoId is { } hitoId)
        {
            var pertenece = await ctx.ProyectoHitos
                .AnyAsync(h => h.Id == hitoId && h.ProyectoId == avance.ProyectoId, ct);
            if (!pertenece)
                throw new DomainException("El hito indicado no pertenece a este proyecto.");
        }

        avance.Actualizar(cmd.Descripcion, cmd.Bloqueo, cmd.HitoId, currentUser.Nombre ?? "—");

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
public sealed record RegistrarAvanceCommand(
    int     ProyectoId,
    string  Descripcion,
    int     PorcentajeReportado,
    int?    HitoId        = null,
    string? Bloqueo       = null,
    string? ArchivoNombre = null,
    string? ArchivoUrl    = null,
    long?   ArchivoTamano = null,

    /// <summary>Da por cumplido el hito al que se imputa el reporte. Sin esto el hito hay que
    /// cerrarlo aparte en la ficha, cosa que en la práctica no se hace: el avance declarado sube
    /// y el cronograma se queda quieto.</summary>
    bool    CompletarHito = false,

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
        var proyecto = await ctx.Proyectos.FirstOrDefaultAsync(p => p.Id == cmd.ProyectoId, ct)
            ?? throw new NotFoundException(nameof(Proyecto), cmd.ProyectoId);

        if (proyecto.Estado is EstadoProyecto.Cerrado or EstadoProyecto.Cancelado)
            throw new DomainException($"El proyecto está «{proyecto.Estado}» y ya no admite reportes de avance.");

        // Un avance solo puede imputarse a un hito del mismo proyecto.
        HitoProyecto? hito = null;
        if (cmd.HitoId is { } hitoId)
        {
            hito = await ctx.ProyectoHitos
                .FirstOrDefaultAsync(h => h.Id == hitoId && h.ProyectoId == cmd.ProyectoId, ct);
            if (hito is null)
                throw new DomainException("El hito indicado no pertenece a este proyecto.");
        }

        if (cmd.CompletarHito && hito is null)
            throw new DomainException("Para dar un hito por cumplido hay que imputarle el reporte.");

        // Igual que con el hito: el riesgo tiene que ser del mismo proyecto.
        RiesgoProyecto? riesgo = null;
        if (cmd.RiesgoId is { } riesgoId)
        {
            riesgo = await ctx.ProyectoRiesgos
                .FirstOrDefaultAsync(r => r.Id == riesgoId && r.ProyectoId == cmd.ProyectoId, ct);
            if (riesgo is null)
                throw new DomainException("El riesgo indicado no pertenece a este proyecto.");
        }

        var avance = AvanceProyecto.Crear(
            cmd.ProyectoId,
            cmd.HitoId,
            cmd.Descripcion,
            cmd.PorcentajeReportado,
            currentUser.Nombre ?? "—",
            cmd.Bloqueo,
            cmd.ArchivoNombre,
            cmd.ArchivoUrl,
            cmd.ArchivoTamano,
            cmd.RiesgoId);

        ctx.ProyectoAvances.Add(avance);

        // El porcentaje del proyecto es el del último reporte. El histórico queda entero
        // en ProyectoAvances; esto es solo el snapshot que lee el listado.
        proyecto.AplicarAvance(cmd.PorcentajeReportado);

        // Cerrar el hito es un cambio del cronograma, no del reporte: va a la bitácora del
        // proyecto aparte, y solo si el hito realmente cambió de estado.
        if (cmd.CompletarHito && hito is not null
            && hito.Completar(DateOnly.FromDateTime(DateTime.UtcNow)))
        {
            ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
                cmd.ProyectoId, TipoEventoProyecto.ModificacionHitos,
                $"Hito «{hito.Nombre}» dado por cumplido al registrar el avance.",
                currentUser.Nombre ?? "—"));
        }

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
                currentUser.Nombre ?? "—"));
        }

        await ctx.SaveChangesAsync(ct);
        return avance.Id;
    }

    /// <summary>La descripción de un riesgo llega hasta 500 caracteres y la bitácora la cita:
    /// entera taparía el resto de la entrada.</summary>
    private static string Recortar(string texto, int max = 70) =>
        texto.Length <= max ? texto : texto[..max].TrimEnd() + "…";
}
