using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Common;

namespace Diger.TramitesEstado.Domain.Entities;

public sealed class Area : BaseAuditableEntity<string>
{
    public string InstitucionId { get; private set; } = default!;
    public string Nombre        { get; private set; } = default!;
    public string? Descripcion  { get; private set; }
    public string? NombreCorto  { get; private set; }
    public string? LogoUrl      { get; private set; }

    private Area() { }

    public static Area Crear(string id, string institucionId, string nombre, string? descripcion = null, string? nombreCorto = null, string? logoUrl = null)
    {
        ValidarId(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(institucionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        
        return new Area 
        { 
            Id = id.Trim().ToUpper(),
            InstitucionId = institucionId.Trim().ToUpper(),
            Nombre = nombre.Trim().ToUpper(), 
            Descripcion = descripcion?.Trim(),
            NombreCorto = nombreCorto?.Trim().ToUpper(),
            LogoUrl = logoUrl?.Trim()
        };
    }

    private static void ValidarId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!Regex.IsMatch(id.Trim(), @"^[A-Z0-9]+$"))
        {
            throw new DomainException("El Id del Área solo puede contener letras mayúsculas y números, sin espacios ni símbolos.");
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
}
