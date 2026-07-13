using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Tickets.Temas;

// ── Crear tema ─────────────────────────────────────────────────────────────
public sealed record CrearTemaCommand(string Nombre, int HorasResolucion, int? CategoriaId) : IRequest<int>;

public sealed class CrearTemaCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<CrearTemaCommand, int>
{
    public async Task<int> Handle(CrearTemaCommand cmd, CancellationToken ct)
    {
        var nombre = cmd.Nombre.Trim();
        if (await ctx.TemasTicket.AnyAsync(t => t.Nombre == nombre, ct))
            throw new DomainException($"Ya existe un tema llamado «{nombre}».");

        var categoriaId = await ResolverCategoriaAsync(ctx, cmd.CategoriaId, ct);
        var tema = TemaTicket.Crear(nombre, cmd.HorasResolucion, categoriaId);
        ctx.TemasTicket.Add(tema);
        await ctx.SaveChangesAsync(ct);
        return tema.Id;
    }

    internal static async Task<int?> ResolverCategoriaAsync(IApplicationDbContext ctx, int? categoriaId, CancellationToken ct)
    {
        if (categoriaId is not int id) return null;
        if (!await ctx.CategoriasTicket.AnyAsync(c => c.Id == id, ct))
            throw new DomainException("La categoría seleccionada no existe.");
        return id;
    }
}

// ── Actualizar tema (nombre, SLA, activo, categoría) ────────────────────────
public sealed record ActualizarTemaCommand(int Id, string Nombre, int HorasResolucion, bool Activo, int? CategoriaId) : IRequest<Unit>;

public sealed class ActualizarTemaCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<ActualizarTemaCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarTemaCommand cmd, CancellationToken ct)
    {
        var tema = await ctx.TemasTicket.FirstOrDefaultAsync(t => t.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(TemaTicket), cmd.Id);

        var nombre = cmd.Nombre.Trim();
        if (await ctx.TemasTicket.AnyAsync(t => t.Nombre == nombre && t.Id != cmd.Id, ct))
            throw new DomainException($"Ya existe un tema llamado «{nombre}».");

        var categoriaId = await CrearTemaCommandHandler.ResolverCategoriaAsync(ctx, cmd.CategoriaId, ct);
        tema.Actualizar(nombre, cmd.HorasResolucion, cmd.Activo, categoriaId);
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Eliminar tema (solo si no está en uso; si no, desactivar) ───────────────
public sealed record EliminarTemaCommand(int Id) : IRequest<Unit>;

public sealed class EliminarTemaCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<EliminarTemaCommand, Unit>
{
    public async Task<Unit> Handle(EliminarTemaCommand cmd, CancellationToken ct)
    {
        var tema = await ctx.TemasTicket.FirstOrDefaultAsync(t => t.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(TemaTicket), cmd.Id);

        if (await ctx.Tickets.IgnoreQueryFilters().AnyAsync(t => t.TemaId == cmd.Id, ct))
            throw new DomainException("No se puede eliminar un tema con tickets asociados. Desactívalo en su lugar.");

        // Quita la especialidad de los usuarios que lo atendían.
        var asignaciones = await ctx.UsuarioTemas.Where(u => u.TemaId == cmd.Id).ToListAsync(ct);
        ctx.UsuarioTemas.RemoveRange(asignaciones);
        ctx.TemasTicket.Remove(tema);
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class CrearTemaCommandValidator : AbstractValidator<CrearTemaCommand>
{
    public CrearTemaCommandValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre del tema es obligatorio.").MaximumLength(80);
        RuleFor(x => x.HorasResolucion).InclusiveBetween(0, 100000).WithMessage("El tiempo máximo en horas no es válido.");
    }
}

public sealed class ActualizarTemaCommandValidator : AbstractValidator<ActualizarTemaCommand>
{
    public ActualizarTemaCommandValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre del tema es obligatorio.").MaximumLength(80);
        RuleFor(x => x.HorasResolucion).InclusiveBetween(0, 100000).WithMessage("El tiempo máximo en horas no es válido.");
    }
}
