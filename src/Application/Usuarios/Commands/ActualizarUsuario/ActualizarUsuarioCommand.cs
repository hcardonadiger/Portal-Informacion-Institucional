using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Usuarios.Common;
using Diger.TramitesEstado.Domain.Enums;

namespace Diger.TramitesEstado.Application.Usuarios.Commands.ActualizarUsuario;

public sealed record ActualizarUsuarioCommand(Guid Id, string Nombre, string Correo, bool Activo, string? CertificadoThumbprint = null)
    : IRequest<Unit>;

public sealed class ActualizarUsuarioCommandHandler(
    IUsuarioRepository repo, IUnitOfWork uow, IApplicationDbContext ctx, IRolCatalogo catalogo)
    : IRequestHandler<ActualizarUsuarioCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarUsuarioCommand cmd, CancellationToken ct)
    {
        var u = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Usuario), cmd.Id);

        if (await repo.ExisteCorreoAsync(cmd.Correo, cmd.Id, ct))
            throw new DomainException($"Ya existe otro usuario con el correo {cmd.Correo.Trim().ToLowerInvariant()}.");

        // Desactivar al último administrador tiene el mismo efecto que quitarle el rol: un
        // usuario inactivo no puede iniciar sesión (ver AutenticarUsuarioQuery).
        if (u.Activo && !cmd.Activo &&
            await AdministradoresInvariante.TieneRolAdministradorAsync(ctx, catalogo, cmd.Id, ct))
        {
            await AdministradoresInvariante.ValidarNoEsElUltimoAdministradorAsync(ctx, catalogo, cmd.Id, ct);
        }

        u.Renombrar(cmd.Nombre);
        u.CambiarCorreo(cmd.Correo);
        u.VincularCertificado(cmd.CertificadoThumbprint);
        
        if (cmd.Activo) u.Activar(); else u.Desactivar();

        repo.Update(u);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class ActualizarUsuarioCommandValidator : AbstractValidator<ActualizarUsuarioCommand>
{
    public ActualizarUsuarioCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Correo).NotEmpty().EmailAddress().MaximumLength(200);
    }
}
