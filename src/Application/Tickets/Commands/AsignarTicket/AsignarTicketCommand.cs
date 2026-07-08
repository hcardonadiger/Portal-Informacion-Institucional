using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Tickets.Commands.AsignarTicket;

public sealed record AsignarTicketCommand(int Id, Guid? UsuarioId) : IRequest<Unit>;

public sealed class AsignarTicketCommandHandler(
    ITicketRepository repo, IUsuarioRepository usuarioRepo, ICurrentUserService currentUser, IUnitOfWork uow)
    : IRequestHandler<AsignarTicketCommand, Unit>
{
    public async Task<Unit> Handle(AsignarTicketCommand cmd, CancellationToken ct)
    {
        var t = await repo.GetByIdWithDetailsAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Ticket), cmd.Id);

        string? nombre = null;
        if (cmd.UsuarioId is Guid uid)
            nombre = (await usuarioRepo.GetByIdAsync(uid, ct))?.Nombre;

        var autor = currentUser.Nombre ?? currentUser.Correo ?? "Sistema";
        t.Asignar(cmd.UsuarioId, nombre, autor);

        repo.Update(t);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
