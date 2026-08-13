namespace Diger.TramitesEstado.Application.Usuarios.Queries.AutenticarUsuario;

public sealed record AsignacionAuthDto(string InstitucionId, string? AreaId, string? UnidadId, string Rol);

/// <summary><paramref name="RolGlobal"/> es null cuando el usuario no tiene ninguna asignación:
/// no tiene rol, y eso NO se rellena con uno por defecto — ver la nota del handler.</summary>
public sealed record UsuarioAuthDto(Guid Id, string Nombre, string Correo, string? RolGlobal, IReadOnlyList<AsignacionAuthDto> Asignaciones);

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
            .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
            .ToListAsync(ct);

        var asignaciones = asignacionesEntity
            .Select(a => new AsignacionAuthDto(a.InstitucionId, a.AreaId, a.UnidadId, a.Rol))
            .ToList();

        // Sin asignaciones NO hay rol, y no se inventa uno.
        //
        // Acá había un `?? "Empleado"` que le daba a cualquier cuenta sin configurar el rol
        // Empleado completo (32 claves en la matriz actual, incluidas Expedientes.Editar y
        // Contactos.Eliminar). Anulaba una propiedad que el sistema ya tiene: CurrentUserService
        // falla cerrado cuando el rol no se resuelve —alcance mínimo y sin capacidades— y
        // PermissionAuthorizationHandler no aprueba nada sin rol. Dejando esto en null, esa
        // red de seguridad hace su trabajo en vez de quedar tapada.
        var rolGlobal = asignaciones.FirstOrDefault()?.Rol;

        return new UsuarioAuthDto(usuario.Id, usuario.Nombre, usuario.Correo, rolGlobal, asignaciones);
    }
}
