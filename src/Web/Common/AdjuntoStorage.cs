namespace Diger.TramitesEstado.Web.Common;

/// <summary>Guarda archivos adjuntos de tickets en wwwroot/uploads/tickets y devuelve sus metadatos.</summary>
public static class AdjuntoStorage
{
    private static readonly string[] ExtPermitidas =
    [
        ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".gif",
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".txt", ".csv", ".log", ".zip"
    ];
    private const long MaxBytes = 10 * 1024 * 1024; // 10 MB por archivo

    public static async Task<List<AdjuntoInput>> GuardarAsync(
        IEnumerable<IFormFile>? files, IWebHostEnvironment env, CancellationToken ct, string carpeta = "tickets")
    {
        var res = new List<AdjuntoInput>();
        if (files is null) return res;

        foreach (var f in files.Where(x => x is { Length: > 0 }))
        {
            if (f.Length > MaxBytes)
                throw new DomainException($"«{f.FileName}» supera el límite de 10 MB.");

            var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
            if (!ExtPermitidas.Contains(ext))
                throw new DomainException($"Tipo de archivo no permitido: {ext}. Permitidos: PDF, imágenes, Office, TXT/CSV/LOG, ZIP.");

            var dir = Path.Combine(env.WebRootPath, "uploads", carpeta);
            Directory.CreateDirectory(dir);
            var nombre = $"{Guid.NewGuid():N}{ext}";
            await using (var fs = File.Create(Path.Combine(dir, nombre)))
                await f.CopyToAsync(fs, ct);

            res.Add(new AdjuntoInput(f.FileName, $"/uploads/{carpeta}/{nombre}", f.Length));
        }
        return res;
    }
}
