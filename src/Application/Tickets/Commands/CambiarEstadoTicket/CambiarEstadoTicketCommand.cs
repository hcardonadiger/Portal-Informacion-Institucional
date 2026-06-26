using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Tickets.Commands.CambiarEstadoTicket;

public sealed record CambiarEstadoTicketCommand(int Id, EstadoTicket Estado, string? Nota) : IRequest<Unit>;

public sealed class CambiarEstadoTicketCommandHandler(
    ITicketRepository repo, ICurrentUserService currentUser, IUnitOfWork uow)
    : IRequestHandler<CambiarEstadoTicketCommand, Unit>
{
    public async Task<Unit> Handle(CambiarEstadoTicketCommand cmd, CancellationToken ct)
    {
        var t = await repo.GetByIdWithDetailsAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Ticket), cmd.Id);

        var autor = currentUser.Nombre ?? currentUser.Correo ?? "Sistema";
        t.CambiarEstado(cmd.Estado, autor, cmd.Nota);

        repo.Update(t);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
