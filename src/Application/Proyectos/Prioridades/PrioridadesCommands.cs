using FluentValidation;

namespace Diger.TramitesEstado.Application.Proyectos.Prioridades;

/// <summary>
/// Resuelve qué prioridad guardar cuando quien llama no trae una elegida: el alta desde el
/// listado rápido, una importación, un comando viejo. Vive acá y no en el handler de proyectos
/// porque la regla —cuál es la predeterminada— es del catálogo.
/// </summary>
public static class PrioridadProyectoResolver
{
    /// <param name="solicitada">Id elegido por quien llama; null o 0 significa «la que corresponda».</param>
    public static async Task<int> ResolverAsync(
        IApplicationDbContext ctx, int? solicitada, CancellationToken ct)
    {
        if (solicitada is int id and > 0)
        {
            if (!await ctx.PrioridadesProyecto.AnyAsync(p => p.Id == id, ct))
                throw new DomainException("La prioridad seleccionada no existe.");
            return id;
        }

        // Predeterminada primero; si nadie la marcó, la de menor orden entre las activas. El
        // respaldo evita que un catálogo mal configurado impida crear proyectos, que sería
        // desproporcionado.
        var porDefecto = await ctx.PrioridadesProyecto
            .Where(p => p.Activo)
            .OrderByDescending(p => p.EsPredeterminada).ThenBy(p => p.Orden).ThenBy(p => p.Nombre)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);

        return porDefecto
            ?? throw new DomainException(
                "No hay ninguna prioridad activa en el catálogo. Configure al menos una en Catálogos › Prioridades.");
    }
}

// ── Crear ──────────────────────────────────────────────────────────────────
public sealed record CrearPrioridadProyectoCommand(string Nombre, int Orden, ColorEtiqueta Color)
    : IRequest<int>;

public sealed class CrearPrioridadProyectoCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<CrearPrioridadProyectoCommand, int>
{
    public async Task<int> Handle(CrearPrioridadProyectoCommand cmd, CancellationToken ct)
    {
        var nombre = cmd.Nombre.Trim();
        if (await ctx.PrioridadesProyecto.AnyAsync(p => p.Nombre == nombre, ct))
            throw new DomainException($"Ya existe una prioridad llamada «{nombre}».");

        var prioridad = PrioridadProyecto.Crear(nombre, cmd.Orden, cmd.Color);
        ctx.PrioridadesProyecto.Add(prioridad);
        await ctx.SaveChangesAsync(ct);
        return prioridad.Id;
    }
}

// ── Actualizar ─────────────────────────────────────────────────────────────
public sealed record ActualizarPrioridadProyectoCommand(
    int Id, string Nombre, int Orden, ColorEtiqueta Color, bool Activo) : IRequest<Unit>;

public sealed class ActualizarPrioridadProyectoCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<ActualizarPrioridadProyectoCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarPrioridadProyectoCommand cmd, CancellationToken ct)
    {
        var prioridad = await ctx.PrioridadesProyecto.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(PrioridadProyecto), cmd.Id);

        var nombre = cmd.Nombre.Trim();
        if (await ctx.PrioridadesProyecto.AnyAsync(p => p.Nombre == nombre && p.Id != cmd.Id, ct))
            throw new DomainException($"Ya existe una prioridad llamada «{nombre}».");

        // Desactivar la última activa dejaría el catálogo sin nada que ofrecer al clasificar.
        if (!cmd.Activo && prioridad.Activo
            && !await ctx.PrioridadesProyecto.AnyAsync(p => p.Activo && p.Id != cmd.Id, ct))
            throw new DomainException("Debe quedar al menos una prioridad activa.");

        prioridad.Actualizar(nombre, cmd.Orden, cmd.Color, cmd.Activo);
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Marcar como predeterminada ─────────────────────────────────────────────
/// <summary>
/// Va por comando propio y no como un campo más de «actualizar» porque toca dos filas: la que
/// se marca y la que se desmarca. Mezclarlo con la edición habría hecho que guardar una
/// prioridad cualquiera pudiera, sin querer, cambiar cuál es la predeterminada.
/// </summary>
public sealed record MarcarPrioridadProyectoPredeterminadaCommand(int Id) : IRequest<Unit>;

public sealed class MarcarPrioridadProyectoPredeterminadaCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<MarcarPrioridadProyectoPredeterminadaCommand, Unit>
{
    public async Task<Unit> Handle(
        MarcarPrioridadProyectoPredeterminadaCommand cmd, CancellationToken ct)
    {
        var todas = await ctx.PrioridadesProyecto.ToListAsync(ct);
        var elegida = todas.FirstOrDefault(p => p.Id == cmd.Id)
            ?? throw new NotFoundException(nameof(PrioridadProyecto), cmd.Id);

        foreach (var p in todas.Where(p => p.EsPredeterminada && p.Id != cmd.Id))
            p.FijarPredeterminada(false);

        elegida.FijarPredeterminada(true);
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Eliminar ───────────────────────────────────────────────────────────────
public sealed record EliminarPrioridadProyectoCommand(int Id) : IRequest<Unit>;

public sealed class EliminarPrioridadProyectoCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<EliminarPrioridadProyectoCommand, Unit>
{
    public async Task<Unit> Handle(EliminarPrioridadProyectoCommand cmd, CancellationToken ct)
    {
        var prioridad = await ctx.PrioridadesProyecto.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(PrioridadProyecto), cmd.Id);

        if (prioridad.EsPredeterminada)
            throw new DomainException(
                "No se puede eliminar la prioridad predeterminada. Marque otra como predeterminada primero.");

        // IgnoreQueryFilters: un proyecto borrado lógicamente sigue apuntando a esta fila, y la
        // llave foránea no distingue. Contarlo evita prometer un borrado que la base rechazaría.
        var enUso = await ctx.Proyectos.IgnoreQueryFilters().CountAsync(x => x.PrioridadId == cmd.Id, ct);
        if (enUso > 0)
            throw new DomainException(
                $"No se puede eliminar: {enUso} proyecto(s) la tienen asignada. Desactívela en su lugar.");

        if (!await ctx.PrioridadesProyecto.AnyAsync(p => p.Activo && p.Id != cmd.Id, ct))
            throw new DomainException("Debe quedar al menos una prioridad activa.");

        ctx.PrioridadesProyecto.Remove(prioridad);
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Validadores ────────────────────────────────────────────────────────────
public sealed class CrearPrioridadProyectoCommandValidator
    : AbstractValidator<CrearPrioridadProyectoCommand>
{
    public CrearPrioridadProyectoCommandValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre de la prioridad es obligatorio.")
                              .MaximumLength(40);
        RuleFor(x => x.Orden).InclusiveBetween(0, 999).WithMessage("El orden debe estar entre 0 y 999.");
        RuleFor(x => x.Color).IsInEnum().WithMessage("El color seleccionado no es válido.");
    }
}

public sealed class ActualizarPrioridadProyectoCommandValidator
    : AbstractValidator<ActualizarPrioridadProyectoCommand>
{
    public ActualizarPrioridadProyectoCommandValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre de la prioridad es obligatorio.")
                              .MaximumLength(40);
        RuleFor(x => x.Orden).InclusiveBetween(0, 999).WithMessage("El orden debe estar entre 0 y 999.");
        RuleFor(x => x.Color).IsInEnum().WithMessage("El color seleccionado no es válido.");
    }
}
