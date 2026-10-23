using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Diger.TramitesEstado.Infrastructure.Outlook;

/// <summary>Cifra la caché de MSAL (que lleva el refresh token) con un propósito exclusivo.</summary>
public sealed class CacheTokensProtector(IDataProtectionProvider dp)
{
    private readonly IDataProtector _p = dp.CreateProtector("ConexionesMicrosoft.CacheTokens");

    public byte[] Cifrar(byte[] claro) => _p.Protect(claro);

    /// <summary>null si no se puede descifrar (llaves perdidas o datos ajenos): el llamador lo
    /// trata como conexión vencida.</summary>
    public byte[]? Descifrar(byte[] cifrado)
    {
        if (cifrado.Length == 0) return null;
        try { return _p.Unprotect(cifrado); }
        catch (CryptographicException) { return null; }
    }
}
