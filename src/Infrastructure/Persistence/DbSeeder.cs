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

        var usuarios = new[]
        {
            Usuario.Crear("Administrador DIGER", "admin@diger.gob.hn",       hasher.Hash("Admin#2026"),   RolUsuario.Administrador),
            Usuario.Crear("Coordinador DIGER",   "coordinador@diger.gob.hn", hasher.Hash("Coord#2026"),   RolUsuario.Coordinador),
            Usuario.Crear("Técnico DIGER",       "tecnico@diger.gob.hn",     hasher.Hash("Tecnico#2026"), RolUsuario.Tecnico),
        };

        await db.Usuarios.AddRangeAsync(usuarios, ct);
        await db.SaveChangesAsync(ct);
    }
}
