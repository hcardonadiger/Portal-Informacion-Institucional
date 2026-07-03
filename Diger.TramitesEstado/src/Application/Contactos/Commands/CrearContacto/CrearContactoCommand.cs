using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Contactos.Commands.CrearContacto;

public sealed record CrearContactoCommand(
    string Nombre, int InstitucionId, string? Cargo, string? Correo, string? Telefono, string? Notas)
    : IRequest<int>;

public sealed class CrearContactoCommandHandler(
    IContactoRepository repo,
    IInstitucionRepository institucionRepo,
    ICurrentUserService currentUser,
    IUnitOfWork uow)
    : IRequestHandler<CrearContactoCommand, int>
{
    public async Task<int> Handle(CrearContactoCommand cmd, CancellationToken ct)
    {
        if (!currentUser.PuedeAccederInstitucion(cmd.InstitucionId))
            throw new DomainException("Debe seleccionar una institución dentro de su alcance asignado.");

        var inst = await institucionRepo.GetByIdAsync(cmd.InstitucionId, ct)
            ?? throw new NotFoundException(nameof(Institucion), cmd.InstitucionId);

        var c = Contacto.Crear(cmd.Nombre, inst.Id, inst.Nombre, cmd.Cargo, cmd.Correo, cmd.Telefono, cmd.Notas);
        await repo.AddAsync(c, ct);
        await uow.SaveChangesAsync(ct);
        return c.Id;
    }
}

public sealed class CrearContactoCommandValidator : AbstractValidator<CrearContactoCommand>
{
    public CrearContactoCommandValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
        RuleFor(x => x.InstitucionId).GreaterThan(0).WithMessage("Seleccione una institución.");
        RuleFor(x => x.Correo).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Correo))
            .WithMessage("Correo no válido.");
        RuleFor(x => x.Telefono).MaximumLength(40);
        RuleFor(x => x.Cargo).MaximumLength(150);
        RuleFor(x => x.Notas).MaximumLength(1000);
    }
}
