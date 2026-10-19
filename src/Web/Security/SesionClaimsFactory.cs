using System.Security.Claims;
using System.Text.Json;
using Diger.TramitesEstado.Application.Usuarios.Queries.AutenticarUsuario;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Security;

/// <summary>
/// Arma la identidad de una sesión. Punto único: lo usan el login por contraseña, el
/// login por certificado y el servidor MCP.
///
/// Que sea uno solo no es prolijidad. <c>CurrentUserService</c> lee exclusivamente de
/// estos claims —de ahí salen el <c>NivelAlcance</c>, las capacidades del rol y los
/// filtros de alcance del <c>AppDbContext</c>— así que una identidad construida "parecida"
/// en otra parte del código no reusaría el aislamiento institucional: lo reimplementaría,
/// y se desviaría con el primer cambio que alguien haga en un solo lado. Antes este bloque
/// estaba copiado en los dos logins.
/// </summary>
public static class SesionClaimsFactory
{
    /// <param name="activa">
    /// Cuál de las asignaciones del usuario es el contexto activo. Si es null se toma la
    /// primera, que es lo que hace el login web. El servidor MCP sí la pasa: un token fija
    /// su contexto al emitirse porque no tiene dónde cambiarlo después.
    /// </param>
    public static ClaimsIdentity Crear(UsuarioAuthDto usuario, string authScheme, AsignacionAuthDto? activa = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(AppClaims.UserId,          usuario.Id.ToString()),
            new(ClaimTypes.Name,           usuario.Nombre),
            new(ClaimTypes.Email,          usuario.Correo)
        };

        // Solo se emite si la contraseña la eligió el dueño. La ausencia del claim es lo que el
        // portón lee como «cambio pendiente», y por eso no se emite un valor vacío ni un cero:
        // una sesión sin dato tiene que quedar retenida, no aprobada por omisión.
        if (usuario.PasswordCambiadoEl is { } cambiadoEl)
        {
            claims.Add(new Claim(
                AppClaims.PasswordCambiadoEl,
                cambiadoEl.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture)));
        }

        if (usuario.Asignaciones.Count > 0)
        {
            var asignacionesJson = JsonSerializer.Serialize(usuario.Asignaciones);
            claims.Add(new Claim(AppClaims.AsignacionesJson, asignacionesJson));

            var active = activa ?? usuario.Asignaciones[0];
            claims.Add(new Claim(ClaimTypes.Role,             active.Rol));
            claims.Add(new Claim(AppClaims.ActiveRol,         active.Rol));
            claims.Add(new Claim(AppClaims.ActiveInstitucion, active.InstitucionId));
            if (active.AreaId   != null) claims.Add(new Claim(AppClaims.ActiveArea,   active.AreaId));
            if (active.UnidadId != null) claims.Add(new Claim(AppClaims.ActiveUnidad, active.UnidadId));
        }
        // Sin asignaciones NO se emite claim de rol: la cuenta entra sin capacidades y sin
        // alcance, y CurrentUserService la trata como no resuelta (fail-closed). Antes se
        // emitía un rol "Empleado" inventado, que le daba acceso real a quien nadie configuró.
        // Lo único que queda accesible es el autoservicio: perfil, contraseña y ayuda.

        return new ClaimsIdentity(claims, authScheme);
    }
}
