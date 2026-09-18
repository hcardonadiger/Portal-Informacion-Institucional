using FluentValidation;

namespace Diger.TramitesEstado.Application.Tickets.Prioridades;

/// <summary>
/// Resuelve qué prioridad guardar cuando quien llama no trae una elegida: el alta desde el chat de
/// soporte, una importación, un comando viejo.
/// </summary>
public static class PrioridadTicketResolver
{
    /// <param name="solicitada">Id elegido por quien llama; null o 0 significa «la que corresponda».</param>
    public static async Task<int> ResolverAsync(
        IApplicationDbContext ctx, int? solicitada, CancellationToken ct)
    {
        if (solicitada is int id and > 0)
        {
            if (!await ctx.PrioridadesTicket.AnyAsync(p => p.Id == id, ct))
                throw new DomainException("La prioridad seleccionada no existe.");
            return id;
        }

        var porDefecto = await ctx.PrioridadesTicket
            .Where(p => p.Activo)
            .OrderByDescending(p => p.EsPredeterminada).ThenBy(p => p.Orden).ThenBy(p => p.Nombre)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);

        return porDefecto
            ?? throw new DomainException(
                "No hay ninguna prioridad activa en el catálogo. Configure al menos una en Catálogos › Prioridades de tickets.");
    }
}

// ── Crear ──────────────────────────────────────────────────────────────────
public sealed record CrearPrioridadTicketCommand(string Nombre, int Orden, ColorEtiqueta Color, bool EsCritica)
    : IRequest<int>;

public sealed class CrearPrioridadTicketCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<CrearPrioridadTicketCommand, int>
{
    public async Task<int> Handle(CrearPrioridadTicketCommand cmd, CancellationToken ct)
    {
        var nombre = cmd.Nombre.Trim();
        if (await ctx.PrioridadesTicket.AnyAsync(p => p.Nombre == nombre, ct))
            throw new DomainException($"Ya existe una prioridad llamada «{nombre}».");

        var prioridad = PrioridadTicket.Crear(nombre, cmd.Orden, cmd.Color, cmd.EsCritica);
        ctx.PrioridadesTicket.Add(prioridad);
        await ctx.SaveChangesAsync(ct);
        return prioridad.Id;
    }
}

// ── Actualizar ─────────────────────────────────────────────────────────────
public sealed record ActualizarPrioridadTicketCommand(
    int Id, string Nombre, int Orden, ColorEtiqueta Color, bool EsCritica, bool Activo) : IRequest<Unit>;

public sealed class ActualizarPrioridadTicketCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<ActualizarPrioridadTicketCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarPrioridadTicketCommand cmd, CancellationToken ct)
    {
        var prioridad = await ctx.PrioridadesTicket.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(PrioridadTicket), cmd.Id);

        var nombre = cmd.Nombre.Trim();
        if (await ctx.PrioridadesTicket.AnyAsync(p => p.Nombre == nombre && p.Id != cmd.Id, ct))
            throw new DomainException($"Ya existe una prioridad llamada «{nombre}».");

        if (!cmd.Activo && prioridad.Activo
            && !await ctx.PrioridadesTicket.AnyAsync(p => p.Activo && p.Id != cmd.Id, ct))
            throw new DomainException("Debe quedar al menos una prioridad activa.");

        prioridad.Actualizar(nombre, cmd.Orden, cmd.Color, cmd.EsCritica, cmd.Activo);
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Marcar como predeterminada ─────────────────────────────────────────────
public sealed record MarcarPrioridadTicketPredeterminadaCommand(int Id) : IRequest<Unit>;

public sealed class MarcarPrioridadTicketPredeterminadaCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<MarcarPrioridadTicketPredeterminadaCommand, Unit>
{
    public async Task<Unit> Handle(MarcarPrioridadTicketPredeterminadaCommand cmd, CancellationToken ct)
    {
        var todas = await ctx.PrioridadesTicket.ToListAsync(ct);
        var elegida = todas.FirstOrDefault(p => p.Id == cmd.Id)
            ?? throw new NotFoundException(nameof(PrioridadTicket), cmd.Id);

        foreach (var p in todas.Where(p => p.EsPredeterminada && p.Id != cmd.Id))
            p.FijarPredeterminada(false);

        elegida.FijarPredeterminada(true);
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Eliminar ───────────────────────────────────────────────────────────────
public sealed record EliminarPrioridadTicketCommand(int Id) : IRequest<Unit>;

public sealed class EliminarPrioridadTicketCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<EliminarPrioridadTicketCommand, Unit>
{
    public async Task<Unit> Handle(EliminarPrioridadTicketCommand cmd, CancellationToken ct)
    {
        var prioridad = await ctx.PrioridadesTicket.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(PrioridadTicket), cmd.Id);

        if (prioridad.EsPredeterminada)
            throw new DomainException(
                "No se puede eliminar la prioridad predeterminada. Marque otra como predeterminada primero.");

        var enUso = await ctx.Tickets.IgnoreQueryFilters().CountAsync(t => t.PrioridadId == cmd.Id, ct);
        if (enUso > 0)
            throw new DomainException(
                $"No se puede eliminar: {enUso} ticket(s) la tienen asignada. Desactívela en su lugar.");

        if (!await ctx.PrioridadesTicket.AnyAsync(p => p.Activo && p.Id != cmd.Id, ct))
            throw new DomainException("Debe quedar al menos una prioridad activa.");

        ctx.PrioridadesTicket.Remove(prioridad);
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Validadores ────────────────────────────────────────────────────────────
public sealed class CrearPrioridadTicketCommandValidator : AbstractValidator<CrearPrioridadTicketCommand>
{
    public CrearPrioridadTicketCommandValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre de la prioridad es obligatorio.")
                              .MaximumLength(40);
        RuleFor(x => x.Orden).InclusiveBetween(0, 999).WithMessage("El orden debe estar entre 0 y 999.");
        RuleFor(x => x.Color).IsInEnum().WithMessage("El color seleccionado no es válido.");
    }
}

public sealed class ActualizarPrioridadTicketCommandValidator : AbstractValidator<ActualizarPrioridadTicketCommand>
{
    public ActualizarPrioridadTicketCommandValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre de la prioridad es obligatorio.")
                              .MaximumLength(40);
        RuleFor(x => x.Orden).InclusiveBetween(0, 999).WithMessage("El orden debe estar entre 0 y 999.");
        RuleFor(x => x.Color).IsInEnum().WithMessage("El color seleccionado no es válido.");
    }
}
