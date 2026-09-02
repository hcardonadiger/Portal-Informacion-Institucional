using FluentValidation;

namespace Diger.TramitesEstado.Application.Reuniones.Commands.DesenlazarReunion;

/// <summary>
/// Saca una reunión de su hilo. Si tras salir el hilo queda con una sola reunión,
/// también se le quita el hilo (un hilo de un solo miembro no tiene sentido).
/// </summary>
public sealed record DesenlazarReunionCommand(int ReunionId) : IRequest<Unit>;

public sealed class DesenlazarReunionCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<DesenlazarReunionCommand, Unit>
{
    public async Task<Unit> Handle(DesenlazarReunionCommand cmd, CancellationToken ct)
    {
        var r = await ctx.Reuniones.FirstOrDefaultAsync(x => x.Id == cmd.ReunionId, ct)
            ?? throw new NotFoundException(nameof(Reunion), cmd.ReunionId);

        if (r.HiloId is null) return Unit.Value;

        var hiloId = r.HiloId.Value;
        r.SalirDelHilo();

        // Miembros que quedan en el hilo (excluyendo la reunión que acaba de salir).
        var restantes = await ctx.Reuniones
            .Where(x => x.HiloId == hiloId && x.Id != r.Id)
            .ToListAsync(ct);
        if (restantes.Count == 1)
            restantes[0].SalirDelHilo();

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class DesenlazarReunionCommandValidator : AbstractValidator<DesenlazarReunionCommand>
{
    public DesenlazarReunionCommandValidator()
    {
        RuleFor(x => x.ReunionId).GreaterThan(0);
    }
}
