using FluentValidation;

namespace Diger.TramitesEstado.Application.Proyectos.Commands;

// ── Agregar a la estructura ───────────────────────────────────────────────
// La puerta de las herramientas MCP crear_entregable y crear_actividad.
//
// No pasan por ActualizarProyectoCommand a propósito: ese comando reconcilia contra la lista
// entera de entregables —lo que no venga se borra— y devuelve Unit. Para un agente eso es lo peor
// de los dos mundos: una omisión destruye estructura, y después de crear un entregable no tiene su
// Id para colgarle actividades. Estos solo agregan y devuelven el Id.

/// <summary>Lo común a los dos altas: el proyecto con su árbol, abierto, y el responsable.</summary>
internal static class EstructuraAbierta
{
    /// <summary>
    /// El proyecto con su estructura completa —el recálculo del avance la necesita entera— y
    /// todavía abierto.
    ///
    /// <para>La ficha impide editar un proyecto cerrado, pero lo hace la pantalla
    /// (<c>PuedeEditar</c>), no el comando. Acá no hay pantalla, así que la regla va en el
    /// handler, con el mismo criterio que el registro de avances.</para>
    /// </summary>
    public static async Task<Proyecto> CargarAsync(IApplicationDbContext ctx, int proyectoId, CancellationToken ct)
    {
        var proyecto = await ProyectoConEstructura.CargarAsync(ctx, proyectoId, ct)
            ?? throw new NotFoundException(nameof(Proyecto), proyectoId);

        if (proyecto.Estado is EstadoProyecto.Cerrado or EstadoProyecto.Cancelado)
            throw new DomainException(
                $"El proyecto está «{proyecto.Estado}» y su estructura ya no admite cambios. " +
                "Para agregarle algo hay que reabrirlo desde la ficha.");

        return proyecto;
    }

    /// <summary>
    /// El nombre que se guarda junto al responsable. Tiene que ser interesado del proyecto —la
    /// misma regla que la ficha— y el nombre sale de ese registro, que es de donde lo toma el
    /// editor.
    /// </summary>
    public static async Task<string?> ResponsableAsync(
        IApplicationDbContext ctx, int proyectoId, Guid? responsableId, string que, string nombre,
        CancellationToken ct)
    {
        if (responsableId is null) return null;

        var interesados = await ctx.ProyectoInteresados.AsNoTracking()
            .Where(i => i.ProyectoId == proyectoId)
            .Select(i => new { i.UsuarioId, i.Nombre })
            .ToListAsync(ct);

        ResponsablesProyecto.Exigir(
            responsableId, null, interesados.Select(i => i.UsuarioId).ToHashSet(), que, nombre);

        return interesados.First(i => i.UsuarioId == responsableId).Nombre;
    }
}

// ── Entregable ────────────────────────────────────────────────────────────
public sealed record AgregarEntregableCommand(
    int       ProyectoId,
    string    Nombre,
    string?   Descripcion   = null,
    DateOnly? FechaPlan     = null,
    Guid?     ResponsableId = null) : IRequest<int>;

public sealed class AgregarEntregableCommandHandler(IApplicationDbContext ctx, ICurrentUserService currentUser)
    : IRequestHandler<AgregarEntregableCommand, int>
{
    public async Task<int> Handle(AgregarEntregableCommand cmd, CancellationToken ct)
    {
        var proyecto = await EstructuraAbierta.CargarAsync(ctx, cmd.ProyectoId, ct);

        var nombre      = (cmd.Nombre ?? "").Trim();
        var responsable = await EstructuraAbierta.ResponsableAsync(
            ctx, proyecto.Id, cmd.ResponsableId, "un entregable", nombre, ct);

        // Al final del orden vigente, como una fila nueva de la ficha de quien no puede reordenar.
        var entregable = EntregableProyecto.Crear(nombre, proyecto.SiguienteOrden());
        entregable.Definir(nombre, cmd.Descripcion, cmd.FechaPlan, cmd.ResponsableId, responsable);
        proyecto.Agregar(entregable);

        // Un entregable nuevo entra al promedio en 0: el avance del proyecto se mueve aunque nadie
        // haya reportado nada, igual que al agregarlo desde la ficha.
        proyecto.RecalcularAvance(proyecto.Entregables);

        ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
            proyecto.Id, TipoEventoProyecto.ModificacionEstructura,
            $"agregados: «{entregable.Nombre}»", currentUser.Nombre ?? "—"));

        await ctx.SaveChangesAsync(ct);
        return entregable.Id;
    }
}

