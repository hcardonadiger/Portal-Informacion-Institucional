namespace Diger.TramitesEstado.Web.Common;

/// <summary>Límites y extensiones permitidas para archivos subidos, cargados desde
/// appsettings.json (sección "Uploads") una única vez al arrancar la app en Program.cs.</summary>
public static class UploadsConfig
{
    public static long TicketsMaxBytes { get; set; } = 10 * 1024 * 1024;
    public static long ReunionesMaxBytes { get; set; } = 5 * 1024 * 1024;

    public static string[] ExtensionesPermitidas { get; set; } =
    [
        ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".gif",
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".txt", ".csv", ".log", ".zip"
    ];

    public static string[] ExtensionesImagenesPermitidas { get; set; } =
        [".jpg", ".jpeg", ".png", ".webp", ".gif"];
}
