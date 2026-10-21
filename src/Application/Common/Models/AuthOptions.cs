namespace Diger.TramitesEstado.Application.Common.Models;

/// <summary>Parámetros de autenticación configurables vía appsettings (sección "Auth").</summary>
public sealed class AuthOptions
{
    public int CookieExpirationHours { get; init; } = 8;
    public int PasswordResetTokenMinutes { get; init; } = 20;
}
