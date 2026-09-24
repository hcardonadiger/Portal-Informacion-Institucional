using QRCoder;

namespace Diger.TramitesEstado.Web.Common;

/// <summary>
/// Genera el código QR en el servidor como PNG (data-URI). Se renderiza con una etiqueta
/// &lt;img&gt; normal, sin JavaScript ni &lt;canvas&gt;, para que se dibuje en cualquier navegador.
/// Usa <see cref="PngByteQRCode"/> (sin dependencia de System.Drawing).
/// </summary>
public static class QrImagen
{
    public static string DataUri(string texto, int pixelesPorModulo = 8) =>
        Generar(texto, pixelesPorModulo, QRCodeGenerator.ECCLevel.M);

    /// <summary>
    /// QR para el afiche impreso o proyectado: módulos grandes, para que el navegador no tenga que
    /// interpolar al ampliarlo, y corrección de errores Q, porque ese QR se escanea en papel, en
    /// ángulo y a varios metros — condiciones en las que el nivel M empieza a fallar.
    /// </summary>
    public static string DataUriAfiche(string texto) =>
        Generar(texto, 20, QRCodeGenerator.ECCLevel.Q);

    private static string Generar(string texto, int pixelesPorModulo, QRCodeGenerator.ECCLevel correccion)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "";
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(texto, correccion);
        var png = new PngByteQRCode(datos).GetGraphic(pixelesPorModulo);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }
}
