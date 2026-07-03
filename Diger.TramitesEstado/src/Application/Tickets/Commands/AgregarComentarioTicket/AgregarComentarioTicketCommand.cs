using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Tickets.Common;

namespace Diger.TramitesEstado.Application.Tickets.Commands.AgregarComentarioTicket;

public sealed record AgregarComentarioTicketCommand(int Id, string Texto, IReadOnlyList<AdjuntoInput>? Adjuntos = null)
    : IRequest<Unit>;

public sealed class AgregarComentarioTicketCommandHandler(
    ITicketRepository repo, ICurrentUserService currentUser, IUnitOfWork uow)
    : IRequestHandler<AgregarComentarioTicketCommand, Unit>
{
    public async Task<Unit> Handle(AgregarComentarioTicketCommand cmd, CancellationToken ct)
    {
        var t = await repo.GetByIdWithDetailsAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Ticket), cmd.Id);

        var autor = currentUser.Nombre ?? currentUser.Correo ?? "Sistema";
        var comentario = t.AgregarComentario(autor, cmd.Texto);

        repo.Update(t);
        await uow.SaveChangesAsync(ct);   // asigna el Id del comentario

        // Adjuntos de la respuesta: se enlazan al comentario recién creado.
        if (cmd.Adjuntos is { Count: > 0 })
        {
            foreach (var a in cmd.Adjuntos)
                t.AgregarAdjunto(a.Nombre, a.Url, a.Tamano, comentario.Id);
            await uow.SaveChangesAsync(ct);
        }

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
