using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Usuarios.Common;

namespace Diger.TramitesEstado.Application.Usuarios.Queries.GetUsuarioById;

public sealed record GetUsuarioByIdQuery(int Id) : IRequest<UsuarioDetailDto>;

public sealed class GetUsuarioByIdQueryHandler(IUsuarioRepository repo)
    : IRequestHandler<GetUsuarioByIdQuery, UsuarioDetailDto>
{
    public async Task<UsuarioDetailDto> Handle(GetUsuarioByIdQuery q, CancellationToken ct)
    {
        var u = await repo.GetByIdAsync(q.Id, ct)
            ?? throw new NotFoundException(nameof(Usuario), q.Id);
        var instituciones = await repo.GetInstitucionIdsAsync(u.Id, ct);
        var temaIds       = await repo.GetTemaIdsAsync(u.Id, ct);
        return new UsuarioDetailDto(u.Id, u.Nombre, u.Correo, u.Rol, u.Activo, instituciones, temaIds);
    }
}
