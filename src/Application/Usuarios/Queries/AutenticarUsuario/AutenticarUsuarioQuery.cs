namespace Diger.TramitesEstado.Application.Usuarios.Queries.AutenticarUsuario;

public sealed record UsuarioAuthDto(int Id, string Nombre, string Correo, RolUsuario Rol, IReadOnlyList<int> Instituciones);

public sealed record AutenticarUsuarioQuery(string Correo, string Password)
    : IRequest<UsuarioAuthDto?>;

public sealed class AutenticarUsuarioQueryHandler(
    IUsuarioRepository repo,
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

        var instituciones = await repo.GetInstitucionIdsAsync(usuario.Id, ct);
        return new UsuarioAuthDto(usuario.Id, usuario.Nombre, usuario.Correo, usuario.Rol, instituciones);
    }
}
