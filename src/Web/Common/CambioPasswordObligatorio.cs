using System.Globalization;
using System.Security.Claims;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Common;

/// <summary>
/// La única definición de «a esta sesión le falta el cambio obligatorio de contraseña».
///
/// Vive aparte del filtro porque la comparten dos lados que tienen que coincidir: el portón que
/// retiene (<see cref="CambioPasswordObligatorioPageFilter"/>) y la propia pantalla de cambio,
/// que se niega a mostrarse a quien ya cumplió. Si cada uno lo decidiera por su cuenta, el día
/// que se corrija uno el otro quedaría desviado.
/// </summary>
public static class CambioPasswordObligatorio
{
    /// <param name="corteUtc">Fecha de corte en UTC, o null si no hay corte configurado — en ese
    /// caso nunca hay nada pendiente: el portón falla abierto a propósito.</param>
    public static bool Pendiente(ClaimsPrincipal user, DateTime? corteUtc)
    {
        if (corteUtc is null) return false;
        if (DateTime.UtcNow < corteUtc.Value) return false;
        if (user.Identity?.IsAuthenticated != true) return false;

        var claim = user.FindFirst(AppClaims.PasswordCambiadoEl)?.Value;

        // Sin claim se cuenta como pendiente: o la contraseña la puso un tercero, o la cookie se
        // emitió antes de que esto existiera. En ambos casos el cambio falta.
        if (!DateTime.TryParse(claim, CultureInfo.InvariantCulture,
                               DateTimeStyles.RoundtripKind, out var cambiadoEl))
            return true;

        return cambiadoEl.ToUniversalTime() < corteUtc.Value;
    }
}
