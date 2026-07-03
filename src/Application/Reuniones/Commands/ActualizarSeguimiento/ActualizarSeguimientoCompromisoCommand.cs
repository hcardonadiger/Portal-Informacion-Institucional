using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Reuniones.Commands.ActualizarSeguimiento;

/// <summary>
/// Actualiza el seguimiento de un compromiso (acuerdo) sin pasar por el editor completo de la reunión.
/// Usado por la página consolidada de "Seguimiento de compromisos".
/// </summary>
public sealed record ActualizarSeguimientoCompromisoCommand(
    int AcuerdoId,
    EstadoCompromiso Estado,
    DateOnly? FechaCumplimiento = null,
    string? Nota = null) : IRequest<Unit>;

public sealed class ActualizarSeguimientoCompromisoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<ActualizarSeguimientoCompromisoCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarSeguimientoCompromisoCommand cmd, CancellationToken ct)
    {
        var acuerdo = await ctx.Acuerdos.FirstOrDefaultAsync(a => a.Id == cmd.AcuerdoId, ct)
            ?? throw new NotFoundException(nameof(AcuerdoReunion), cmd.AcuerdoId);

        // Alcance: la reunión debe ser visible para el usuario (el filtro global se aplica a Reuniones).
        var reunionVisible = await ctx.Reuniones.AnyAsync(r => r.Id == acuerdo.ReunionId, ct);
        if (!reunionVisible)
            throw new NotFoundException(nameof(AcuerdoReunion), cmd.AcuerdoId);

        var actor = currentUser.Nombre ?? currentUser.Correo;
        acuerdo.ActualizarSeguimiento(cmd.Estado, cmd.FechaCumplimiento, cmd.Nota, actor);

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class ActualizarSeguimientoCompromisoCommandValidator
    : AbstractValidator<ActualizarSeguimientoCompromisoCommand>
{
    public ActualizarSeguimientoCompromisoCommandValidator()
    {
        RuleFor(x => x.AcuerdoId).GreaterThan(0);
        RuleFor(x => x.Estado).IsInEnum();
        RuleFor(x => x.Nota).MaximumLength(1000);
    }
}
