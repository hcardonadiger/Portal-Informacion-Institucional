using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Reuniones.Common;

namespace Diger.TramitesEstado.Application.Reuniones.Commands.ActualizarReunion;

public sealed record ActualizarReunionCommand(
    int Id, ReunionFormDto Datos, List<AsistenteInput> Asistentes, List<AcuerdoInput> Acuerdos) : IRequest<Unit>;

public sealed class ActualizarReunionCommandHandler(
    IReunionRepository repo,
    IInstitucionRepository institucionRepo,
    IContactoRepository contactoRepo,
    ICurrentUserService currentUser,
    IApplicationDbContext ctx,
    IUnitOfWork uow)
    : IRequestHandler<ActualizarReunionCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarReunionCommand cmd, CancellationToken ct)
    {
        var r = await repo.GetByIdWithDetailsAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Reunion), cmd.Id);

        // "Es capacitación de plataforma" ya no es un checkbox manual: se deriva del Tipo elegido.
        cmd.Datos.EsCapacitacionPlataforma = cmd.Datos.Tipo == "Capacitación";

        ReunionMapper.Aplicar(r, cmd.Datos, cmd.Asistentes, cmd.Acuerdos);

        // Al volver privada una reunión sin dueño registrado, el editor pasa a ser el dueño
        // (de lo contrario nadie podría volver a verla).
        if (r.Visibilidad == VisibilidadReunion.Privada && r.CreadoPorId is null)
            r.CreadoPorId = currentUser.UserId;

        // Institución principal = la primera convocada (alcance, acta, tablero).
        var principalId = r.InstitucionesParticipantes.OrderBy(x => x.Orden).Select(x => (int?)x.InstitucionId).FirstOrDefault();
        if (principalId is int id)
        {
            var inst = await institucionRepo.GetByIdAsync(id, ct);
            r.InstitucionId = inst?.Id;
            r.Institucion   = inst?.Nombre;
        }
        else { r.InstitucionId = null; r.Institucion = null; }

        r.MarcarActualizada();
        repo.Update(r);
        await ContactoFeeder.FeedAsync(r, contactoRepo, institucionRepo, ctx, uow, ct);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class ActualizarReunionCommandValidator : AbstractValidator<ActualizarReunionCommand>
{
    public ActualizarReunionCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Datos.Titulo).NotEmpty().MaximumLength(250);
    }
}
