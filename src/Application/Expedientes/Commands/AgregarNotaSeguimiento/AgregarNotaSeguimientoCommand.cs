using Diger.TramitesEstado.Application.Common.Exceptions;
using FluentValidation;

namespace Diger.TramitesEstado.Application.Expedientes.Commands.AgregarNotaSeguimiento;

public sealed record AgregarNotaSeguimientoCommand(int ExpedienteId, string Texto) : IRequest<int>;

public sealed class AgregarNotaSeguimientoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<AgregarNotaSeguimientoCommand, int>
{
    public async Task<int> Handle(AgregarNotaSeguimientoCommand cmd, CancellationToken ct)
    {
        // ctx.Expedientes está filtrado por alcance: si el expediente no es visible
        // para el usuario, la nota no se puede crear.
        var existe = await ctx.Expedientes
            .AsNoTracking()
            .AnyAsync(e => e.Id == cmd.ExpedienteId, ct);
        if (!existe) throw new NotFoundException(nameof(Expediente), cmd.ExpedienteId);

        var nota = NotaSeguimientoExpediente.Crear(cmd.ExpedienteId, cmd.Texto, currentUser.Nombre ?? "—");

        ctx.NotasSeguimiento.Add(nota);
        await ctx.SaveChangesAsync(ct);
        return nota.Id;
    }
}

public sealed class AgregarNotaSeguimientoCommandValidator : AbstractValidator<AgregarNotaSeguimientoCommand>
{
    public AgregarNotaSeguimientoCommandValidator()
    {
        RuleFor(x => x.ExpedienteId).GreaterThan(0);
        RuleFor(x => x.Texto).NotEmpty().MaximumLength(NotaSeguimientoExpediente.MaxTexto);
    }
}
