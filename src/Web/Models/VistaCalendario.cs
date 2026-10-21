namespace Diger.TramitesEstado.Web.Models;

/// <summary>Granularidad del calendario. El nombre viaja en la query string (<c>?vista=Semana</c>).</summary>
public enum VistaCalendario
{
    Dia,
    Semana,
    Mes,
}
