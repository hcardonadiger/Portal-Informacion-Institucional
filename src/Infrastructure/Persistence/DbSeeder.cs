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

        // 1. Crear Jerarquía Básica
        var inst = Institucion.Crear("DIGER", "Dirección General de Gobierno Digital");
        var area = Area.Crear("TIC", "DIGER", "Tecnologías de la Información");
        var unidad = Unidad.Crear("DEV", "TIC", "Desarrollo de Software");

        if (!await db.Instituciones.AnyAsync(i => i.Id == "DIGER", ct))
        {
            await db.Instituciones.AddAsync(inst, ct);
            await db.Areas.AddAsync(area, ct);
            await db.Unidades.AddAsync(unidad, ct);
        }

        // 2. Crear Usuarios de Prueba
        var admin = Usuario.Crear("Admin Global", "admin@diger.gob.hn", hasher.Hash("Admin#2026"));
        var jefeInst = Usuario.Crear("Jefe DIGER", "jefe.inst@diger.gob.hn", hasher.Hash("JefeInst#2026"));
        var jefeArea = Usuario.Crear("Jefe TIC", "jefe.area@diger.gob.hn", hasher.Hash("JefeArea#2026"));
        var jefeUni = Usuario.Crear("Jefe DEV", "jefe.uni@diger.gob.hn", hasher.Hash("JefeUni#2026"));
        var emp = Usuario.Crear("Empleado DEV", "empleado@diger.gob.hn", hasher.Hash("Empleado#2026"));
        var cons = Usuario.Crear("Consultor DIGER", "consultor@diger.gob.hn", hasher.Hash("Consultor#2026"));

        await db.Usuarios.AddRangeAsync([admin, jefeInst, jefeArea, jefeUni, emp, cons], ct);
        
        // 3. Asignar Roles y Alcance
        await db.AsignacionesUsuario.AddRangeAsync(
            [
                AsignacionUsuario.Crear(admin.Id,    "DIGER", null,  null,  "Administrador"),
                AsignacionUsuario.Crear(jefeInst.Id, "DIGER", null,  null,  "JefeInstitucion"),
                AsignacionUsuario.Crear(jefeArea.Id, "DIGER", "TIC", null,  "JefeArea"),
                AsignacionUsuario.Crear(jefeUni.Id,  "DIGER", "TIC", "DEV", "JefeUnidad"),
                AsignacionUsuario.Crear(emp.Id,      "DIGER", "TIC", "DEV", "Empleado"),
                AsignacionUsuario.Crear(cons.Id,     "DIGER", null,  null,  "Consultor")
            ], ct
        );

        await db.SaveChangesAsync(ct);
    }
}
