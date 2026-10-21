namespace Diger.TramitesEstado.Application.Common.Models;

/// <summary>Resultado paginado genérico para listas con búsqueda.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int  TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    public bool HasPrev    => Page > 1;
    public bool HasNext    => Page < TotalPages;
    public int  Desde      => Total == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int  Hasta      => Math.Min(Page * PageSize, Total);

    public static PagedResult<T> Empty(int pageSize) => new([], 0, 1, pageSize);
}

/// <summary>Normaliza parámetros de paginación/búsqueda recibidos de la UI.</summary>
public static class Paginacion
{
    /// <summary>Configurable vía appsettings ("Paginacion:TamanoDefecto"/"TamanoMaximo"),
    /// asignado en Program.cs al arrancar.</summary>
    public static int TamanoDefecto { get; set; } = 20;
    public static int TamanoMaximo { get; set; } = 100;

    public static (string? q, int page, int size) Normalizar(string? q, int? page, int? size)
    {
        var p = page is null or < 1 ? 1 : page.Value;
        var s = size is null or < 1 ? TamanoDefecto : Math.Min(size.Value, TamanoMaximo);
        var t = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        return (t, p, s);
    }
}
