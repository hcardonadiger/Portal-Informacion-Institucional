using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using MediatR;

namespace Diger.TramitesEstado.Application.Recursos.Commands.CrearRecurso;

public sealed record CrearRecursoCommand(
    string Titulo,
    string? Descripcion,
    string Categoria,
    string ArchivoNombre,
    string ArchivoUrl,
    long ArchivoTamano) : IRequest<int>;

public sealed class CrearRecursoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<CrearRecursoCommand, int>
{
    public async Task<int> Handle(CrearRecursoCommand cmd, CancellationToken ct)
    {
        if (currentUser.Rol != "Administrador")
            throw new UnauthorizedAccessException("Solo los usuarios con rol Administrador pueden subir recursos.");

        var recurso = Recurso.Crear(
            cmd.Titulo,
            cmd.Descripcion,
            cmd.Categoria,
            cmd.ArchivoNombre,
            cmd.ArchivoUrl,
            cmd.ArchivoTamano
        );

        ctx.Recursos.Add(recurso);
        await ctx.SaveChangesAsync(ct);
        return recurso.Id;
    }
}
