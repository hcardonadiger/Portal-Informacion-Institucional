using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Tickets.Commands.AgregarComentarioTicket;

public sealed record AgregarComentarioTicketCommand(int Id, string Texto) : IRequest<Unit>;

public sealed class AgregarComentarioTicketCommandHandler(
    ITicketRepository repo, ICurrentUserService currentUser, IUnitOfWork uow)
    : IRequestHandler<AgregarComentarioTicketCommand, Unit>
{
    public async Task<Unit> Handle(AgregarComentarioTicketCommand cmd, CancellationToken ct)
    {
        var t = await repo.GetByIdWithDetailsAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Ticket), cmd.Id);

        var autor = currentUser.Nombre ?? currentUser.Correo ?? "Sistema";
        t.AgregarComentario(autor, cmd.Texto);

        repo.Update(t);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class AgregarComentarioTicketCommandValidator : AbstractValidator<AgregarComentarioTicketCommand>
{
    public AgregarComentarioTicketCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Texto).NotEmpty().WithMessage("El comentario no puede estar vacío.").MaximumLength(2000);
    }
}
