using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Tickets.Common;

namespace Diger.TramitesEstado.Application.Tickets.Commands.ActualizarTicket;

public sealed record ActualizarTicketCommand(int Id, TicketFormDto Datos) : IRequest<Unit>;

public sealed class ActualizarTicketCommandHandler(
    ITicketRepository repo,
    IInstitucionRepository institucionRepo,
    IExpedienteRepository expedienteRepo,
    ICurrentUserService currentUser,
    IUnitOfWork uow)
    : IRequestHandler<ActualizarTicketCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarTicketCommand cmd, CancellationToken ct)
    {
        var t = await repo.GetByIdWithDetailsAsync(cmd.Id, ct)   // incluye trámites (se reemplazan en bloque)
            ?? throw new NotFoundException(nameof(Ticket), cmd.Id);

        var d = cmd.Datos;
        // Coherencia institución-expediente: el expediente (si hay) define la institución.
        var exp = d.ExpedienteId is int expId ? await expedienteRepo.GetByIdAsync(expId, ct) : null;
        var institucionId = exp?.InstitucionId ?? d.InstitucionId;

        if (!currentUser.PuedeAccederInstitucion(institucionId))
            throw new DomainException("Debe seleccionar una institución dentro de su alcance asignado.");

        TicketMapper.Aplicar(t, d);

        t.InstitucionId    = institucionId;
        t.Institucion      = exp?.Institucion
            ?? (!string.IsNullOrWhiteSpace(institucionId) ? (await institucionRepo.GetByIdAsync(institucionId, ct))?.Nombre : null);
        t.ExpedienteId     = exp?.Id;
        t.ExpedienteCodigo = exp?.Codigo;

        // Trámites afectados: solo los del catálogo de la institución del ticket (integridad).
        if (!string.IsNullOrWhiteSpace(institucionId) && d.TramiteIds.Count > 0)
        {
            var catalogo = await institucionRepo.GetTramitesAsync(institucionId, ct);
            t.EstablecerTramites(catalogo
                .Where(td => d.TramiteIds.Contains(td.Id))
                .Select(td => ((int?)td.Id, td.Nombre)));
        }
        else t.EstablecerTramites([]);

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
