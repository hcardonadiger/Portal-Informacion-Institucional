using Diger.TramitesEstado.Domain.Common;

namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Catálogo de permisos de acción descubiertos por reflexión sobre los PageModel del
/// Web (ver PermissionCatalogSyncService). Id = la clave estable "Modulo.Accion" (ej.
/// "Expedientes.Crear"), independiente del nombre del método en C# — renombrar un
/// handler no rompe los RolPermiso ya otorgados si la clave del atributo no cambia.
/// Los permisos que dejan de existir en el código se desactivan, no se borran, para
/// no perder la trazabilidad de RolPermiso/PermisoAuditoria que los referencian.
/// </summary>
public sealed class Permiso : BaseAuditableEntity<string>
{
    public string       Nombre { get; private set; } = default!;
    public string       Modulo { get; private set; } = default!;
    public AccionModulo Accion { get; private set; }
    public bool         Activo { get; private set; } = true;

    private Permiso() { }

    public static Permiso Crear(string clave, string nombre, string modulo, AccionModulo accion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(modulo);

        return new Permiso
        {
            Id = clave.Trim(),
            Nombre = nombre.Trim(),
            Modulo = modulo.Trim(),
            Accion = accion,
            Activo = true
        };
    }

    /// <summary>Actualiza nombre/módulo/acción si cambiaron en el código y reactiva el
    /// permiso si había quedado desactivado en una sincronización anterior.</summary>
    public void Sincronizar(string nombre, string modulo, AccionModulo accion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(modulo);
        Nombre = nombre.Trim();
        Modulo = modulo.Trim();
        Accion = accion;
        Activo = true;
    }

    public void Desactivar() => Activo = false;
}
