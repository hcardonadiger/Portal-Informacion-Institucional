using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Application.Reuniones.Common;

/// <summary>Alimenta el Directorio de Contactos con los asistentes de una reunión
/// (deduplicado por correo; Origen = Reunion).</summary>
internal static class ContactoFeeder
{
    public static async Task FeedAsync(
        Reunion r, IContactoRepository contactoRepo, IInstitucionRepository instRepo,
        IApplicationDbContext ctx, IUnitOfWork uow, CancellationToken ct)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var a in r.Asistentes)
        {
            var correo = a.Correo?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(correo)) continue;
            if (!seen.Add(correo)) continue;
            if (await contactoRepo.ExisteCorreoAsync(correo, ct)) continue;

            // La institución del contacto se determina por su PRIMER registro de asistencia (por
            // fecha de reunión), nunca por la reunión que se está guardando ahora: una misma
            // persona puede aparecer en varias reuniones con la institución mal escrita, y solo la
            // primera vez refleja de forma confiable a qué institución pertenece de verdad.
            var primero = await ctx.Asistentes
                .Where(x => x.Correo == correo)
                .Join(ctx.Reuniones, x => x.ReunionId, rr => rr.Id,
                    (x, rr) => new { x.Nombre, x.Cargo, x.Institucion, x.Telefono, rr.Fecha, ReunionInstTexto = rr.Institucion })
                .OrderBy(x => x.Fecha == null ? 1 : 0).ThenBy(x => x.Fecha)
                .FirstOrDefaultAsync(ct);

            string nombre; string? cargo, telefono, institucionTexto;
            if (primero is not null && (r.Fecha is null || primero.Fecha is null || primero.Fecha <= r.Fecha))
            {
                nombre = primero.Nombre; cargo = primero.Cargo; telefono = primero.Telefono;
                institucionTexto = !string.IsNullOrWhiteSpace(primero.Institucion) ? primero.Institucion : primero.ReunionInstTexto;
            }
            else
            {
                nombre = a.Nombre; cargo = a.Cargo; telefono = a.Telefono;
                institucionTexto = !string.IsNullOrWhiteSpace(a.Institucion) ? a.Institucion : r.Institucion;
            }

            if (string.IsNullOrWhiteSpace(institucionTexto)) continue; // ninguna pista de institución

            var inst = await instRepo.GetByNombreAsync(institucionTexto!, ct);
            if (inst is null)
            {
                // La institución mencionada en la reunión todavía no existe en el catálogo: se crea
                // automáticamente (activa) para no perder el contacto, y queda disponible de
                // inmediato en todos los listados del portal (Instituciones, Reuniones, Contactos).
                inst = Institucion.Crear(institucionTexto!);
                await instRepo.AddAsync(inst, ct);
                await uow.SaveChangesAsync(ct); // asigna el Id antes de usarlo en el contacto
            }

            await contactoRepo.AddAsync(
                Contacto.Crear(nombre, inst.Id, inst.Nombre, cargo, correo, telefono,
                    $"Registrado desde reunión: {r.Titulo}", OrigenContacto.Reunion), ct);
        }
    }
}
