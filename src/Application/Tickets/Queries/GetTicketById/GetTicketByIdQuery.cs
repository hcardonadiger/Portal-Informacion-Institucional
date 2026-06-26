using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Tickets.Common;

namespace Diger.TramitesEstado.Application.Tickets.Queries.GetTicketById;

public sealed record GetTicketByIdQuery(int Id) : IRequest<TicketDetailDto>;

public sealed class GetTicketByIdQueryHandler(ITicketRepository repo)
    : IRequestHandler<GetTicketByIdQuery, TicketDetailDto>
{
    public async Task<TicketDetailDto> Handle(GetTicketByIdQuery q, CancellationToken ct)
    {
        var t = await repo.GetByIdWithDetailsAsync(q.Id, ct)
            ?? throw new NotFoundException(nameof(Ticket), q.Id);
        return TicketMapper.ToDetail(t);
    }
}
