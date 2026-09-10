using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Common;

namespace Diger.TramitesEstado.Domain.Entities;

public sealed class Unidad : BaseAuditableEntity<string>
{
    public string AreaId        { get; private set; } = default!;
    public string Nombre        { get; private set; } = default!;
    public string? Descripcion  { get; private set; }
    public string? NombreCorto  { get; private set; }
    public string? LogoUrl      { get; private set; }
    public bool   Activo        { get; private set; } = true;

    private Unidad() { }

    public static Unidad Crear(string id, string areaId, string nombre, string? descripcion = null, string? nombreCorto = null, string? logoUrl = null)
    {
        ValidarId(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(areaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        
        return new Unidad 
        { 
            Id = id.Trim().ToUpper(),
            AreaId = areaId.Trim().ToUpper(),
            Nombre = nombre.Trim().ToUpper(), 
            Descripcion = descripcion?.Trim(),
            NombreCorto = nombreCorto?.Trim().ToUpper(),
            LogoUrl = logoUrl?.Trim(),
            Activo = true
        };
    }

    private static void ValidarId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!Regex.IsMatch(id.Trim(), @"^[A-Z0-9\-_]+$"))
        {
            throw new DomainException("El Id de la Unidad solo puede contener letras mayúsculas, números, guiones (-) y guiones bajos (_), sin espacios.");
        }
    }

    public void Renombrar(string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        Nombre = nombre.Trim().ToUpper();
    }
    
    public void ActualizarDetalles(string? descripcion, string? nombreCorto, string? logoUrl)
    {
        Descripcion = descripcion?.Trim();
        NombreCorto = nombreCorto?.Trim().ToUpper();
        LogoUrl = logoUrl?.Trim();
    }

    public void Activar()    => Activo = true;
    public void Desactivar() => Activo = false;
}
