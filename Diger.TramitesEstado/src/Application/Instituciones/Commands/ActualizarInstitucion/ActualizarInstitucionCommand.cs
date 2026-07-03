using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Instituciones.Commands.CrearInstitucion;

namespace Diger.TramitesEstado.Application.Instituciones.Commands.ActualizarInstitucion;

public sealed record ActualizarInstitucionCommand(int Id, string Nombre, bool Activo, List<string> Tramites)
    : IRequest<Unit>;

public sealed class ActualizarInstitucionCommandHandler(
    IInstitucionRepository repo,
    IUnitOfWork uow)
    : IRequestHandler<ActualizarInstitucionCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarInstitucionCommand cmd, CancellationToken ct)
    {
        var inst = await repo.GetByIdWithTramitesAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Institucion), cmd.Id);

        if (await repo.ExisteNombreAsync(cmd.Nombre, cmd.Id, ct))
            throw new DomainException($"Ya existe otra institución con el nombre '{cmd.Nombre.Trim().ToUpper()}'.");

        inst.Renombrar(cmd.Nombre);
        if (cmd.Activo) inst.Activar(); else inst.Desactivar();
        CrearInstitucionCommandHandler.AsignarTramites(inst, cmd.Tramites);

        repo.Update(inst);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class ActualizarInstitucionCommandValidator : AbstractValidator<ActualizarInstitucionCommand>
{
    public ActualizarInstitucionCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(120);
        RuleForEach(x => x.Tramites).MaximumLength(400);
    }
}
