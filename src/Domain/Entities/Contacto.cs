namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>Contacto del directorio institucional (enlaces por institución).</summary>
public sealed class Contacto : BaseAuditableEntity
{
    public int            InstitucionId { get; private set; }
    public string         Institucion   { get; private set; } = default!; // snapshot del nombre
    public string         Nombre        { get; private set; } = default!;
    public string?        Cargo         { get; private set; }
    public string?        Correo        { get; private set; }
    public string?        Telefono      { get; private set; }
    public string?        Notas         { get; private set; }
    public OrigenContacto Origen        { get; private set; } = OrigenContacto.Manual;
    public bool           Activo        { get; private set; } = true;

    private Contacto() { }

    public void DarDeBaja() => Activo = false;
    public void Reactivar() => Activo = true;

    public static Contacto Crear(
        string nombre, int institucionId, string institucionNombre, string? cargo, string? correo,
        string? telefono, string? notas, OrigenContacto origen = OrigenContacto.Manual)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(institucionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(institucionNombre);

        return new Contacto
        {
            InstitucionId = institucionId,
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
        string nombre, int institucionId, string institucionNombre,
        string? cargo, string? correo, string? telefono, string? notas)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(institucionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(institucionNombre);

        InstitucionId = institucionId;
        Institucion   = institucionNombre.Trim();
        Nombre        = nombre.Trim();
        Cargo         = cargo?.Trim();
        Correo        = correo?.Trim().ToLowerInvariant();
        Telefono      = telefono?.Trim();
        Notas         = notas?.Trim();
    }
}
