namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>Categoría temática de un trámite (Salud, Educación, etc.) para el catálogo público.
/// Catálogo cerrado y pequeño: se administra por Id, sin factory ni validación de dominio.</summary>
public sealed class CategoriaTramite : BaseAuditableEntity
{
    public string Nombre { get; set; } = default!;
    public string? Icono { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}
