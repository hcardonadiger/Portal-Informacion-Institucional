using System.Security.Cryptography;
using Diger.TramitesEstado.Application.Notificaciones;
using Microsoft.AspNetCore.DataProtection;

namespace Diger.TramitesEstado.Infrastructure.Security;

/// <summary>
/// Cifra la contraseña del correo saliente con Data Protection. Las claves viven en la carpeta que
/// configura Program.cs (<c>PersistKeysToFileSystem</c>): si el servidor la pierde, lo guardado deja
/// de leerse y <see cref="Descifrar"/> devuelve null; el panel lo dice y basta con volver a escribirla.
/// </summary>
public sealed class ProtectorSecretos(IDataProtectionProvider proveedor) : IProtectorSecretos
{
    public const string Proposito = "PanelControl.Notificaciones.Smtp";

    private readonly IDataProtector _protector = proveedor.CreateProtector(Proposito);

    public string Cifrar(string texto) => _protector.Protect(texto);

    public string? Descifrar(string cifrado)
    {
        try { return _protector.Unprotect(cifrado); }
        catch (CryptographicException) { return null; }
    }
}
