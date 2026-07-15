namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>Plantilla de Marco Legal y Requisitos reutilizable entre trámites con el mismo nombre.</summary>
public sealed class PlantillaTramite : BaseAuditableEntity
{
    public string Nombre { get; private set; } = default!;
    public bool   Activa { get; private set; } = true;

    private readonly List<PlantillaFundamentoLegal> _legal = [];
    private readonly List<PlantillaRequisito>       _requisitos = [];
    public IReadOnlyCollection<PlantillaFundamentoLegal> Legal      => _legal.AsReadOnly();
    public IReadOnlyCollection<PlantillaRequisito>       Requisitos => _requisitos.AsReadOnly();

    private PlantillaTramite() { }

    public static PlantillaTramite Crear(string nombre)
        => new() { Nombre = nombre };

    public void Actualizar(string nombre, bool activa)
    {
        Nombre = nombre;
        Activa = activa;
    }

    public void LimpiarHijos()
    {
        _legal.Clear();
        _requisitos.Clear();
    }

    public void Agregar(PlantillaFundamentoLegal l) => _legal.Add(l);
    public void Agregar(PlantillaRequisito r)       => _requisitos.Add(r);
}

public sealed class PlantillaFundamentoLegal : BaseEntity
{
    public int     PlantillaId { get; set; }
    public int     Orden       { get; set; }
    public string  Instrumento { get; set; } = default!;
    public string? Articulos   { get; set; }
    public string? Obs         { get; set; }
}

public sealed class PlantillaRequisito : BaseEntity
{
    public int     PlantillaId { get; set; }
    public int     Orden       { get; set; }
    public string  Requisito   { get; set; } = default!;
    public string? Obs         { get; set; }
}
