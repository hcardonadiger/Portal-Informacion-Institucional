using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Common;

namespace Diger.TramitesEstado.Domain.Entities;

public sealed class Movimiento : BaseAuditableEntity<string>
{
    public string Nombre       { get; private set; } = default!;
    public string? Descripcion { get; private set; }

    private Movimiento() { }

    public static Movimiento Crear(string id, string nombre, string? descripcion = null)
    {
        ValidarId(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        
        return new Movimiento 
        { 
            Id = id.Trim().ToUpper(),
            Nombre = nombre.Trim(), 
            Descripcion = descripcion?.Trim()
        };
    }

    private static void ValidarId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!Regex.IsMatch(id.Trim(), @"^[A-Z0-9]+$"))
        {
            throw new DomainException("El Id del Movimiento solo puede contener letras mayúsculas y números.");
        }
    }

    public void Renombrar(string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        Nombre = nombre.Trim();
    }
    
    public void ActualizarDescripcion(string? descripcion)
    {
        Descripcion = descripcion?.Trim();
    }
}
