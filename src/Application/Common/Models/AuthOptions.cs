namespace Diger.TramitesEstado.Application.Common.Models;

/// <summary>Parámetros de autenticación configurables vía appsettings (sección "Auth").</summary>
public sealed class AuthOptions
{
    public int CookieExpirationHours { get; init; } = 8;
    public int PasswordResetTokenMinutes { get; init; } = 20;

    /// <summary>
    /// Fecha a partir de la cual se exige el cambio de contraseña por única vez. Se interpreta
    /// como medianoche en la hora del servidor.
    ///
    /// Nula o ausente significa que el portón no retiene a nadie: falla abierto a propósito.
    /// Una clave mal escrita no puede dejar al portal entero dando vueltas en la pantalla de
    /// cambio, y mientras la fecha no llegue nadie debe notar que esto existe.
    /// </summary>
    public DateTime? CambioPasswordObligatorioDesde { get; init; }

    /// <summary>El corte en UTC, que es como viaja <c>Usuario.PasswordCambiadoEl</c>. Null
    /// cuando no hay corte configurado.</summary>
    public DateTime? CorteCambioPasswordUtc =>
        CambioPasswordObligatorioDesde is { } fecha
            ? (fecha.Kind == DateTimeKind.Utc
                ? fecha
                : DateTime.SpecifyKind(fecha, DateTimeKind.Local).ToUniversalTime())
            : null;
}
