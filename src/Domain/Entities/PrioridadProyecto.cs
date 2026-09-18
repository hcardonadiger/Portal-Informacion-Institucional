namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Prioridad asignable a un proyecto, administrable desde /Catalogos/Prioridades. Reemplaza al
/// enum fijo Alta/Media/Baja: cada prioridad nueva era un cambio de código y un despliegue, y
/// las piden cada tanto.
///
/// El proyecto apunta acá por Id y no por nombre. Guardar el nombre habría sido más simple —la
/// columna ya era texto— pero renombrar una prioridad habría obligado a reescribir de paso cada
/// proyecto que la tuviera, y bastaba con que esa reescritura fallara a medias para dejar
/// proyectos apuntando a algo que ya no existe. Con el Id, renombrar es tocar una sola fila.
/// </summary>
public sealed class PrioridadProyecto : BaseAuditableEntity
{
    public string Nombre { get; private set; } = default!;

    /// <summary>Posición en listados y desplegables. El menor va primero.</summary>
    public int Orden { get; private set; }

    /// <summary>Color de la insignia con que se pinta en listados y tableros.</summary>
    public ColorEtiqueta Color { get; private set; } = ColorEtiqueta.Gris;

    /// <summary>
    /// La que viene preseleccionada al crear un proyecto. Solo una a la vez; de eso se encarga
    /// PrioridadesProyectoModule, que es quien ve todas las filas.
    /// </summary>
    public bool EsPredeterminada { get; private set; }

    /// <summary>
    /// Una prioridad inactiva no se ofrece al clasificar, pero sigue mostrándose en los
    /// proyectos que ya la tienen. Es la salida para retirar una prioridad sin falsear la
    /// historia de lo que ya se clasificó con ella.
    /// </summary>
    public bool Activo { get; private set; } = true;

    private PrioridadProyecto() { }

    public static PrioridadProyecto Crear(string nombre, int orden, ColorEtiqueta color)
    {
        var p = new PrioridadProyecto();
        p.Actualizar(nombre, orden, color, activo: true);
        return p;
    }

    public void Actualizar(string nombre, int orden, ColorEtiqueta color, bool activo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        if (orden < 0)
            throw new DomainException("El orden no puede ser negativo.");
        if (!activo && EsPredeterminada)
            throw new DomainException(
                "No se puede desactivar la prioridad predeterminada. Marque otra como predeterminada primero.");

        Nombre = nombre.Trim();
        Orden  = orden;
        Color  = color;
        Activo = activo;
    }

    /// <summary>
    /// Llamar solo desde PrioridadesProyectoModule: marcar una implica desmarcar la anterior, y
    /// eso exige ver el conjunto completo, que es algo que una entidad suelta no puede hacer.
    /// </summary>
    public void FijarPredeterminada(bool valor)
    {
        if (valor && !Activo)
            throw new DomainException("Una prioridad inactiva no puede ser la predeterminada.");
        EsPredeterminada = valor;
    }
}
