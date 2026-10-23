namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Categoría del catálogo de insumos, administrable desde /Catalogos/CategoriasInsumo. Agrupa los
/// insumos en la ventana con que se eligen para una actividad.
///
/// <para><b>No se borra, se desactiva.</b> Una categoría desactivada deja de ofrecer sus insumos
/// para elegir, pero las actividades que ya los tienen los conservan: retirarla no puede falsear
/// lo que ya se planificó con ella.</para>
///
/// <para>En la interfaz se llama «Insumos» para no confundirse con el módulo «Recursos y
/// plantillas», que es otra cosa.</para>
/// </summary>
public sealed class CategoriaInsumo : BaseAuditableEntity
{
    public string Nombre { get; private set; } = default!;

    /// <summary>Posición en el catálogo y en la ventana. El menor va primero.</summary>
    public int Orden { get; private set; }

    public bool Activo { get; private set; } = true;

    private CategoriaInsumo() { }   // EF

    public static CategoriaInsumo Crear(string nombre, int orden)
    {
        var c = new CategoriaInsumo();
        c.Actualizar(nombre, orden, activo: true);
        return c;
    }

    public void Actualizar(string nombre, int orden, bool activo)
    {
        var limpio = (nombre ?? "").Trim();
        if (limpio.Length == 0)
            throw new DomainException("La categoría necesita un nombre.");
        if (limpio.Length > MaxNombre)
            throw new DomainException($"El nombre de la categoría no puede superar los {MaxNombre} caracteres.");
        if (orden < 0)
            throw new DomainException("El orden no puede ser negativo.");

        Nombre = limpio;
        Orden  = orden;
        Activo = activo;
    }

    public const int MaxNombre = 60;
}

/// <summary>
/// Un insumo del catálogo: algo que una actividad necesita para hacerse —un material, un equipo,
/// un servicio—. Solo el nombre: <b>sin cantidad, costo ni inventario</b>, que no se pidieron y
/// convertirían una lista de lo que hace falta en una contabilidad.
///
/// <para>Igual que su categoría, no se borra: se desactiva. El nombre es único dentro de su
/// categoría; dos categorías sí pueden tener un insumo con el mismo nombre.</para>
/// </summary>
public sealed class Insumo : BaseAuditableEntity
{
    public int CategoriaId { get; private set; }
    public CategoriaInsumo? Categoria { get; private set; }

    public string  Nombre      { get; private set; } = default!;
    public string? Descripcion { get; private set; }

    /// <summary>Un insumo inactivo no se ofrece para elegir, pero sigue en las actividades que ya
    /// lo tenían. Ver <see cref="ActividadProyecto.FijarInsumos"/>.</summary>
    public bool Activo { get; private set; } = true;

    private Insumo() { }   // EF

    public static Insumo Crear(int categoriaId, string nombre, string? descripcion)
    {
        var i = new Insumo();
        i.Actualizar(categoriaId, nombre, descripcion, activo: true);
        return i;
    }

    public void Actualizar(int categoriaId, string nombre, string? descripcion, bool activo)
    {
        if (categoriaId <= 0)
            throw new DomainException("El insumo necesita una categoría.");

        var limpio = (nombre ?? "").Trim();
        if (limpio.Length == 0)
            throw new DomainException("El insumo necesita un nombre.");
        if (limpio.Length > MaxNombre)
            throw new DomainException($"El nombre del insumo no puede superar los {MaxNombre} caracteres.");

        var desc = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        if (desc is { Length: > MaxDescripcion })
            throw new DomainException($"La descripción del insumo no puede superar los {MaxDescripcion} caracteres.");

        CategoriaId = categoriaId;
        Nombre      = limpio;
        Descripcion = desc;
        Activo      = activo;
    }

    public const int MaxNombre      = 120;
    public const int MaxDescripcion = 500;
}

/// <summary>
/// Un insumo asignado a una actividad. La llave es la pareja actividad–insumo: el mismo insumo no
/// se asigna dos veces a la misma actividad.
///
/// <para><b>La posee la actividad</b> y no tiene DbSet propio: se llega a ella solo a través del
/// proyecto, que es el que lleva el filtro de alcance. Una tabla suelta consultable por su cuenta
/// es justo el defecto que dejó riesgos e interesados al alcance de cualquiera que conociera el
/// número.</para>
/// </summary>
public sealed class InsumoActividad
{
    /// <summary>La fija EF al agregarla a la colección de la actividad.</summary>
    public int ActividadId { get; set; }
    public int InsumoId    { get; private set; }

    public DateTime AgregadoEl  { get; private set; }
    public string   AgregadoPor { get; private set; } = default!;

    private InsumoActividad() { }   // EF

    internal static InsumoActividad Crear(int insumoId, string agregadoPor, DateTime agregadoEl) =>
        new() { InsumoId = insumoId, AgregadoPor = agregadoPor, AgregadoEl = agregadoEl };

    public const int MaxAgregadoPor = 200;
}
