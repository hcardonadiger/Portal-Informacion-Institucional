using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using MediatR;

namespace Diger.TramitesEstado.Application.Usuarios.Commands.VincularMiCertificado;

public sealed record VincularMiCertificadoCommand(Guid UsuarioId, string? Thumbprint)
    : IRequest<Unit>;

public sealed class VincularMiCertificadoCommandHandler(IUsuarioRepository repo, IUnitOfWork uow)
    : IRequestHandler<VincularMiCertificadoCommand, Unit>
{
    public async Task<Unit> Handle(VincularMiCertificadoCommand cmd, CancellationToken ct)
    {
        var u = await repo.GetByIdAsync(cmd.UsuarioId, ct)
            ?? throw new NotFoundException(nameof(Usuario), cmd.UsuarioId);

        if (!string.IsNullOrWhiteSpace(cmd.Thumbprint))
        {
            var existente = await repo.GetByCertificadoThumbprintAsync(System.Text.RegularExpressions.Regex.Replace(cmd.Thumbprint, @"[^\da-fA-F]", "").ToUpperInvariant(), ct);
            if (existente != null && existente.Id != cmd.UsuarioId)
                throw new DomainException("Este certificado digital ya se encuentra vinculado a otro usuario del sistema.");
        }

        u.VincularCertificado(cmd.Thumbprint);

        repo.Update(u);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class VincularMiCertificadoCommandValidator : AbstractValidator<VincularMiCertificadoCommand>
{
    public VincularMiCertificadoCommandValidator()
    {
        RuleFor(x => x.UsuarioId).NotEmpty();
    }
}
