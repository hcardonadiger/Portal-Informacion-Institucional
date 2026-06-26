using FluentValidation;
using Diger.TramitesEstado.Application.Tickets.Common;

namespace Diger.TramitesEstado.Application.Tickets.Commands.CrearTicket;

public sealed record CrearTicketCommand(TicketFormDto Datos) : IRequest<int>;

public sealed class CrearTicketCommandHandler(
    ITicketRepository repo,
    IInstitucionRepository institucionRepo,
    IExpedienteRepository expedienteRepo,
    ICurrentUserService currentUser,
    IUnitOfWork uow)
    : IRequestHandler<CrearTicketCommand, int>
{
    public async Task<int> Handle(CrearTicketCommand cmd, CancellationToken ct)
    {
        if (!currentUser.PuedeAccederInstitucion(cmd.Datos.InstitucionId))
            throw new DomainException("Debe seleccionar una institución dentro de su alcance asignado.");

        var numero = await GenerarNumeroAsync(ct);
        var t = Ticket.Crear(numero, cmd.Datos.Titulo);
        TicketMapper.Aplicar(t, cmd.Datos);

        if (cmd.Datos.InstitucionId is int instId)
            t.Institucion = (await institucionRepo.GetByIdAsync(instId, ct))?.Nombre;
        if (cmd.Datos.ExpedienteId is int expId)
            t.ExpedienteCodigo = (await expedienteRepo.GetByIdAsync(expId, ct))?.Codigo;

        await repo.AddAsync(t, ct);
        await uow.SaveChangesAsync(ct);
        return t.Id;
    }

    private async Task<string> GenerarNumeroAsync(CancellationToken ct)
    {
        var prefijo = $"TCK-{DateTime.UtcNow.Year}-";
        var seq = await repo.CountByNumeroPrefixAsync(prefijo, ct) + 1;
        string numero;
        do { numero = $"{prefijo}{seq:D4}"; seq++; }
        while (await repo.NumeroExisteAsync(numero, ct));
        return numero;
    }
}

public sealed class CrearTicketCommandValidator : AbstractValidator<CrearTicketCommand>
{
    public CrearTicketCommandValidator()
    {
        RuleFor(x => x.Datos.Titulo).NotEmpty().WithMessage("El título es obligatorio.").MaximumLength(200);
        RuleFor(x => x.Datos.Descripcion).MaximumLength(4000);
    }
}
