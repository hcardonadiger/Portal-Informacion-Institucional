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

    /// <summary>Cuándo fijó su contraseña el propio dueño de la cuenta (UTC). Nula mientras la
    /// contraseña vigente la haya puesto alguien más: la cuenta recién creada con una temporal,
    /// o la que un administrador restableció. Es lo que lee el portón de cambio obligatorio —
    /// nula, o anterior a la fecha de corte, significa «cambio pendiente». Una sola columna
    /// sirve para la tanda única de la plataforma y para toda cuenta nueva que venga después.</summary>
    public DateTime?  PasswordCambiadoEl { get; private set; }
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

    /// <summary>La contraseña la fija su dueño: queda estampada la fecha y el portón de cambio
    /// obligatorio ya no vuelve a retenerlo. Para la que fija un tercero está
    /// <see cref="AsignarPasswordTemporal"/>, que deliberadamente no estampa.</summary>
    public void CambiarPassword(string nuevoHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nuevoHash);
        PasswordHash       = nuevoHash;
        PasswordCambiadoEl = DateTime.UtcNow;
    }

    /// <summary>La contraseña la fija alguien que no es el dueño —un administrador que la
    /// restablece— así que vale como temporal: <see cref="PasswordCambiadoEl"/> vuelve a nula y
    /// la cuenta queda pendiente de cambio en su próximo ingreso. Sin esta separación, un
    /// restablecimiento ajeno contaría como «ya la cambió» y nadie se la volvería a pedir.</summary>
    public void AsignarPasswordTemporal(string nuevoHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nuevoHash);
        PasswordHash       = nuevoHash;
        PasswordCambiadoEl = null;
    }

    /// <summary>¿Le falta el cambio obligatorio? Pendiente es no haberla fijado nunca él mismo,
    /// o haberlo hecho antes de la fecha de corte — el cambio tiene que ocurrir a partir de ella,
    /// si no la tanda única no se cumple.</summary>
    public bool RequiereCambioPassword(DateTime corteUtc)
        => PasswordCambiadoEl is null || PasswordCambiadoEl.Value < corteUtc;

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
