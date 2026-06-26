namespace Diger.TramitesEstado.Application.Reuniones.Import;

/// <summary>Reunión cruda tal como llega de la fuente externa (Supabase):
/// columnas planas + el blob JSON <c>tema</c>, con sus asistencias ya agrupadas.</summary>
public sealed class ReunionImportRow
{
    public string  Id        { get; set; } = string.Empty; // id externo (reuniones.id)
    public string? Titulo    { get; set; }
    public string? Fecha     { get; set; }                 // "yyyy-MM-dd"
    public string? Hora      { get; set; }
    public string? Lugar     { get; set; }
    public string? Tema      { get; set; }                 // JSON serializado (cap, modalidad, …)
    public string? CreadaPor { get; set; }
    public List<AsistenteImportRow> Asistencias { get; set; } = [];
}

public sealed class AsistenteImportRow
{
    public string  Nombre      { get; set; } = string.Empty;
    public string? Cargo       { get; set; }
    public string? Correo      { get; set; }
    public string? Institucion { get; set; }
    public string? Telefono    { get; set; }
}

/// <summary>Fuente externa de reuniones a importar (implementada en Infrastructure).</summary>
public interface IReunionImportSource
{
    Task<IReadOnlyList<ReunionImportRow>> ObtenerReunionesAsync(CancellationToken ct = default);
}
