using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Application.Recursos.Commands.ActualizarRecurso;

public sealed record ActualizarRecursoCommand(
    int     Id,
    string  Titulo,
    string? Descripcion,
    string  Categoria,
    string? ArchivoNombre = null,
    string? ArchivoUrl = null,
    long?   ArchivoTamano = null) : IRequest<Unit>;

public sealed class ActualizarRecursoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<ActualizarRecursoCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarRecursoCommand cmd, CancellationToken ct)
    {
        if (currentUser.Rol != "Administrador")
            throw new UnauthorizedAccessException("Solo los usuarios con rol Administrador pueden modificar recursos.");

        var recurso = await ctx.Recursos.FirstOrDefaultAsync(r => r.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Recurso), cmd.Id);

        recurso.Actualizar(
            cmd.Titulo,
            cmd.Descripcion,
            cmd.Categoria,
            cmd.ArchivoNombre,
            cmd.ArchivoUrl,
            cmd.ArchivoTamano
        );

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
