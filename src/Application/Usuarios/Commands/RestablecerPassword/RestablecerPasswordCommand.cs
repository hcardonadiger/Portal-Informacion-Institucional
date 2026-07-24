using FluentValidation;

namespace Diger.TramitesEstado.Application.Usuarios.Commands.RestablecerPassword;

public sealed record RestablecerPasswordCommand(
    string Correo,
    string Token,
    string NuevaPassword,
    string ConfirmarPassword) : IRequest<Unit>;

public sealed class RestablecerPasswordCommandValidator : AbstractValidator<RestablecerPasswordCommand>
{
    public RestablecerPasswordCommandValidator()
    {
        RuleFor(x => x.Correo).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty().WithMessage("El token de restablecimiento es requerido.");
        RuleFor(x => x.NuevaPassword)
            .NotEmpty().WithMessage("La nueva contraseña es requerida.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.");
        RuleFor(x => x.ConfirmarPassword)
            .Equal(x => x.NuevaPassword).WithMessage("La confirmación de contraseña no coincide con la nueva contraseña.");
    }
}

public sealed class RestablecerPasswordCommandHandler(
    IApplicationDbContext ctx,
    IUnitOfWork uow,
    IPasswordHasher passwordHasher) : IRequestHandler<RestablecerPasswordCommand, Unit>
{
    public async Task<Unit> Handle(RestablecerPasswordCommand cmd, CancellationToken ct)
    {
        var correoNormalizado = cmd.Correo.Trim().ToLowerInvariant();
        var usuario = await ctx.Usuarios
            .FirstOrDefaultAsync(u => u.Correo == correoNormalizado && u.Activo, ct);

        if (usuario is null || !usuario.EsTokenRecuperacionValido(cmd.Token))
        {
            throw new DomainException("El enlace de restablecimiento es inválido o ha expirado. Por favor solicite uno nuevo.");
        }

        var nuevoHash = passwordHasher.Hash(cmd.NuevaPassword);
        usuario.CambiarPassword(nuevoHash);
        usuario.LimpiarTokenRecuperacion();

        await uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
