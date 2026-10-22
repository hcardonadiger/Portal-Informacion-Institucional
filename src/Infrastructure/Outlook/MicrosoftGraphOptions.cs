namespace Diger.TramitesEstado.Infrastructure.Outlook;

/// <summary>
/// Registro de la aplicación en Entra ID (lo entrega TI). TenantId y ClientId no son secretos;
/// ClientSecret sí: User Secrets en desarrollo, configuración del servidor en producción, nunca un
/// appsettings versionado. Mientras falte cualquiera de los tres, la integración se apaga sola.
/// </summary>
public sealed class MicrosoftGraphOptions
{
    public const string Seccion = "MicrosoftGraph";

    public string TenantId     { get; set; } = string.Empty;
    public string ClientId     { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    public bool EstaCompleta =>
        !string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
