using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Expedientes.Common;

namespace Diger.TramitesEstado.Application.Expedientes.Queries.GetExpedienteById;

public sealed record ExpedienteDetailDto(int Id, string Codigo, ExpedienteInputDto Datos);

public sealed record GetExpedienteByIdQuery(int Id) : IRequest<ExpedienteDetailDto>;

public sealed class GetExpedienteByIdQueryHandler(IExpedienteRepository repo)
    : IRequestHandler<GetExpedienteByIdQuery, ExpedienteDetailDto>
{
    public async Task<ExpedienteDetailDto> Handle(GetExpedienteByIdQuery q, CancellationToken ct)
    {
        var e = await repo.GetByIdWithDetailsAsync(q.Id, ct)
            ?? throw new NotFoundException(nameof(Expediente), q.Id);

        return new ExpedienteDetailDto(e.Id, e.Codigo, ExpedienteMapper.ToInputDto(e));
    }
}
