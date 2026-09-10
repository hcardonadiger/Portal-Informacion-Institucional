using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Application.Recursos.Commands.RegistrarDescarga;

public sealed record RegistrarDescargaRecursoCommand(int Id) : IRequest<Unit>;

public sealed class RegistrarDescargaRecursoCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<RegistrarDescargaRecursoCommand, Unit>
{
    public async Task<Unit> Handle(RegistrarDescargaRecursoCommand cmd, CancellationToken ct)
    {
        var recurso = await ctx.Recursos.FirstOrDefaultAsync(r => r.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Recurso), cmd.Id);

        recurso.IncrementarDescargas();
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
