using Diger.TramitesEstado.Application.Tickets.Common;

namespace Diger.TramitesEstado.Application.Tickets.Queries.GetUsuariosAsignables;

/// <summary>Usuarios internos activos a los que se puede asignar un ticket.</summary>
public sealed record GetUsuariosAsignablesQuery : IRequest<IReadOnlyList<UsuarioAsignableDto>>;

public sealed class GetUsuariosAsignablesQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetUsuariosAsignablesQuery, IReadOnlyList<UsuarioAsignableDto>>
{
    public async Task<IReadOnlyList<UsuarioAsignableDto>> Handle(GetUsuariosAsignablesQuery _, CancellationToken ct) =>
        await ctx.Usuarios
            .AsNoTracking()
            .Where(u => u.Activo)
            .OrderBy(u => u.Nombre)
            .Select(u => new UsuarioAsignableDto(u.Id, u.Nombre, 
                ctx.AsignacionesUsuario.Where(a => a.UsuarioId == u.Id).Select(a => a.Rol).FirstOrDefault() ?? "Empleado"))
            .ToListAsync(ct);
}
