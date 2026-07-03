namespace Diger.TramitesEstado.Web.Models;

/// <summary>Datos que consume el partial _Paginacion (los enlaces preservan el query string actual).</summary>
public sealed record PaginacionVm(int Page, int TotalPages, int Total, int Desde, int Hasta)
{
    public bool HasPrev => Page > 1;
    public bool HasNext => Page < TotalPages;
}
