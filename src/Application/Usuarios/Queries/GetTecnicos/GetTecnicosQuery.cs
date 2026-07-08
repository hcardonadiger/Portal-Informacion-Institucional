namespace Diger.TramitesEstado.Application.Usuarios.Queries.GetTecnicos;

public sealed record TecnicoDto(Guid Id, string Nombre, string Correo);

public sealed record GetTecnicosQuery : IRequest<IReadOnlyList<TecnicoDto>>;

public sealed class GetTecnicosQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetTecnicosQuery, IReadOnlyList<TecnicoDto>>
{
    public async Task<IReadOnlyList<TecnicoDto>> Handle(GetTecnicosQuery _, CancellationToken ct) =>
        await ctx.Usuarios
            .AsNoTracking()
            .Where(u => u.Activo && ctx.AsignacionesUsuario.Any(a => a.UsuarioId == u.Id && a.Rol == "Tecnico"))
            .OrderBy(u => u.Nombre)
            .Select(u => new TecnicoDto(u.Id, u.Nombre, u.Correo))
            .ToListAsync(ct);
}
