using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Contactos.Commands.EliminarContacto;

public sealed record EliminarContactoCommand(int Id) : IRequest<Unit>;

public sealed class EliminarContactoCommandHandler(IContactoRepository repo, IUnitOfWork uow)
    : IRequestHandler<EliminarContactoCommand, Unit>
{
    public async Task<Unit> Handle(EliminarContactoCommand cmd, CancellationToken ct)
    {
        var c = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Contacto), cmd.Id);

        repo.Delete(c);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
