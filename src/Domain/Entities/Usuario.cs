using Diger.TramitesEstado.Domain.Common;

namespace Diger.TramitesEstado.Domain.Entities;

public sealed class Usuario : BaseAuditableEntity<Guid>, ISoftDeletable
{
    public string     Nombre       { get; private set; } = default!;
    public string     Correo       { get; private set; } = default!; // login (único)
    public string     PasswordHash { get; private set; } = default!;
    public string?    Telefono     { get; private set; }
    public string?    CertificadoThumbprint { get; private set; }
    public string?    PasswordResetToken    { get; private set; }
    public DateTime?  PasswordResetTokenExpiration { get; private set; }
    public bool       Activo       { get; private set; } = true;

    /// <summary>Borrado lógico. La fila se conserva porque quince columnas repartidas en once
    /// tablas guardan el GUID del usuario sin clave foránea; borrarla de verdad las dejaría
    /// apuntando al vacío. Lo aplica el filtro global de AppDbContext.</summary>
    public bool       IsDeleted    { get; set; }

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

    /// <summary>Borrado lógico: la fila queda, el usuario desaparece. Apaga además
    /// <see cref="Activo"/> a propósito — hay caminos que solo consultan esa bandera, y un
    /// «eliminado» que siguiera pasando por ellos sería un borrado de mentira.</summary>
    public void Eliminar()
    {
        IsDeleted = true;
        Activo    = false;
    }

    /// <summary>Deshace <see cref="Eliminar"/>. Lo devuelve activo: se restaura a alguien para que
    /// vuelva a trabajar, y dejarlo inactivo obligaría a un segundo paso que nadie recordaría.</summary>
    public void Restaurar()
    {
        IsDeleted = false;
        Activo    = true;
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
        if (string.IsNullOrWhiteSpace(thumbprint))
        {
            CertificadoThumbprint = null;
        }
        else
        {
            CertificadoThumbprint = System.Text.RegularExpressions.Regex.Replace(thumbprint, @"[^\da-fA-F]", "").ToUpperInvariant();
        }
    }

    public void GenerarTokenRecuperacion(string token, TimeSpan validez)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        PasswordResetToken = token;
        PasswordResetTokenExpiration = DateTime.UtcNow.Add(validez);
    }

    public void LimpiarTokenRecuperacion()
    {
        PasswordResetToken = null;
        PasswordResetTokenExpiration = null;
    }

    public bool EsTokenRecuperacionValido(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(PasswordResetToken))
            return false;

        if (!string.Equals(PasswordResetToken, token, StringComparison.Ordinal))
            return false;

        return PasswordResetTokenExpiration.HasValue && PasswordResetTokenExpiration.Value > DateTime.UtcNow;
    }

    public void Desactivar() => Activo = false;
    public void Activar()    => Activo = true;
}
