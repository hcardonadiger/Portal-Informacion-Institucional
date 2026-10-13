namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Categoría temática de un proyecto, administrable desde /Catalogos/Categorias. Responde «¿de qué
/// trata?», que es distinto de <see cref="Proyecto.Accion"/> —«¿qué ponemos nosotros acá?»— y de la
/// prioridad —«¿cuánto pesa?»—.
///
/// <para><b>Es opcional en el proyecto</b>, y por eso este catálogo no tiene predeterminada ni
/// exige que quede alguna activa, a diferencia del de prioridades. Un proyecto sin categoría dice
/// «todavía nadie lo clasificó», que es un dato: una categoría puesta de oficio por una migración
/// se lee igual que una que alguien declaró, y después no hay forma de separarlas. Es el mismo
/// criterio con el que se agregó <c>AccionProyecto</c>.</para>
/// </summary>
public sealed class CategoriaProyecto : BaseAuditableEntity
{
    public string Nombre { get; private set; } = default!;

    /// <summary>Posición en listados y desplegables. El menor va primero.</summary>
    public int Orden { get; private set; }

    /// <summary>Color de la insignia con que se pinta en el listado de proyectos.</summary>
    public ColorEtiqueta Color { get; private set; } = ColorEtiqueta.Gris;

    /// <summary>
    /// Una categoría inactiva no se ofrece al clasificar, pero sigue mostrándose en los proyectos
    /// que ya la tienen: retirarla no puede falsear lo que ya se clasificó con ella.
    /// </summary>
    public bool Activo { get; private set; } = true;

    private CategoriaProyecto() { }

    public static CategoriaProyecto Crear(string nombre, int orden, ColorEtiqueta color)
    {
        var c = new CategoriaProyecto();
        c.Actualizar(nombre, orden, color, activo: true);
        return c;
    }

    public void Actualizar(string nombre, int orden, ColorEtiqueta color, bool activo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        if (orden < 0)
            throw new DomainException("El orden no puede ser negativo.");

        Nombre = nombre.Trim();
        Orden  = orden;
        Color  = color;
        Activo = activo;
    }
}
