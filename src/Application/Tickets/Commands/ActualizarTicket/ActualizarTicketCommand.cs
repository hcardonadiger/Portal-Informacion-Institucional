using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Tickets.Common;

namespace Diger.TramitesEstado.Application.Tickets.Commands.ActualizarTicket;

public sealed record ActualizarTicketCommand(int Id, TicketFormDto Datos) : IRequest<Unit>;

public sealed class ActualizarTicketCommandHandler(
    ITicketRepository repo,
    IInstitucionRepository institucionRepo,
    IExpedienteRepository expedienteRepo,
    IUnitOfWork uow)
    : IRequestHandler<ActualizarTicketCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarTicketCommand cmd, CancellationToken ct)
    {
        var t = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Ticket), cmd.Id);

        TicketMapper.Aplicar(t, cmd.Datos);

        t.Institucion = cmd.Datos.InstitucionId is int instId
            ? (await institucionRepo.GetByIdAsync(instId, ct))?.Nombre : null;
        t.ExpedienteCodigo = cmd.Datos.ExpedienteId is int expId
            ? (await expedienteRepo.GetByIdAsync(expId, ct))?.Codigo : null;

        t.MarcarActualizado();
        repo.Update(t);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class ActualizarTicketCommandValidator : AbstractValidator<ActualizarTicketCommand>
{
    public ActualizarTicketCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Datos.Titulo).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Datos.Descripcion).MaximumLength(4000);
    }
}
