namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>Contacto del directorio institucional (enlaces por institución).</summary>
public sealed class Contacto : BaseAuditableEntity, ISoftDeletable
{
    // ── Soft Delete ───────────────────────────────────────────────
    public bool IsDeleted { get; set; }
    public string         InstitucionId { get; private set; }
    public string?        AreaId        { get; private set; }
    public string?        UnidadId      { get; private set; }
    public string         Institucion   { get; private set; } = default!; // snapshot del nombre
    public string         Nombre        { get; private set; } = default!;
    public string?        Cargo         { get; private set; }
    public string?        Correo        { get; private set; }
    public string?        Telefono      { get; private set; }
    public string?        Notas         { get; private set; }
    public OrigenContacto Origen        { get; private set; } = OrigenContacto.Manual;

    private Contacto() { }

    public static Contacto Crear(
        string nombre, string institucionId, string institucionNombre, string? areaId, string? unidadId, string? cargo, string? correo,
        string? telefono, string? notas, OrigenContacto origen = OrigenContacto.Manual)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(institucionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(institucionNombre);

        return new Contacto
        {
            InstitucionId = institucionId,
            AreaId        = areaId,
            UnidadId      = unidadId,
            Institucion   = institucionNombre.Trim(),
            Nombre        = nombre.Trim(),
            Cargo         = cargo?.Trim(),
            Correo        = correo?.Trim().ToLowerInvariant(),
            Telefono      = telefono?.Trim(),
            Notas         = notas?.Trim(),
            Origen        = origen
        };
    }

    public void Actualizar(
        string nombre, string institucionId, string institucionNombre, string? areaId, string? unidadId,
        string? cargo, string? correo, string? telefono, string? notas)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(institucionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(institucionNombre);

        InstitucionId = institucionId;
        AreaId        = areaId;
        UnidadId      = unidadId;
        Institucion   = institucionNombre.Trim();
        Nombre        = nombre.Trim();
        Cargo         = cargo?.Trim();
        Correo        = correo?.Trim().ToLowerInvariant();
        Telefono      = telefono?.Trim();
        Notas         = notas?.Trim();
    }
}
