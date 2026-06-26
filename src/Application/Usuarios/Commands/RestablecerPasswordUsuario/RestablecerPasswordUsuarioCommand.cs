using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Usuarios.Commands.RestablecerPasswordUsuario;

public sealed record RestablecerPasswordUsuarioCommand(int Id, string NuevaPassword) : IRequest<Unit>;

public sealed class RestablecerPasswordUsuarioCommandHandler(
    IUsuarioRepository repo, IPasswordHasher hasher, IUnitOfWork uow)
    : IRequestHandler<RestablecerPasswordUsuarioCommand, Unit>
{
    public async Task<Unit> Handle(RestablecerPasswordUsuarioCommand cmd, CancellationToken ct)
    {
        var u = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Usuario), cmd.Id);

        u.CambiarPassword(hasher.Hash(cmd.NuevaPassword));
        repo.Update(u);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class RestablecerPasswordUsuarioCommandValidator : AbstractValidator<RestablecerPasswordUsuarioCommand>
{
    public RestablecerPasswordUsuarioCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NuevaPassword).NotEmpty().MinimumLength(8)
            .WithMessage("La contraseña debe tener al menos 8 caracteres.");
    }
}
