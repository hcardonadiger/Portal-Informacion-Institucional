using System.Security.Cryptography;

namespace Diger.TramitesEstado.Web.Common;

/// <summary>
/// Guarda el archivo de un documento del repositorio y devuelve sus metadatos, incluida la huella
/// del contenido.
///
/// <para>No reusa <see cref="AdjuntoStorage"/> por una diferencia real: el repositorio necesita el
/// SHA-256 para poder avisar que se está volviendo a subir el mismo archivo y para verificar años
/// después que lo archivado es lo que se archivó. Calcularlo exige leer el flujo entero, así que
/// se hace de una sola pasada mientras se escribe en disco en vez de volver a abrir el archivo.</para>
/// </summary>
public static class DocumentosStorage
{
    /// <summary>Carpeta bajo <c>App_Data/uploads</c>. Separada de la evidencia de la bitácora:
    /// son dos cosas distintas y conviene poder mirarlas por separado en el disco.</summary>
    private const string Carpeta = "proyectos/documentos";

    public sealed record Guardado(string Nombre, string Url, long Tamano, string Sha256);

    public static async Task<Guardado> GuardarAsync(
        IFormFile archivo, IWebHostEnvironment env, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            throw new DomainException("No se recibió ningún archivo.");

        if (archivo.Length > UploadsConfig.TicketsMaxBytes)
            throw new DomainException(
                $"«{archivo.FileName}» supera el límite de {UploadsConfig.TicketsMaxBytes / (1024 * 1024)} MB.");

        var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (!UploadsConfig.ExtensionesPermitidas.Contains(ext))
            throw new DomainException(
                $"Tipo de archivo no permitido: {ext}. Permitidos: PDF, imágenes, Office, TXT/CSV/LOG, ZIP.");

        var dir = Path.Combine(env.ContentRootPath, "App_Data", "uploads",
                               Carpeta.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(dir);

        // Nombre GUID en disco, nombre original en la base. Desde que /uploads no se sirve como
        // estático el GUID ya no es una defensa —lo era, y mala—, pero sigue evitando colisiones
        // y que el nombre que eligió un tercero llegue al sistema de archivos.
        var enDisco = $"{Guid.NewGuid():N}{ext}";
        var destino = Path.Combine(dir, enDisco);

        string huella;
        await using (var entrada = archivo.OpenReadStream())
        await using (var salida  = new FileStream(destino, FileMode.Create, FileAccess.Write,
                                                  FileShare.None, 8192, useAsync: true))
        using (var sha = SHA256.Create())
        // CryptoStream envuelve la salida: los bytes se escriben y se digieren en la misma pasada.
        await using (var medido = new CryptoStream(salida, sha, CryptoStreamMode.Write, leaveOpen: true))
        {
            await entrada.CopyToAsync(medido, ct);
            await medido.FlushFinalBlockAsync(ct);
            huella = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        }

        return new Guardado(
            Nombre: Path.GetFileName(archivo.FileName),
            Url:    $"/uploads/{Carpeta}/{enDisco}",
            Tamano: archivo.Length,
            Sha256: huella);
    }
}
