namespace Diger.TramitesEstado.Application.Usuarios.Queries.AutenticarUsuario;

public sealed record AsignacionDto(string InstitucionId, string? AreaId, string? UnidadId, string Rol);

public sealed record UsuarioAuthDto(Guid Id, string Nombre, string Correo, string RolGlobal, IReadOnlyList<AsignacionDto> Asignaciones);

public sealed record AutenticarUsuarioQuery(string Correo, string Password)
    : IRequest<UsuarioAuthDto?>;

public sealed class AutenticarUsuarioQueryHandler(
    IUsuarioRepository repo,
    IApplicationDbContext ctx,
    IPasswordHasher hasher)
    : IRequestHandler<AutenticarUsuarioQuery, UsuarioAuthDto?>
{
    public async Task<UsuarioAuthDto?> Handle(AutenticarUsuarioQuery q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q.Correo) || string.IsNullOrWhiteSpace(q.Password))
            return null;

        var usuario = await repo.GetByCorreoAsync(q.Correo.Trim().ToLowerInvariant(), ct);
        if (usuario is null || !usuario.Activo)
            return null;

        if (!hasher.Verify(q.Password, usuario.PasswordHash))
            return null;

        var asignacionesEntity = await ctx.AsignacionesUsuario
            .Where(a => a.UsuarioId == usuario.Id)
            .ToListAsync(ct);

        var asignaciones = asignacionesEntity
            .Select(a => new AsignacionDto(a.InstitucionId, a.AreaId, a.UnidadId, a.Rol))
            .ToList();

        var rolGlobal = asignaciones.FirstOrDefault()?.Rol ?? "Empleado";

        return new UsuarioAuthDto(usuario.Id, usuario.Nombre, usuario.Correo, rolGlobal, asignaciones);
    }
}
