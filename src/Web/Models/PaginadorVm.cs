namespace Diger.TramitesEstado.Web.Models;

/// <summary>
/// Datos del partial <c>_TablaPaginada</c>: el paginador que corre en el navegador.
///
/// <para>No lo confunda con <see cref="PaginacionVm"/>, que alimenta a <c>_Paginacion</c> y describe
/// una página <b>ya recortada por el servidor</b>. Acá el servidor manda todas las filas y el
/// navegador decide cuáles se ven. Se eligió así porque estas listas viven en pantallas cuyos
/// indicadores —semáforo, promedios, los conteos que van dentro de los propios títulos— se calculan
/// sobre el conjunto completo: recortar en la consulta habría hecho que esos números mintieran.</para>
/// </summary>
/// <param name="Id">
/// Identificador que une el control con su lista: el contenedor de las filas lleva
/// <c>data-pg="&lt;Id&gt;"</c> y el control <c>data-pg-for="&lt;Id&gt;"</c>. Tiene que ser único en la
/// página — el Tablero de Proyectos monta cinco listas independientes en una sola pantalla.
/// </param>
/// <param name="PorDefecto">Filas visibles al abrir. Es 10 en todas las listas; el parámetro existe
/// para no tener que tocar el partial si alguna necesita otro arranque.</param>
public sealed record PaginadorVm(string Id, int PorDefecto = 10);
