namespace Diger.TramitesEstado.Application.Reuniones.Common;

/// <summary>Alimenta el Directorio de Contactos con los asistentes de una reunión
/// (deduplicado por correo; Origen = Reunion).</summary>
internal static class ContactoFeeder
{
    public static async Task FeedAsync(
        Reunion r, IContactoRepository contactoRepo, IInstitucionRepository instRepo, CancellationToken ct)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reunionInst = !string.IsNullOrWhiteSpace(r.InstitucionId) ? await instRepo.GetByIdAsync(r.InstitucionId, ct) : null;

        foreach (var a in r.Asistentes)
        {
            var correo = a.Correo?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(correo)) continue;
            if (!seen.Add(correo)) continue;
            if (await contactoRepo.ExisteCorreoAsync(correo, ct)) continue;

            var inst = (!string.IsNullOrWhiteSpace(a.Institucion)
                ? await instRepo.GetByNombreAsync(a.Institucion!, ct)
                : null) ?? reunionInst;
            if (inst is null) continue;

            await contactoRepo.AddAsync(
                Contacto.Crear(a.Nombre, inst.Id, inst.Nombre, null, null, a.Cargo, correo, a.Telefono,
                    $"Registrado desde reunión: {r.Titulo}", OrigenContacto.Reunion), ct);
        }
    }
}
