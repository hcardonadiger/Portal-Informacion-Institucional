using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Usuarios.Commands.AsignarTemasUsuario;

/// <summary>Reemplaza los temas de ticket que atiende un usuario (especialidad).</summary>
public sealed record AsignarTemasUsuarioCommand(Guid UsuarioId, IReadOnlyList<int> TemaIds)
    : IRequest<Unit>;

public sealed class AsignarTemasUsuarioCommandHandler(IUsuarioRepository repo, IUnitOfWork uow)
    : IRequestHandler<AsignarTemasUsuarioCommand, Unit>
{
    public async Task<Unit> Handle(AsignarTemasUsuarioCommand cmd, CancellationToken ct)
    {
        _ = await repo.GetByIdAsync(cmd.UsuarioId, ct)
            ?? throw new NotFoundException(nameof(Usuario), cmd.UsuarioId);

        await repo.ReemplazarTemasAsync(cmd.UsuarioId, cmd.TemaIds ?? [], ct);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
