using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Usuarios.Commands.AsignarInstitucionesUsuario;

public sealed record AsignarInstitucionesUsuarioCommand(int UsuarioId, IReadOnlyList<int> InstitucionIds)
    : IRequest<Unit>;

public sealed class AsignarInstitucionesUsuarioCommandHandler(IUsuarioRepository repo, IUnitOfWork uow)
    : IRequestHandler<AsignarInstitucionesUsuarioCommand, Unit>
{
    public async Task<Unit> Handle(AsignarInstitucionesUsuarioCommand cmd, CancellationToken ct)
    {
        _ = await repo.GetByIdAsync(cmd.UsuarioId, ct)
            ?? throw new NotFoundException(nameof(Usuario), cmd.UsuarioId);

        await repo.ReemplazarInstitucionesAsync(cmd.UsuarioId, cmd.InstitucionIds ?? [], ct);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
