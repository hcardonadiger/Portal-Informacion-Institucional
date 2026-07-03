using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Expedientes.Commands.EliminarExpediente;

public sealed record EliminarExpedienteCommand(int Id) : IRequest<Unit>;

public sealed class EliminarExpedienteCommandHandler(
    IExpedienteRepository repo,
    IUnitOfWork uow)
    : IRequestHandler<EliminarExpedienteCommand, Unit>
{
    public async Task<Unit> Handle(EliminarExpedienteCommand cmd, CancellationToken ct)
    {
        var exp = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Expediente), cmd.Id);

        repo.Delete(exp);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
