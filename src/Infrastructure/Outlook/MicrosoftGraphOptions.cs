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

    /// <summary>
    /// Pedir al conectar los permisos de transcripciones de Teams (fase 2, parte B). Apagado por
    /// defecto, y se enciende solo DESPUÉS de que TI dé el consentimiento de administrador:
    /// OnlineMeetingTranscript.Read.All lo exige, y pedirlo sin él hace fallar «Conectar mi
    /// Outlook» a todo el que no sea administrador del tenant, calendario incluido.
    /// </summary>
    public bool Transcripciones { get; set; }

    public bool EstaCompleta =>
        !string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
