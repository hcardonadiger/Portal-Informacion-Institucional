namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Tema/categoría de ticket administrable. Reemplaza el enum fijo: el administrador crea,
/// edita y desactiva temas, y define el tiempo máximo de solución (SLA en horas) por tema,
/// usado para el monitoreo de vencimientos. Se asigna a especialistas vía <see cref="UsuarioTema"/>.
/// </summary>
public sealed class TemaTicket : BaseAuditableEntity
{
    public string Nombre { get; private set; } = default!;
    /// <summary>Tiempo máximo de solución en horas (SLA). 0 = sin límite definido.</summary>
    public int    HorasResolucion { get; private set; }
    public bool   Activo { get; private set; } = true;

    // Categoría (nivel superior) a la que pertenece el tema. Opcional.
    public int?            CategoriaId  { get; private set; }
    public CategoriaTicket? CategoriaRef { get; private set; }

    private TemaTicket() { }

    public static TemaTicket Crear(string nombre, int horasResolucion, int? categoriaId = null)
    {
        var t = new TemaTicket();
        t.Actualizar(nombre, horasResolucion, activo: true, categoriaId);
        return t;
    }

    public void Actualizar(string nombre, int horasResolucion, bool activo, int? categoriaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        if (horasResolucion < 0)
            throw new DomainException("El tiempo máximo de solución no puede ser negativo.");
        Nombre = nombre.Trim();
        HorasResolucion = horasResolucion;
        Activo = activo;
        CategoriaId = categoriaId;
    }
}
