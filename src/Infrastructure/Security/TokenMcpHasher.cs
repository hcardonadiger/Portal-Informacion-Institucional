using System.Security.Cryptography;
using System.Text;

namespace Diger.TramitesEstado.Infrastructure.Security;

/// <summary>
/// Genera y verifica los tokens del servidor MCP.
///
/// Formato: <c>pdt.{prefijo}.{secreto}</c>. El prefijo es público y es lo que se guarda
/// en claro: con él se encuentra la fila por índice único, sin escanear la tabla y sin
/// tener que hashear contra cada candidato. El secreto son 32 bytes del generador
/// criptográfico, y de él solo se guarda el SHA-256.
///
/// <para>El separador es un punto y no un guión bajo, aunque «pdt_..._...» se lea mejor:
/// el alfabeto de Base64Url incluye <c>-</c> y <c>_</c>, así que con guión bajo el token se
/// partía en más de tres trozos cada vez que el azar metía uno en el secreto, y esos tokens
/// —una buena parte de los emitidos— no validaban nunca. El punto no pertenece a ese
/// alfabeto. Es el mismo separador que usa <see cref="PasswordHasher"/>.</para>
///
/// **Por qué SHA-256 de un pase y no PBKDF2 como las contraseñas.** El estiramiento de
/// claves existe para encarecer la fuerza bruta contra secretos que una persona eligió y
/// que por lo tanto tienen poca entropía. Acá el secreto tiene 256 bits de entropía real:
/// no hay nada que adivinar, y las 100.000 iteraciones de <c>PasswordHasher</c> se
/// pagarían en cada petición MCP. Por lo mismo no hace falta sal: no hay tablas
/// precalculadas posibles contra un espacio de 2^256.
/// </summary>
public sealed class TokenMcpHasher : ITokenMcpHasher
{
    private const string Marca        = "pdt";
    private const char   Separador    = '.';
    private const int    BytesPrefijo = 6;    // 8 chars en Base64Url — suficiente para no colisionar
    private const int    BytesSecreto = 32;   // 256 bits

    public (string Token, string Prefijo, string Hash) Generar()
    {
        var prefijo = Base64Url(RandomNumberGenerator.GetBytes(BytesPrefijo));
        var secreto = Base64Url(RandomNumberGenerator.GetBytes(BytesSecreto));
        var token   = $"{Marca}{Separador}{prefijo}{Separador}{secreto}";

        return (token, prefijo, Hash(token));
    }

    public string? LeerPrefijo(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var partes = token.Split(Separador);
        if (partes.Length != 3)    return null;
        if (partes[0] != Marca)    return null;
        if (partes[1].Length == 0) return null;
        if (partes[2].Length == 0) return null;

        return partes[1];
    }

    public bool Verificar(string hashAlmacenado, string tokenPresentado)
    {
        if (string.IsNullOrWhiteSpace(hashAlmacenado) || string.IsNullOrWhiteSpace(tokenPresentado))
            return false;

        byte[] esperado;
        try
        {
            esperado = Convert.FromBase64String(hashAlmacenado);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(tokenPresentado));

        // Tiempo constante: comparar con == filtraría por cuánto tarda en salir cuántos
        // bytes iniciales acertó quien prueba.
        return CryptographicOperations.FixedTimeEquals(actual, esperado);
    }

    private static string Hash(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Base64 apto para una cabecera HTTP: sin '+', '/' ni relleno '='.</summary>
    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
