namespace Diger.TramitesEstado.Infrastructure.Persistence;

/// <summary>
/// Siembra los usuarios iniciales en runtime (los hashes de contraseña no se
/// pueden materializar de forma estable vía HasData en una migración).
/// Idempotente: no hace nada si ya existen usuarios.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedUsuariosAsync(
        AppDbContext db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        if (await db.Usuarios.AnyAsync(ct))
            return;

        var admin = Usuario.Crear("Administrador DIGER", "admin@diger.gob.hn", hasher.Hash("Admin#2026"));
        var coord = Usuario.Crear("Coordinador DIGER", "coordinador@diger.gob.hn", hasher.Hash("Coord#2026"));
        var tecnico = Usuario.Crear("Técnico DIGER", "tecnico@diger.gob.hn", hasher.Hash("Tecnico#2026"));

        await db.Usuarios.AddRangeAsync(new[] { admin, coord, tecnico }, ct);
        
        await db.AsignacionesUsuario.AddRangeAsync(
            new[] {
                AsignacionUsuario.Crear(admin.Id,   "DIGER", null, null, "Administrador"),
                AsignacionUsuario.Crear(coord.Id,   "DIGER", null, null, "Coordinador"),
                AsignacionUsuario.Crear(tecnico.Id, "DIGER", null, null, "Tecnico")
            }, ct
        );

        await db.SaveChangesAsync(ct);
    }
}
