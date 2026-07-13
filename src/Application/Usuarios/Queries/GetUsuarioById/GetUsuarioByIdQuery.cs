using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Usuarios.Common;

namespace Diger.TramitesEstado.Application.Usuarios.Queries.GetUsuarioById;

public sealed record GetUsuarioByIdQuery(Guid Id) : IRequest<UsuarioDetailDto>;

public sealed class GetUsuarioByIdQueryHandler(IUsuarioRepository repo, IApplicationDbContext ctx)
    : IRequestHandler<GetUsuarioByIdQuery, UsuarioDetailDto>
{
    public async Task<UsuarioDetailDto> Handle(GetUsuarioByIdQuery q, CancellationToken ct)
    {
        var u = await repo.GetByIdAsync(q.Id, ct)
            ?? throw new NotFoundException(nameof(Usuario), q.Id);
        var asignaciones = await repo.GetAsignacionesAsync(u.Id, ct);
        var temaIds       = await repo.GetTemaIdsAsync(u.Id, ct);
        var rol = await ctx.AsignacionesUsuario.Where(a => a.UsuarioId == u.Id).OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).Select(a => a.Rol).FirstOrDefaultAsync(ct) ?? "Empleado";
        return new UsuarioDetailDto(u.Id, u.Nombre, u.Correo, rol, u.Activo, asignaciones, temaIds);
    }
}
