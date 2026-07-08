using FluentValidation;
using Diger.TramitesEstado.Domain.Enums;

namespace Diger.TramitesEstado.Application.Usuarios.Commands.CrearUsuario;

public sealed record CrearUsuarioCommand(string Nombre, string Correo, string Password)
    : IRequest<Guid>;

public sealed class CrearUsuarioCommandHandler(
    IUsuarioRepository repo, IPasswordHasher hasher, IUnitOfWork uow)
    : IRequestHandler<CrearUsuarioCommand, Guid>
{
    public async Task<Guid> Handle(CrearUsuarioCommand cmd, CancellationToken ct)
    {
        if (await repo.ExisteCorreoAsync(cmd.Correo, null, ct))
            throw new DomainException($"Ya existe un usuario con el correo {cmd.Correo.Trim().ToLowerInvariant()}.");

        var u = Usuario.Crear(cmd.Nombre, cmd.Correo, hasher.Hash(cmd.Password));
        await repo.AddAsync(u, ct);
        await uow.SaveChangesAsync(ct);
        return u.Id;
    }
}

public sealed class CrearUsuarioCommandValidator : AbstractValidator<CrearUsuarioCommand>
{
    public CrearUsuarioCommandValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Correo).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .WithMessage("La contraseña debe tener al menos 8 caracteres.");
    }
}
