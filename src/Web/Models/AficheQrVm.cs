namespace Diger.TramitesEstado.Web.Models;

/// <summary>
/// Contenido del afiche del QR de asistencia (<c>Pages/Reuniones/_AficheQr.cshtml</c>).
///
/// <para>Los datos de la reunión son campos con nombre y no una lista genérica a propósito: el
/// afiche no los pinta como una rejilla de fichas iguales, sino jerarquizados —el tipo arriba
/// como antetítulo, la fecha grande, la hora y la modalidad en una línea, el lugar debajo—, y
/// esa jerarquía no se puede armar recorriendo una lista.</para>
///
/// <para>Todo llega ya recortado, y lo que viene vacío llega en <c>null</c>: la vista solo
/// pregunta si hay valor, nunca limpia.</para>
/// </summary>
/// <param name="Titulo">Nombre de la reunión. Es el titular, que el afiche muestra en mayúscula.</param>
/// <param name="QrDataUri">PNG del QR como data-URI, en resolución de impresión.</param>
/// <param name="Institucion">Institución que convoca; va en el membrete y en el texto alterno del logo.</param>
/// <param name="Logo">Ruta del logo institucional; el afiche lo invierte a blanco.</param>
/// <param name="Tipo">Tipo de reunión. Va de antetítulo: dice qué convocatoria es antes del nombre.</param>
/// <param name="Fecha">Fecha en palabras. El dato más buscado después del nombre.</param>
/// <param name="Hora">Hora o rango de horas.</param>
/// <param name="Modalidad">Presencial, virtual o híbrida.</param>
/// <param name="Lugar">Dónde, cuando la modalidad lo amerita.</param>
public sealed record AficheQrVm(
    string Titulo,
    string QrDataUri,
    string Institucion,
    string Logo,
    string? Tipo = null,
    string? Fecha = null,
    string? Hora = null,
    string? Modalidad = null,
    string? Lugar = null)
{
    /// <summary>Recorta y convierte en <c>null</c> lo que viene vacío, para que la vista no lo haga.</summary>
    public static string? Limpio(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>¿Hay algo que poner en la ficha de fecha, hora, modalidad y lugar?</summary>
    public bool HayFicha => Fecha is not null || Hora is not null || Modalidad is not null || Lugar is not null;

    /// <summary>
    /// Antetítulo: el tipo de reunión, que dice qué convocatoria es antes de leer el nombre.
    /// Sin tipo cae en «Convocatoria», que es lo que el afiche es en cualquier caso.
    /// </summary>
    public string Antetitulo => Tipo ?? "Convocatoria";

    /// <summary>Hora y modalidad en una sola línea; si falta una, se muestra la otra sola.</summary>
    public string? Cuando => string.Join(" · ", new[] { Hora, Modalidad }.Where(v => v is not null)) is { Length: > 0 } t
        ? t
        : null;
}
