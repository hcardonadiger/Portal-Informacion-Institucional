namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Categoría de ticket: nivel superior que agrupa varios <see cref="TemaTicket"/>.
/// Sirve para segmentar la asignación de especialistas y el análisis de forma más eficiente.
/// </summary>
public sealed class CategoriaTicket : BaseAuditableEntity
{
    public string Nombre { get; private set; } = default!;
    public bool   Activo { get; private set; } = true;

    private CategoriaTicket() { }

    public static CategoriaTicket Crear(string nombre)
    {
        var c = new CategoriaTicket();
        c.Actualizar(nombre, activo: true);
        return c;
    }

    public void Actualizar(string nombre, bool activo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        Nombre = nombre.Trim();
        Activo = activo;
    }
}
