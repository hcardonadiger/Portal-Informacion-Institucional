namespace Diger.TramitesEstado.Domain.Entities;

public sealed class Institucion : BaseEntity
{
    public string Nombre { get; private set; } = default!;
    public bool   Activo { get; private set; } = true;

    private readonly List<TramiteDefinicion> _tramites = [];
    public IReadOnlyCollection<TramiteDefinicion> Tramites => _tramites.AsReadOnly();

    private Institucion() { }

    public static Institucion Crear(string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        return new() { Nombre = nombre.Trim().ToUpper(), Activo = true };
    }

    public void Renombrar(string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        Nombre = nombre.Trim().ToUpper();
    }

    public void Activar()    => Activo = true;
    public void Desactivar() => Activo = false;

    public void AgregarTramite(TramiteDefinicion t)
    {
        ArgumentNullException.ThrowIfNull(t);
        _tramites.Add(t);
    }

    public void LimpiarTramites() => _tramites.Clear();
}

public sealed class TramiteDefinicion : BaseEntity
{
    public int    InstitucionId { get; private set; }
    public string Nombre        { get; private set; } = default!;
    public int    Orden         { get; private set; }

    private TramiteDefinicion() { }

    public static TramiteDefinicion Crear(int institucionId, string nombre, int orden) =>
        new() { InstitucionId = institucionId, Nombre = nombre.Trim(), Orden = orden };
}
