using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Contactos.Commands.ReactivarContacto;

public sealed record ReactivarContactoCommand(int Id) : IRequest<Unit>;

public sealed class ReactivarContactoCommandHandler(IContactoRepository repo, IUnitOfWork uow)
    : IRequestHandler<ReactivarContactoCommand, Unit>
{
    public async Task<Unit> Handle(ReactivarContactoCommand cmd, CancellationToken ct)
    {
        var c = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Contacto), cmd.Id);

        c.Reactivar();
        repo.Update(c);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
