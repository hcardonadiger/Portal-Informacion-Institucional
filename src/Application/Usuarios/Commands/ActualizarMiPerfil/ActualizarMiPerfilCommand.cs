using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using MediatR;

namespace Diger.TramitesEstado.Application.Usuarios.Commands.ActualizarMiPerfil;

public sealed record ActualizarMiPerfilCommand(Guid UsuarioId, string Nombre, string Correo, string? PasswordActual, string? PasswordNuevo)
    : IRequest<Unit>;

public sealed class ActualizarMiPerfilCommandHandler(IUsuarioRepository repo, IPasswordHasher hasher, IUnitOfWork uow)
    : IRequestHandler<ActualizarMiPerfilCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarMiPerfilCommand cmd, CancellationToken ct)
    {
        var u = await repo.GetByIdAsync(cmd.UsuarioId, ct)
            ?? throw new NotFoundException(nameof(Usuario), cmd.UsuarioId);

        if (await repo.ExisteCorreoAsync(cmd.Correo, cmd.UsuarioId, ct))
            throw new DomainException($"Ya existe otro usuario con el correo {cmd.Correo.Trim().ToLowerInvariant()}.");

        u.Renombrar(cmd.Nombre);
        u.CambiarCorreo(cmd.Correo);

        if (!string.IsNullOrWhiteSpace(cmd.PasswordNuevo))
        {
            if (string.IsNullOrWhiteSpace(cmd.PasswordActual))
                throw new DomainException("Debe proporcionar su contraseña actual para poder cambiarla.");

            if (!hasher.Verify(cmd.PasswordActual, u.PasswordHash))
                throw new DomainException("La contraseña actual proporcionada es incorrecta.");

            var nuevoHash = hasher.Hash(cmd.PasswordNuevo);
            u.CambiarPassword(nuevoHash);
        }

        repo.Update(u);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class ActualizarMiPerfilCommandValidator : AbstractValidator<ActualizarMiPerfilCommand>
{
    public ActualizarMiPerfilCommandValidator()
    {
        RuleFor(x => x.UsuarioId).NotEmpty();
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Correo).NotEmpty().EmailAddress().MaximumLength(200);
        
        RuleFor(x => x.PasswordNuevo)
            .MinimumLength(6).WithMessage("La nueva contraseña debe tener al menos 6 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.PasswordNuevo));
    }
}
