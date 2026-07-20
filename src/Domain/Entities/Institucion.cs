using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Common;

namespace Diger.TramitesEstado.Domain.Entities;

public sealed class Institucion : BaseAuditableEntity<string>
{
    public string Nombre      { get; private set; } = default!;
    public string? Descripcion { get; private set; }
    public string? NombreCorto { get; private set; }
    public string? LogoUrl     { get; private set; }
    public string? Color       { get; private set; }
    public string? InfoExtra   { get; private set; }
    public bool   Activo      { get; private set; } = true;

    private readonly List<TramiteDefinicion> _tramites = [];
    public IReadOnlyCollection<TramiteDefinicion> Tramites => _tramites.AsReadOnly();

    private Institucion() { }

    public static Institucion Crear(string id, string nombre, string? descripcion = null, string? nombreCorto = null, string? logoUrl = null, string? infoExtra = null)
    {
        ValidarId(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        
        return new Institucion 
        { 
            Id = id.Trim().ToUpper(),
            Nombre = nombre.Trim().ToUpper(), 
            Descripcion = descripcion?.Trim(),
            NombreCorto = nombreCorto?.Trim().ToUpper(),
            LogoUrl = logoUrl?.Trim(),
            InfoExtra = infoExtra?.Trim(),
            Activo = true 
        };
    }

    private static void ValidarId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!Regex.IsMatch(id.Trim(), @"^[A-Z0-9]+$"))
        {
            throw new DomainException("El Id de la Institución solo puede contener letras mayúsculas y números, sin espacios ni símbolos.");
        }
    }

    public void Renombrar(string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        Nombre = nombre.Trim().ToUpper();
    }
    
    public void ActualizarDetalles(string? descripcion, string? nombreCorto, string? logoUrl, string? infoExtra, string? color = null)
    {
        Descripcion = descripcion?.Trim();
        NombreCorto = nombreCorto?.Trim().ToUpper();
        LogoUrl = logoUrl?.Trim();
        InfoExtra = infoExtra?.Trim();
        Color = string.IsNullOrWhiteSpace(color) ? null : color.Trim();
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
    public string InstitucionId { get; private set; } = default!;
    public string Nombre        { get; private set; } = default!;
    public int    Orden         { get; private set; }

    private TramiteDefinicion() { }

    public static TramiteDefinicion Crear(string institucionId, string nombre, int orden) =>
        new() { InstitucionId = institucionId, Nombre = nombre.Trim(), Orden = orden };
}
