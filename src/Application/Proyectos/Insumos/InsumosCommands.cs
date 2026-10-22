using FluentValidation;

namespace Diger.TramitesEstado.Application.Proyectos.Insumos;

// El catálogo de insumos, solo para administradores (la clave la exigen las páginas). No hay
// comandos de borrado a propósito: un insumo o una categoría se desactivan, y lo que las
// actividades ya tienen asignado se conserva.

internal static class CatalogoInsumos
{
    /// <summary>
    /// La categoría a la que se lleva un insumo tiene que existir y estar activa. Se exige solo
    /// cuando la categoría cambia: renombrar o desactivar un insumo de una categoría ya retirada
    /// tiene que seguir siendo posible.
    /// </summary>
    public static async Task<CategoriaInsumo> CategoriaActivaAsync(
        IApplicationDbContext ctx, int categoriaId, CancellationToken ct)
    {
        var categoria = await ctx.CategoriasInsumo.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoriaId, ct)
            ?? throw new DomainException("La categoría seleccionada no existe.");
        if (!categoria.Activo)
            throw new DomainException(
                $"La categoría «{categoria.Nombre}» está desactivada. Actívela o elija otra.");
        return categoria;
    }

    public static async Task ExigirNombreLibreAsync(
        IApplicationDbContext ctx, int categoriaId, string categoria, string nombre, int? salvoId, CancellationToken ct)
    {
        // En minúsculas porque la regla tiene que ser la misma que la del índice único, y la
        // intercalación de SQL Server no distingue «Cinta» de «cinta».
        var buscado = nombre.ToLower();
        if (await ctx.Insumos.AnyAsync(i => i.CategoriaId == categoriaId && i.Nombre.ToLower() == buscado
                                         && i.Id != salvoId, ct))
            throw new DomainException($"Ya existe un insumo llamado «{nombre}» en «{categoria}».");
    }
}

// ── Categorías ─────────────────────────────────────────────────────────────
public sealed record CrearCategoriaInsumoCommand(string Nombre, int Orden) : IRequest<int>;

public sealed class CrearCategoriaInsumoCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<CrearCategoriaInsumoCommand, int>
{
    public async Task<int> Handle(CrearCategoriaInsumoCommand cmd, CancellationToken ct)
    {
        var nombre = (cmd.Nombre ?? "").Trim();
        var buscado = nombre.ToLower();
        if (await ctx.CategoriasInsumo.AnyAsync(c => c.Nombre.ToLower() == buscado, ct))
            throw new DomainException($"Ya existe una categoría llamada «{nombre}».");

        var categoria = CategoriaInsumo.Crear(nombre, cmd.Orden);
        ctx.CategoriasInsumo.Add(categoria);
        await ctx.SaveChangesAsync(ct);
        return categoria.Id;
    }
}

public sealed record ActualizarCategoriaInsumoCommand(int Id, string Nombre, int Orden, bool Activo)
    : IRequest<Unit>;

public sealed class ActualizarCategoriaInsumoCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<ActualizarCategoriaInsumoCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarCategoriaInsumoCommand cmd, CancellationToken ct)
    {
        var categoria = await ctx.CategoriasInsumo.FirstOrDefaultAsync(c => c.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(CategoriaInsumo), cmd.Id);

        var nombre = (cmd.Nombre ?? "").Trim();
        var buscado = nombre.ToLower();
        if (await ctx.CategoriasInsumo.AnyAsync(c => c.Nombre.ToLower() == buscado && c.Id != cmd.Id, ct))
            throw new DomainException($"Ya existe una categoría llamada «{nombre}».");

        // Desactivarla deja de ofrecer sus insumos; no se los quita a las actividades que ya los
        // tienen. Por eso no se exige que quede alguna activa.
        categoria.Actualizar(nombre, cmd.Orden, cmd.Activo);
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Insumos ────────────────────────────────────────────────────────────────
public sealed record CrearInsumoCommand(int CategoriaId, string Nombre, string? Descripcion) : IRequest<int>;

public sealed class CrearInsumoCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<CrearInsumoCommand, int>
{
    public async Task<int> Handle(CrearInsumoCommand cmd, CancellationToken ct)
    {
        var categoria = await CatalogoInsumos.CategoriaActivaAsync(ctx, cmd.CategoriaId, ct);
        var nombre    = (cmd.Nombre ?? "").Trim();
        await CatalogoInsumos.ExigirNombreLibreAsync(ctx, categoria.Id, categoria.Nombre, nombre, null, ct);

        var insumo = Insumo.Crear(categoria.Id, nombre, cmd.Descripcion);
        ctx.Insumos.Add(insumo);
        await ctx.SaveChangesAsync(ct);
        return insumo.Id;
    }
}

public sealed record ActualizarInsumoCommand(
    int Id, int CategoriaId, string Nombre, string? Descripcion, bool Activo) : IRequest<Unit>;

public sealed class ActualizarInsumoCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<ActualizarInsumoCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarInsumoCommand cmd, CancellationToken ct)
    {
        var insumo = await ctx.Insumos.Include(i => i.Categoria).FirstOrDefaultAsync(i => i.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Insumo), cmd.Id);

        var categoria = cmd.CategoriaId == insumo.CategoriaId
            ? insumo.Categoria!
            : await CatalogoInsumos.CategoriaActivaAsync(ctx, cmd.CategoriaId, ct);

        var nombre = (cmd.Nombre ?? "").Trim();
        await CatalogoInsumos.ExigirNombreLibreAsync(ctx, categoria.Id, categoria.Nombre, nombre, insumo.Id, ct);

        insumo.Actualizar(categoria.Id, nombre, cmd.Descripcion, cmd.Activo);
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Validadores ────────────────────────────────────────────────────────────
public sealed class CrearCategoriaInsumoCommandValidator : AbstractValidator<CrearCategoriaInsumoCommand>
{
    public CrearCategoriaInsumoCommandValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre de la categoría es obligatorio.")
                              .MaximumLength(CategoriaInsumo.MaxNombre);
        RuleFor(x => x.Orden).InclusiveBetween(0, 999).WithMessage("El orden debe estar entre 0 y 999.");
    }
}

public sealed class ActualizarCategoriaInsumoCommandValidator : AbstractValidator<ActualizarCategoriaInsumoCommand>
{
    public ActualizarCategoriaInsumoCommandValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre de la categoría es obligatorio.")
                              .MaximumLength(CategoriaInsumo.MaxNombre);
        RuleFor(x => x.Orden).InclusiveBetween(0, 999).WithMessage("El orden debe estar entre 0 y 999.");
    }
}

public sealed class CrearInsumoCommandValidator : AbstractValidator<CrearInsumoCommand>
{
    public CrearInsumoCommandValidator()
    {
        RuleFor(x => x.CategoriaId).GreaterThan(0).WithMessage("Elija la categoría del insumo.");
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre del insumo es obligatorio.")
                              .MaximumLength(Insumo.MaxNombre);
        RuleFor(x => x.Descripcion).MaximumLength(Insumo.MaxDescripcion);
    }
}

public sealed class ActualizarInsumoCommandValidator : AbstractValidator<ActualizarInsumoCommand>
{
    public ActualizarInsumoCommandValidator()
    {
        RuleFor(x => x.CategoriaId).GreaterThan(0).WithMessage("Elija la categoría del insumo.");
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre del insumo es obligatorio.")
                              .MaximumLength(Insumo.MaxNombre);
        RuleFor(x => x.Descripcion).MaximumLength(Insumo.MaxDescripcion);
    }
}
