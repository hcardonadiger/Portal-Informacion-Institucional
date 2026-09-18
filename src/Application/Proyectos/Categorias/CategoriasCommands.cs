using FluentValidation;

namespace Diger.TramitesEstado.Application.Proyectos.Categorias;

/// <summary>
/// Valida la categoría que trae quien guarda un proyecto. No tiene la parte de «resolver la que
/// corresponda» que sí tiene el de prioridades: la categoría es opcional, así que null no es un
/// hueco que haya que llenar sino una respuesta —«sin clasificar»—.
/// </summary>
public static class CategoriaProyectoResolver
{
    public static async Task<int?> ResolverAsync(
        IApplicationDbContext ctx, int? solicitada, CancellationToken ct)
    {
        if (solicitada is not int id || id <= 0) return null;

        if (!await ctx.CategoriasProyecto.AnyAsync(c => c.Id == id, ct))
            throw new DomainException("La categoría seleccionada no existe.");

        return id;
    }
}

// ── Crear ──────────────────────────────────────────────────────────────────
public sealed record CrearCategoriaProyectoCommand(string Nombre, int Orden, ColorEtiqueta Color)
    : IRequest<int>;

public sealed class CrearCategoriaProyectoCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<CrearCategoriaProyectoCommand, int>
{
    public async Task<int> Handle(CrearCategoriaProyectoCommand cmd, CancellationToken ct)
    {
        var nombre = cmd.Nombre.Trim();
        if (await ctx.CategoriasProyecto.AnyAsync(c => c.Nombre == nombre, ct))
            throw new DomainException($"Ya existe una categoría llamada «{nombre}».");

        var categoria = CategoriaProyecto.Crear(nombre, cmd.Orden, cmd.Color);
        ctx.CategoriasProyecto.Add(categoria);
        await ctx.SaveChangesAsync(ct);
        return categoria.Id;
    }
}

// ── Actualizar ─────────────────────────────────────────────────────────────
public sealed record ActualizarCategoriaProyectoCommand(
    int Id, string Nombre, int Orden, ColorEtiqueta Color, bool Activo) : IRequest<Unit>;

public sealed class ActualizarCategoriaProyectoCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<ActualizarCategoriaProyectoCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarCategoriaProyectoCommand cmd, CancellationToken ct)
    {
        var categoria = await ctx.CategoriasProyecto.FirstOrDefaultAsync(c => c.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(CategoriaProyecto), cmd.Id);

        var nombre = cmd.Nombre.Trim();
        if (await ctx.CategoriasProyecto.AnyAsync(c => c.Nombre == nombre && c.Id != cmd.Id, ct))
            throw new DomainException($"Ya existe una categoría llamada «{nombre}».");

        // A diferencia del catálogo de prioridades, acá no se exige que quede alguna activa: un
        // proyecto puede vivir sin categoría, así que un catálogo entero desactivado solo
        // significa que por ahora nadie clasifica.
        categoria.Actualizar(nombre, cmd.Orden, cmd.Color, cmd.Activo);
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Eliminar ───────────────────────────────────────────────────────────────
public sealed record EliminarCategoriaProyectoCommand(int Id) : IRequest<Unit>;

public sealed class EliminarCategoriaProyectoCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<EliminarCategoriaProyectoCommand, Unit>
{
    public async Task<Unit> Handle(EliminarCategoriaProyectoCommand cmd, CancellationToken ct)
    {
        var categoria = await ctx.CategoriasProyecto.FirstOrDefaultAsync(c => c.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(CategoriaProyecto), cmd.Id);

        // IgnoreQueryFilters: un proyecto borrado lógicamente sigue apuntando a esta fila y la
        // llave foránea no distingue. Contarlo evita prometer un borrado que la base rechazaría.
        var enUso = await ctx.Proyectos.IgnoreQueryFilters().CountAsync(p => p.CategoriaId == cmd.Id, ct);
        if (enUso > 0)
            throw new DomainException(
                $"No se puede eliminar: {enUso} proyecto(s) la tienen asignada. Desactívela en su lugar.");

        ctx.CategoriasProyecto.Remove(categoria);
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Validadores ────────────────────────────────────────────────────────────
public sealed class CrearCategoriaProyectoCommandValidator
    : AbstractValidator<CrearCategoriaProyectoCommand>
{
    public CrearCategoriaProyectoCommandValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre de la categoría es obligatorio.")
                              .MaximumLength(60);
        RuleFor(x => x.Orden).InclusiveBetween(0, 999).WithMessage("El orden debe estar entre 0 y 999.");
        RuleFor(x => x.Color).IsInEnum().WithMessage("El color seleccionado no es válido.");
    }
}

public sealed class ActualizarCategoriaProyectoCommandValidator
    : AbstractValidator<ActualizarCategoriaProyectoCommand>
{
    public ActualizarCategoriaProyectoCommandValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre de la categoría es obligatorio.")
                              .MaximumLength(60);
        RuleFor(x => x.Orden).InclusiveBetween(0, 999).WithMessage("El orden debe estar entre 0 y 999.");
        RuleFor(x => x.Color).IsInEnum().WithMessage("El color seleccionado no es válido.");
    }
}
