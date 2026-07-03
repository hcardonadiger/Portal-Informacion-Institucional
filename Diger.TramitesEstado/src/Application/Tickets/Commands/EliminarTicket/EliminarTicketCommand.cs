using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Tickets.Commands.EliminarTicket;

public sealed record EliminarTicketCommand(int Id) : IRequest<Unit>;

public sealed class EliminarTicketCommandHandler(ITicketRepository repo, IUnitOfWork uow)
    : IRequestHandler<EliminarTicketCommand, Unit>
{
    public async Task<Unit> Handle(EliminarTicketCommand cmd, CancellationToken ct)
    {
        var t = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Ticket), cmd.Id);
        repo.Delete(t);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
