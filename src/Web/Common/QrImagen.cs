using QRCoder;

namespace Diger.TramitesEstado.Web.Common;

/// <summary>
/// Genera el código QR en el servidor como PNG (data-URI). Se renderiza con una etiqueta
/// &lt;img&gt; normal, sin JavaScript ni &lt;canvas&gt;, para que se dibuje en cualquier navegador.
/// Usa <see cref="PngByteQRCode"/> (sin dependencia de System.Drawing).
/// </summary>
public static class QrImagen
{
    public static string DataUri(string texto, int pixelesPorModulo = 8)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "";
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(texto, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(datos).GetGraphic(pixelesPorModulo);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }
}
