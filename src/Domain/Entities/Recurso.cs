using Diger.TramitesEstado.Domain.Common;

namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Archivo o plantilla descargable administrado por los administradores de la plataforma.
/// </summary>
public sealed class Recurso : BaseAuditableEntity, ISoftDeletable
{
    public bool IsDeleted { get; set; }

    public string  Titulo        { get; private set; } = default!;
    public string? Descripcion   { get; set; }
    public string  Categoria     { get; set; } = "Plantilla"; // Plantilla, Guía / Manual, Formulario, Formato Oficial, Otro
    public string  ArchivoNombre { get; private set; } = default!;
    public string  ArchivoUrl    { get; private set; } = default!;
    public long    ArchivoTamano { get; private set; }
    public int     DescargasCount { get; private set; }

    private Recurso() { }

    public static Recurso Crear(
        string titulo,
        string? descripcion,
        string categoria,
        string archivoNombre,
        string archivoUrl,
        long archivoTamano)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titulo);
        ArgumentException.ThrowIfNullOrWhiteSpace(archivoNombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(archivoUrl);

        return new Recurso
        {
            Titulo        = titulo.Trim(),
            Descripcion   = descripcion?.Trim(),
            Categoria     = string.IsNullOrWhiteSpace(categoria) ? "Plantilla" : categoria.Trim(),
            ArchivoNombre = archivoNombre.Trim(),
            ArchivoUrl    = archivoUrl.Trim(),
            ArchivoTamano = archivoTamano,
            DescargasCount = 0
        };
    }

    public void Actualizar(
        string titulo,
        string? descripcion,
        string categoria,
        string? archivoNombre = null,
        string? archivoUrl = null,
        long? archivoTamano = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titulo);

        Titulo      = titulo.Trim();
        Descripcion = descripcion?.Trim();
        Categoria   = string.IsNullOrWhiteSpace(categoria) ? "Plantilla" : categoria.Trim();

        if (!string.IsNullOrWhiteSpace(archivoNombre) && !string.IsNullOrWhiteSpace(archivoUrl) && archivoTamano.HasValue)
        {
            ArchivoNombre = archivoNombre.Trim();
            ArchivoUrl    = archivoUrl.Trim();
            ArchivoTamano = archivoTamano.Value;
        }
    }

    public void IncrementarDescargas()
    {
        DescargasCount++;
    }
}
