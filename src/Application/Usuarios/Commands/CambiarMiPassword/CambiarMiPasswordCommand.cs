using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using MediatR;

namespace Diger.TramitesEstado.Application.Usuarios.Commands.CambiarMiPassword;

public sealed record CambiarMiPasswordCommand(Guid UsuarioId, string PasswordActual, string PasswordNuevo)
    : IRequest<Unit>;

public sealed class CambiarMiPasswordCommandHandler(IUsuarioRepository repo, IPasswordHasher hasher, IUnitOfWork uow)
    : IRequestHandler<CambiarMiPasswordCommand, Unit>
{
    public async Task<Unit> Handle(CambiarMiPasswordCommand cmd, CancellationToken ct)
    {
        var u = await repo.GetByIdAsync(cmd.UsuarioId, ct)
            ?? throw new NotFoundException(nameof(Usuario), cmd.UsuarioId);

        if (!hasher.Verify(cmd.PasswordActual, u.PasswordHash))
            throw new DomainException("La contraseña actual proporcionada es incorrecta.");

        u.CambiarPassword(hasher.Hash(cmd.PasswordNuevo));

        repo.Update(u);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class CambiarMiPasswordCommandValidator : AbstractValidator<CambiarMiPasswordCommand>
{
    public CambiarMiPasswordCommandValidator()
    {
        RuleFor(x => x.UsuarioId).NotEmpty();
        RuleFor(x => x.PasswordActual).NotEmpty().WithMessage("La contraseña actual es requerida.");
        RuleFor(x => x.PasswordNuevo)
            .NotEmpty().WithMessage("La nueva contraseña es requerida.")
            .MinimumLength(6).WithMessage("La nueva contraseña debe tener al menos 6 caracteres.");
    }
}
