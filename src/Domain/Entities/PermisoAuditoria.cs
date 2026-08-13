namespace Diger.TramitesEstado.Domain.Entities;

public enum AccionPermiso { Otorgado, Revocado }

/// <summary>
/// Bitácora append-only de cambios en la matriz de permisos: quién otorgó/revocó qué
/// permiso a qué rol y cuándo. PermisoNombre queda como snapshot (no FK a Permiso) para
/// que el registro siga siendo legible aunque el permiso se renombre o se desactive
/// después. Mismo patrón que BitacoraExpediente: registro acumulativo, nunca se edita.
/// </summary>
public sealed class PermisoAuditoria : BaseEntity
{
    public string        RolId         { get; private set; } = default!;
    public string        PermisoClave  { get; private set; } = default!;
    public string        PermisoNombre { get; private set; } = default!;
    public AccionPermiso Accion        { get; private set; }
    public string        Actor         { get; private set; } = default!;
    public DateTime      Fecha         { get; private set; }

    private PermisoAuditoria() { }   // EF

    public static PermisoAuditoria Crear(string rolId, string permisoClave, string permisoNombre, AccionPermiso accion, string actor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rolId);
        ArgumentException.ThrowIfNullOrWhiteSpace(permisoClave);
        ArgumentException.ThrowIfNullOrWhiteSpace(permisoNombre);

        return new PermisoAuditoria
        {
            RolId = rolId.Trim(),
            PermisoClave = permisoClave.Trim(),
            PermisoNombre = permisoNombre.Trim(),
            Accion = accion,
            Actor = string.IsNullOrWhiteSpace(actor) ? "—" : actor.Trim(),
            Fecha = DateTime.UtcNow,
        };
    }
}
