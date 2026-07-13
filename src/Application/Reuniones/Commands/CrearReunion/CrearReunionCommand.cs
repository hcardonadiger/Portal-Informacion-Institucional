using FluentValidation;
using Diger.TramitesEstado.Application.Reuniones.Common;

namespace Diger.TramitesEstado.Application.Reuniones.Commands.CrearReunion;

public sealed record CrearReunionCommand(
    ReunionFormDto Datos, List<AsistenteInput> Asistentes, List<AcuerdoInput> Acuerdos) : IRequest<int>;

public sealed class CrearReunionCommandHandler(
    IReunionRepository repo,
    IInstitucionRepository institucionRepo,
    IContactoRepository contactoRepo,
    ICurrentUserService currentUser,
    IApplicationDbContext ctx,
    IUnitOfWork uow)
    : IRequestHandler<CrearReunionCommand, int>
{
    public async Task<int> Handle(CrearReunionCommand cmd, CancellationToken ct)
    {
        if (!currentUser.EsGlobal)
        {
            if (cmd.Datos.InstitucionesIds.Count == 0)
                throw new DomainException("Debe seleccionar al menos una institución dentro de su alcance asignado.");
            if (cmd.Datos.InstitucionesIds.Any(id => !currentUser.PuedeAccederInstitucion(id)))
                throw new DomainException("Solo puede seleccionar instituciones dentro de su alcance asignado.");
        }

        // "Es capacitación de plataforma" ya no es un checkbox manual: se deriva del Tipo elegido.
        cmd.Datos.EsCapacitacionPlataforma = cmd.Datos.Tipo == "Capacitación";

        var r = Reunion.Crear(cmd.Datos.Titulo);
        ReunionMapper.Aplicar(r, cmd.Datos, cmd.Asistentes, cmd.Acuerdos);
        r.CreadoPorId = currentUser.UserId;   // dueño (relevante para reuniones privadas)

        if (!string.IsNullOrWhiteSpace(cmd.Datos.InstitucionId))
        {
            var inst = await institucionRepo.GetByIdAsync(cmd.Datos.InstitucionId, ct);
            r.InstitucionId = inst?.Id;
            r.Institucion   = inst?.Nombre;
        }

        await repo.AddAsync(r, ct);
        await ContactoFeeder.FeedAsync(r, contactoRepo, institucionRepo, ctx, uow, ct);
        await uow.SaveChangesAsync(ct);
        return r.Id;
    }
}

public sealed class CrearReunionCommandValidator : AbstractValidator<CrearReunionCommand>
{
    public CrearReunionCommandValidator()
    {
        RuleFor(x => x.Datos.Titulo).NotEmpty().MaximumLength(250);
    }
}
