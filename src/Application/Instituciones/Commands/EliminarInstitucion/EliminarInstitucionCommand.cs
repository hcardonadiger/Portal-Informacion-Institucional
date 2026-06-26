using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Instituciones.Commands.EliminarInstitucion;

public sealed record EliminarInstitucionCommand(int Id) : IRequest<Unit>;

public sealed class EliminarInstitucionCommandHandler(
    IInstitucionRepository repo,
    IUnitOfWork uow)
    : IRequestHandler<EliminarInstitucionCommand, Unit>
{
    public async Task<Unit> Handle(EliminarInstitucionCommand cmd, CancellationToken ct)
    {
        var inst = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Institucion), cmd.Id);

        if (await repo.TieneExpedientesAsync(cmd.Id, ct))
            throw new DomainException(
                "No se puede eliminar la institución porque tiene expedientes asociados. Desactívela en su lugar.");

        repo.Delete(inst);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
