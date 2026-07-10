using Diger.TramitesEstado.Domain.Common;

namespace Diger.TramitesEstado.Domain.Entities;

public sealed class Usuario : BaseAuditableEntity<Guid>
{
    public string     Nombre       { get; private set; } = default!;
    public string     Correo       { get; private set; } = default!; // login (único)
    public string     PasswordHash { get; private set; } = default!;
    public string?    Telefono     { get; private set; }
    public string?    CertificadoThumbprint { get; private set; }
    public bool       Activo       { get; private set; } = true;

    private Usuario() { }

    public static Usuario Crear(string nombre, string correo, string passwordHash, string? telefono = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(correo);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new Usuario
        {
            Id           = Guid.NewGuid(),
            Nombre       = nombre.Trim(),
            Correo       = correo.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Telefono     = telefono?.Trim(),
            Activo       = true
        };
    }

    public void CambiarPassword(string nuevoHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nuevoHash);
        PasswordHash = nuevoHash;
    }

    public void Renombrar(string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        Nombre = nombre.Trim();
    }

    public void CambiarCorreo(string correo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correo);
        Correo = correo.Trim().ToLowerInvariant();
    }

    public void ActualizarTelefono(string? telefono)
    {
        Telefono = telefono?.Trim();
    }

    public void VincularCertificado(string? thumbprint)
    {
        CertificadoThumbprint = string.IsNullOrWhiteSpace(thumbprint) ? null : thumbprint.Trim().ToUpperInvariant();
    }

    public void Desactivar() => Activo = false;
    public void Activar()    => Activo = true;
}