public sealed class AgregarEntregableCommandValidator : AbstractValidator<AgregarEntregableCommand>
{
    public AgregarEntregableCommandValidator()
    {
        RuleFor(x => x.ProyectoId).GreaterThan(0);
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El entregable necesita un nombre.")
            .MaximumLength(EntregableProyecto.MaxNombre);
        RuleFor(x => x.Descripcion).MaximumLength(EntregableProyecto.MaxDescripcion);
    }
}

// ── Actividad ─────────────────────────────────────────────────────────────
public sealed record AgregarActividadCommand(
    int       ProyectoId,
    int       EntregableId,
    string    Nombre,
    string?   Descripcion     = null,
    DateOnly? FechaInicioPlan = null,
    DateOnly? FechaFinPlan    = null,
    Guid?     ResponsableId   = null) : IRequest<int>;

public sealed class AgregarActividadCommandHandler(IApplicationDbContext ctx, ICurrentUserService currentUser)
    : IRequestHandler<AgregarActividadCommand, int>
{
    public async Task<int> Handle(AgregarActividadCommand cmd, CancellationToken ct)
    {
        var proyecto = await EstructuraAbierta.CargarAsync(ctx, cmd.ProyectoId, ct);

        // Se busca dentro del proyecto pedido, no por Id suelto: con el Id de un entregable ajeno
        // la actividad terminaría en un proyecto que quien la pidió no estaba mirando.
        var entregable = proyecto.Entregables.FirstOrDefault(e => e.Id == cmd.EntregableId)
            ?? throw new DomainException("El entregable indicado no pertenece a este proyecto.");

        var nombre      = (cmd.Nombre ?? "").Trim();
        var responsable = await EstructuraAbierta.ResponsableAsync(
            ctx, proyecto.Id, cmd.ResponsableId, "una actividad", nombre, ct);

        // Nace en 0 % y Pendiente: el porcentaje lo mueve el reporte de avance, no el alta.
        var actividad = ActividadProyecto.Crear(nombre, entregable.SiguienteOrdenActividad());
        actividad.Definir(
            nombre, cmd.Descripcion, cmd.FechaInicioPlan, cmd.FechaFinPlan, cmd.ResponsableId, responsable);
        entregable.Agregar(actividad);

        // Un entregable que no tenía actividades valía por su estado; desde esta, las promedia.
        proyecto.RecalcularAvance(proyecto.Entregables);

        ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
            proyecto.Id, TipoEventoProyecto.ModificacionEstructura,
            $"«{entregable.Nombre}» → actividad agregada: «{actividad.Nombre}»", currentUser.Nombre ?? "—"));

        await ctx.SaveChangesAsync(ct);
        return actividad.Id;
    }
}

public sealed class AgregarActividadCommandValidator : AbstractValidator<AgregarActividadCommand>
{
    public AgregarActividadCommandValidator()
    {
        RuleFor(x => x.ProyectoId).GreaterThan(0);
        RuleFor(x => x.EntregableId).GreaterThan(0);
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("La actividad necesita un nombre.")
            .MaximumLength(ActividadProyecto.MaxNombre);
        RuleFor(x => x.Descripcion).MaximumLength(ActividadProyecto.MaxDescripcion);
    }
}
