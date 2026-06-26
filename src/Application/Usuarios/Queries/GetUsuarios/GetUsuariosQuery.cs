using Diger.TramitesEstado.Application.Usuarios.Common;

namespace Diger.TramitesEstado.Application.Usuarios.Queries.GetUsuarios;

public sealed record GetUsuariosQuery : IRequest<IReadOnlyList<UsuarioListItemDto>>;

public sealed class GetUsuariosQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetUsuariosQuery, IReadOnlyList<UsuarioListItemDto>>
{
    public async Task<IReadOnlyList<UsuarioListItemDto>> Handle(GetUsuariosQuery _, CancellationToken ct) =>
        await ctx.Usuarios
            .AsNoTracking()
            .OrderBy(u => u.Rol).ThenBy(u => u.Nombre)
            .Select(u => new UsuarioListItemDto(u.Id, u.Nombre, u.Correo, u.Rol, u.Activo, u.CreatedAt))
            .ToListAsync(ct);
}
