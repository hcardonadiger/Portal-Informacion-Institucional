using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Contactos.Commands.DarDeBajaContacto;

public sealed record DarDeBajaContactoCommand(int Id) : IRequest<Unit>;

public sealed class DarDeBajaContactoCommandHandler(IContactoRepository repo, IUnitOfWork uow)
    : IRequestHandler<DarDeBajaContactoCommand, Unit>
{
    public async Task<Unit> Handle(DarDeBajaContactoCommand cmd, CancellationToken ct)
    {
        var c = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Contacto), cmd.Id);

        c.DarDeBaja();
        repo.Update(c);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
