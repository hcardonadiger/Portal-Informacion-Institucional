using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Reuniones.Commands.EliminarReunion;

public sealed record EliminarReunionCommand(int Id) : IRequest<Unit>;

public sealed class EliminarReunionCommandHandler(IReunionRepository repo, IUnitOfWork uow)
    : IRequestHandler<EliminarReunionCommand, Unit>
{
    public async Task<Unit> Handle(EliminarReunionCommand cmd, CancellationToken ct)
    {
        var r = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Reunion), cmd.Id);

        repo.Delete(r);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
