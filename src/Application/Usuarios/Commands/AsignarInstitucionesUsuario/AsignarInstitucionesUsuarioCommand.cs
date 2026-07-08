using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Usuarios.Commands.AsignarInstitucionesUsuario;

using Diger.TramitesEstado.Application.Usuarios.Common;

public sealed record AsignarJerarquiaUsuarioCommand(Guid UsuarioId, string Rol, IReadOnlyList<AsignacionDto> Asignaciones)
    : IRequest<Unit>;

public sealed class AsignarJerarquiaUsuarioCommandHandler(IUsuarioRepository repo, IUnitOfWork uow)
    : IRequestHandler<AsignarJerarquiaUsuarioCommand, Unit>
{
    public async Task<Unit> Handle(AsignarJerarquiaUsuarioCommand cmd, CancellationToken ct)
    {
        _ = await repo.GetByIdAsync(cmd.UsuarioId, ct)
            ?? throw new NotFoundException(nameof(Usuario), cmd.UsuarioId);

        await repo.ReemplazarAsignacionesAsync(cmd.UsuarioId, cmd.Rol, cmd.Asignaciones ?? [], ct);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
