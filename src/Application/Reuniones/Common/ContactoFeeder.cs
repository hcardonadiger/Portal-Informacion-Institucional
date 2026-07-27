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
        var reunionInst = !string.IsNullOrWhiteSpace(r.InstitucionId) ? await instRepo.GetByIdAsync(r.InstitucionId, ct) : null;

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
                // El Id se deriva del nombre normalizado. Debe quedar sólo con A-Z y 0-9: hay que
                // quitar los acentos primero, porque char.IsLetterOrDigit acepta 'Í'/'Ó' y el
                // dominio los rechaza (rompía la creación de instituciones como "SECRETARÍA…").
                var idGenerado = NormalizarId(institucionTexto!);
                if (string.IsNullOrWhiteSpace(idGenerado)) continue;

                // Si ya existe por Id (creado en otro flujo), se usa el existente.
                inst = await instRepo.GetByIdAsync(idGenerado, ct)
                    ?? Institucion.Crear(idGenerado, institucionTexto!);

                if (inst.Id == idGenerado && !await instRepo.TieneExpedientesAsync(idGenerado, ct))
                    await instRepo.AddAsync(inst, ct);
                await uow.SaveChangesAsync(ct); // asigna el Id antes de usarlo en el contacto
            }

            await contactoRepo.AddAsync(
                Contacto.Crear(a.Nombre, inst.Id, inst.Nombre, r.AreaId, r.UnidadId, a.Cargo, correo, a.Telefono,
                    $"Registrado desde reunión: {r.Titulo}", OrigenContacto.Reunion), ct);
        }
    }

    /// <summary>Id de institución válido: mayúsculas sin acentos, sólo A-Z y 0-9.</summary>
    private static string NormalizarId(string nombre)
    {
        var desc = nombre.Trim().ToUpperInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(desc.Length);
        foreach (var ch in desc)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (ch is >= 'A' and <= 'Z' or >= '0' and <= '9') sb.Append(ch);
        }
        return sb.ToString();
    }
}
