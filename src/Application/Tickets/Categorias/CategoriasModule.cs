using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Tickets.Common;

namespace Diger.TramitesEstado.Application.Tickets.Categorias;

/// <summary>Fila del catálogo de categorías (nivel superior) para administración.</summary>
public sealed record CategoriaAdminDto(int Id, string Nombre, bool Activo, int Temas);

// ── Queries ─────────────────────────────────────────────────────────────────
public sealed record GetCategoriasQuery(bool SoloActivas = false) : IRequest<IReadOnlyList<CategoriaAdminDto>>;

public sealed class GetCategoriasQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetCategoriasQuery, IReadOnlyList<CategoriaAdminDto>>
{
    public async Task<IReadOnlyList<CategoriaAdminDto>> Handle(GetCategoriasQuery q, CancellationToken ct) =>
        await ctx.CategoriasTicket
            .Where(c => !q.SoloActivas || c.Activo)
            .OrderByDescending(c => c.Activo).ThenBy(c => c.Nombre)
            .Select(c => new CategoriaAdminDto(c.Id, c.Nombre, c.Activo,
                ctx.TemasTicket.Count(t => t.CategoriaId == c.Id)))
            .ToListAsync(ct);
}

public sealed record GetCategoriasActivasQuery : IRequest<IReadOnlyList<CategoriaOpcionDto>>, ICacheableQuery
{
    public string CacheKey => "categorias-activas";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(60);
}

public sealed class GetCategoriasActivasQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetCategoriasActivasQuery, IReadOnlyList<CategoriaOpcionDto>>
{
    public async Task<IReadOnlyList<CategoriaOpcionDto>> Handle(GetCategoriasActivasQuery q, CancellationToken ct) =>
        await ctx.CategoriasTicket
            .Where(c => c.Activo)
            .OrderBy(c => c.Nombre)
            .Select(c => new CategoriaOpcionDto(c.Id, c.Nombre))
            .ToListAsync(ct);
}

// ── Commands ────────────────────────────────────────────────────────────────
public sealed record CrearCategoriaCommand(string Nombre) : IRequest<int>;

public sealed class CrearCategoriaCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<CrearCategoriaCommand, int>
{
    public async Task<int> Handle(CrearCategoriaCommand cmd, CancellationToken ct)
    {
        var nombre = cmd.Nombre.Trim();
        if (await ctx.CategoriasTicket.AnyAsync(c => c.Nombre == nombre, ct))
            throw new DomainException($"Ya existe una categoría llamada «{nombre}».");

        var cat = CategoriaTicket.Crear(nombre);
        ctx.CategoriasTicket.Add(cat);
        await ctx.SaveChangesAsync(ct);
        return cat.Id;
    }
}

public sealed record ActualizarCategoriaCommand(int Id, string Nombre, bool Activo) : IRequest<Unit>;

public sealed class ActualizarCategoriaCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<ActualizarCategoriaCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarCategoriaCommand cmd, CancellationToken ct)
    {
        var cat = await ctx.CategoriasTicket.FirstOrDefaultAsync(c => c.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(CategoriaTicket), cmd.Id);

        var nombre = cmd.Nombre.Trim();
        if (await ctx.CategoriasTicket.AnyAsync(c => c.Nombre == nombre && c.Id != cmd.Id, ct))
            throw new DomainException($"Ya existe una categoría llamada «{nombre}».");

        cat.Actualizar(nombre, cmd.Activo);
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

/// <summary>Elimina la categoría; los temas que la usaban quedan sin categoría (no se borran).</summary>
public sealed record EliminarCategoriaCommand(int Id) : IRequest<Unit>;

public sealed class EliminarCategoriaCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<EliminarCategoriaCommand, Unit>
{
    public async Task<Unit> Handle(EliminarCategoriaCommand cmd, CancellationToken ct)
    {
        var cat = await ctx.CategoriasTicket.FirstOrDefaultAsync(c => c.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(CategoriaTicket), cmd.Id);

        // Desvincula los temas (categoría es opcional) antes de eliminar.
        var temas = await ctx.TemasTicket.Where(t => t.CategoriaId == cmd.Id).ToListAsync(ct);
        foreach (var t in temas)
            t.Actualizar(t.Nombre, t.HorasResolucion, t.Activo, categoriaId: null);

        ctx.CategoriasTicket.Remove(cat);
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class CrearCategoriaCommandValidator : AbstractValidator<CrearCategoriaCommand>
{
    public CrearCategoriaCommandValidator() =>
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre de la categoría es obligatorio.").MaximumLength(80);
}

public sealed class ActualizarCategoriaCommandValidator : AbstractValidator<ActualizarCategoriaCommand>
{
    public ActualizarCategoriaCommandValidator() =>
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre de la categoría es obligatorio.").MaximumLength(80);
}
