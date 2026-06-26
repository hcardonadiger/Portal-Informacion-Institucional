using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Expedientes.Common;

namespace Diger.TramitesEstado.Application.Expedientes.Commands.ActualizarExpediente;

public sealed record ActualizarExpedienteCommand(int Id, ExpedienteInputDto Datos) : IRequest<Unit>;

public sealed class ActualizarExpedienteCommandHandler(
    IExpedienteRepository repo,
    IUnitOfWork uow)
    : IRequestHandler<ActualizarExpedienteCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarExpedienteCommand cmd, CancellationToken ct)
    {
        var exp = await repo.GetByIdWithDetailsAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Expediente), cmd.Id);

        ExpedienteMapper.Aplicar(exp, cmd.Datos);
        exp.MarcarActualizado();

        repo.Update(exp);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class ActualizarExpedienteCommandValidator : AbstractValidator<ActualizarExpedienteCommand>
{
    public ActualizarExpedienteCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Datos.Analista).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Datos.Tramites)
            .Must(t => t != null && t.Any(x => !string.IsNullOrWhiteSpace(x.NombreTramite)))
            .WithMessage("Registre al menos un trámite.");
    }
}
