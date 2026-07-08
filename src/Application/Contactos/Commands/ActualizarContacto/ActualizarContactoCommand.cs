using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Contactos.Commands.ActualizarContacto;

public sealed record ActualizarContactoCommand(
    int Id, string Nombre, string InstitucionId, string? AreaId, string? UnidadId, string? Cargo, string? Correo, string? Telefono, string? Notas)
    : IRequest<Unit>;

public sealed class ActualizarContactoCommandHandler(
    IContactoRepository repo,
    IInstitucionRepository institucionRepo,
    IUnitOfWork uow)
    : IRequestHandler<ActualizarContactoCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarContactoCommand cmd, CancellationToken ct)
    {
        var c = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Contacto), cmd.Id);

        var inst = await institucionRepo.GetByIdAsync(cmd.InstitucionId, ct)
            ?? throw new NotFoundException(nameof(Institucion), cmd.InstitucionId);

        c.Actualizar(cmd.Nombre, inst.Id, inst.Nombre, cmd.AreaId, cmd.UnidadId, cmd.Cargo, cmd.Correo, cmd.Telefono, cmd.Notas);
        repo.Update(c);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class ActualizarContactoCommandValidator : AbstractValidator<ActualizarContactoCommand>
{
    public ActualizarContactoCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
        RuleFor(x => x.InstitucionId).NotEmpty().WithMessage("Seleccione una institución.");
        RuleFor(x => x.Correo).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Correo))
            .WithMessage("Correo no válido.");
        RuleFor(x => x.Telefono).MaximumLength(40);
    }
}
