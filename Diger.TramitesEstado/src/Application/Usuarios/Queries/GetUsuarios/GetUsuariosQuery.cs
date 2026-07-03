using Diger.TramitesEstado.Application.Common.Models;
using Diger.TramitesEstado.Application.Usuarios.Common;

namespace Diger.TramitesEstado.Application.Usuarios.Queries.GetUsuarios;

public sealed record GetUsuariosQuery(string? Q = null, int? Page = null, int? Size = null)
    : IRequest<PagedResult<UsuarioListItemDto>>;

public sealed class GetUsuariosQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetUsuariosQuery, PagedResult<UsuarioListItemDto>>
{
    public async Task<PagedResult<UsuarioListItemDto>> Handle(GetUsuariosQuery query, CancellationToken ct)
    {
        var (q, page, size) = Paginacion.Normalizar(query.Q, query.Page, query.Size);

        var baseq = ctx.Usuarios.AsNoTracking();
        if (q is not null)
            baseq = baseq.Where(u => u.Nombre.Contains(q) || u.Correo.Contains(q));

        var total = await baseq.CountAsync(ct);
        var items = await baseq
            .OrderBy(u => u.Rol).ThenBy(u => u.Nombre)
            .Skip((page - 1) * size).Take(size)
            .Select(u => new UsuarioListItemDto(u.Id, u.Nombre, u.Correo, u.Rol, u.Activo, u.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<UsuarioListItemDto>(items, total, page, size);
    }
}
