namespace Diger.TramitesEstado.Web.Models;

/// <summary>
/// Contenido del afiche del QR de asistencia (<c>Pages/Reuniones/_AficheQr.cshtml</c>).
/// Es un tipo propio y no el DTO de la reunión porque el afiche solo lleva lo que se lee de lejos:
/// arma sus campos con <see cref="Campo"/> y los que llegan vacíos no se dibujan.
/// </summary>
/// <param name="Titulo">Nombre de la reunión. Es el titular del afiche.</param>
/// <param name="QrDataUri">PNG del QR como data-URI, en resolución de impresión.</param>
/// <param name="Url">Enlace de registro, impreso como alternativa a escanear.</param>
/// <param name="Institucion">Institución que convoca, para la línea de cabecera.</param>
/// <param name="Logo">Ruta del logo institucional; el afiche lo invierte a blanco.</param>
/// <param name="Campos">Datos de la reunión, ya filtrados y en el orden en que se muestran.</param>
public sealed record AficheQrVm(
    string Titulo,
    string QrDataUri,
    string Url,
    string Institucion,
    string Logo,
    IReadOnlyList<AficheQrCampo> Campos)
{
    /// <summary>Agrega un campo solo si trae valor, para que el afiche no muestre filas vacías.</summary>
    public static void Campo(List<AficheQrCampo> destino, string etiqueta, string? valor)
    {
        if (!string.IsNullOrWhiteSpace(valor)) destino.Add(new AficheQrCampo(etiqueta, valor.Trim()));
    }
}

public sealed record AficheQrCampo(string Etiqueta, string Valor);
