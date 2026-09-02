using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Application.Recursos.Commands.EliminarRecurso;

public sealed record EliminarRecursoCommand(int Id) : IRequest<Unit>;

public sealed class EliminarRecursoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<EliminarRecursoCommand, Unit>
{
    public async Task<Unit> Handle(EliminarRecursoCommand cmd, CancellationToken ct)
    {
        if (currentUser.Rol != "Administrador")
            throw new UnauthorizedAccessException("Solo los usuarios con rol Administrador pueden eliminar recursos.");

        var recurso = await ctx.Recursos.FirstOrDefaultAsync(r => r.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Recurso), cmd.Id);

        recurso.IsDeleted = true;
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
