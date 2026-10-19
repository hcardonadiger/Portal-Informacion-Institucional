using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using MediatR;

namespace Diger.TramitesEstado.Application.Usuarios.Commands.EstablecerPasswordObligatoria;

/// <summary>
/// El cambio que exige el portón de contraseña obligatoria. No pide la contraseña actual: el
/// usuario acaba de autenticarse para llegar hasta acá, y volver a pedírsela sería trabarle la
/// salida a quien entró con una temporal que no eligió. Por eso NO es
/// <c>CambiarMiPasswordCommand</c>, que sí la exige.
/// </summary>
/// <returns>La fecha que quedó estampada, que la sesión necesita para refrescar su claim —
/// sin eso el portón seguiría reteniendo al usuario que ya cumplió.</returns>
public sealed record EstablecerPasswordObligatoriaCommand(Guid UsuarioId, string PasswordNueva)
    : IRequest<DateTime>;

public sealed class EstablecerPasswordObligatoriaCommandHandler(
    IUsuarioRepository repo, IPasswordHasher hasher, IUnitOfWork uow)
    : IRequestHandler<EstablecerPasswordObligatoriaCommand, DateTime>
{
    public async Task<DateTime> Handle(EstablecerPasswordObligatoriaCommand cmd, CancellationToken ct)
    {
        var u = await repo.GetByIdAsync(cmd.UsuarioId, ct)
            ?? throw new NotFoundException(nameof(Usuario), cmd.UsuarioId);

        // Sin esta guarda el cambio obligatorio es teatro: se podría «cumplir» reescribiendo la
        // misma contraseña temporal que el administrador entregó.
        if (hasher.Verify(cmd.PasswordNueva, u.PasswordHash))
            throw new DomainException("La nueva contraseña debe ser distinta de la que usa hoy.");

        u.CambiarPassword(hasher.Hash(cmd.PasswordNueva));

        repo.Update(u);
        await uow.SaveChangesAsync(ct);

        return u.PasswordCambiadoEl!.Value;
    }
}

public sealed class EstablecerPasswordObligatoriaCommandValidator
    : AbstractValidator<EstablecerPasswordObligatoriaCommand>
{
    public EstablecerPasswordObligatoriaCommandValidator()
    {
        RuleFor(x => x.UsuarioId).NotEmpty();
        RuleFor(x => x.PasswordNueva)
            .NotEmpty().WithMessage("La nueva contraseña es requerida.")
            .MinimumLength(8).WithMessage("La nueva contraseña debe tener al menos 8 caracteres.");
    }
}
