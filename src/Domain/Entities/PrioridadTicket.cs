namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Prioridad asignable a un ticket de soporte, administrable desde /Catalogos/PrioridadesTicket.
/// Reemplaza al enum fijo Baja/Media/Alta/Crítica por la misma razón que el catálogo de proyectos:
/// agregar una opción no debería costar un despliegue.
///
/// <para>Tiene una marca que el de proyectos no necesita: <see cref="EsCritica"/>. Los tableros
/// cuentan «tickets críticos» como indicador de alerta, y esa cuenta estaba atada al miembro
/// <c>Critica</c> del enum. Con un catálogo el nombre lo edita quien administra —puede pasar a
/// llamarse «Urgente» mañana— así que la condición no puede depender de cómo se llame la fila.
/// La marca la declara explícitamente, y admite más de una: si algún día «Crítica» y «Bloqueante»
/// tienen que pesar igual en el indicador, se marcan las dos.</para>
/// </summary>
public sealed class PrioridadTicket : BaseAuditableEntity
{
    public string Nombre { get; private set; } = default!;

    /// <summary>Posición en listados y desplegables. El menor va primero, y primero va lo urgente.</summary>
    public int Orden { get; private set; }

    /// <summary>Color de la insignia con que se pinta en listados y tableros.</summary>
    public ColorEtiqueta Color { get; private set; } = ColorEtiqueta.Gris;

    /// <summary>
    /// ¿Los tickets con esta prioridad cuentan para el indicador de críticos de los tableros?
    /// Es lo que antes preguntaba <c>Prioridad == PrioridadTicket.Critica</c>.
    /// </summary>
    public bool EsCritica { get; private set; }

    /// <summary>La que trae un ticket nuevo. Solo una a la vez; de eso se encarga el módulo.</summary>
    public bool EsPredeterminada { get; private set; }

    /// <summary>
    /// Una prioridad inactiva no se ofrece al clasificar, pero sigue mostrándose en los tickets
    /// que ya la tienen: retirarla no puede falsear lo que ya se clasificó con ella.
    /// </summary>
    public bool Activo { get; private set; } = true;

    private PrioridadTicket() { }

    public static PrioridadTicket Crear(string nombre, int orden, ColorEtiqueta color, bool esCritica = false)
    {
        var p = new PrioridadTicket();
        p.Actualizar(nombre, orden, color, esCritica, activo: true);
        return p;
    }

    public void Actualizar(string nombre, int orden, ColorEtiqueta color, bool esCritica, bool activo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        if (orden < 0)
            throw new DomainException("El orden no puede ser negativo.");
        if (!activo && EsPredeterminada)
            throw new DomainException(
                "No se puede desactivar la prioridad predeterminada. Marque otra como predeterminada primero.");

        Nombre    = nombre.Trim();
        Orden     = orden;
        Color     = color;
        EsCritica = esCritica;
        Activo    = activo;
    }

    /// <summary>
    /// Llamar solo desde PrioridadesTicketModule: marcar una implica desmarcar la anterior, y eso
    /// exige ver el conjunto completo, que es algo que una entidad suelta no puede hacer.
    /// </summary>
    public void FijarPredeterminada(bool valor)
    {
        if (valor && !Activo)
            throw new DomainException("Una prioridad inactiva no puede ser la predeterminada.");
        EsPredeterminada = valor;
    }
}
