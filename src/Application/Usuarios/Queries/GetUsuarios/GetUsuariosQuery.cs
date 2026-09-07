using Diger.TramitesEstado.Application.Common.Models;
using Diger.TramitesEstado.Application.Usuarios.Common;

namespace Diger.TramitesEstado.Application.Usuarios.Queries.GetUsuarios;

public sealed record GetUsuariosQuery(
    string? Q = null, int? Page = null, int? Size = null, bool IncluirEliminados = false)
    : IRequest<PagedResult<UsuarioListItemDto>>;

public sealed class GetUsuariosQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetUsuariosQuery, PagedResult<UsuarioListItemDto>>
{
    public async Task<PagedResult<UsuarioListItemDto>> Handle(GetUsuariosQuery query, CancellationToken ct)
    {
        var (q, page, size) = Paginacion.Normalizar(query.Q, query.Page, query.Size);

        // El filtro global de AppDbContext ya esconde a los eliminados; esto lo levanta solo
        // cuando la lista los pide. Es la única vía del portal para volver a verlos.
        var baseq = query.IncluirEliminados
            ? ctx.Usuarios.AsNoTracking().IgnoreQueryFilters()
            : ctx.Usuarios.AsNoTracking();
        if (q is not null)
            baseq = baseq.Where(u => u.Nombre.Contains(q) || u.Correo.Contains(q));

        var total = await baseq.CountAsync(ct);
        var items = await baseq
            .OrderBy(u => u.Nombre)
            .Skip((page - 1) * size).Take(size)
            .Select(u => new UsuarioListItemDto(u.Id, u.Nombre, u.Correo, 
                ctx.AsignacionesUsuario.Where(a => a.UsuarioId == u.Id).OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).Select(a => a.Rol).FirstOrDefault() ?? "Empleado",
                u.Activo, u.CreatedAt, u.IsDeleted))
            .ToListAsync(ct);

        return new PagedResult<UsuarioListItemDto>(items, total, page, size);
    }
}
