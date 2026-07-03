using Diger.TramitesEstado.Application.Accesos;

namespace Diger.TramitesEstado.Web.Common;

/// <summary>
/// Bloquea el acceso por URL directa a los módulos del portal según los permisos del rol.
/// Se ejecuta después de la autorización de endpoint (el usuario ya está autenticado); si el
/// rol no tiene acceso al módulo de la ruta, redirige a "Acceso denegado".
/// </summary>
public sealed class ModuloAccesoMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext ctx, AccesoModulosService acceso)
    {
        if (ctx.User.Identity?.IsAuthenticated != true)
        {
            await next(ctx);
            return;
        }

        var modulo = MapearModulo(ctx.Request.Path.Value);
        if (modulo is not null && !await acceso.PuedeAsync(modulo, ctx.RequestAborted))
        {
            ctx.Response.Redirect("/Cuenta/AccessDenied");
            return;
        }

        await next(ctx);
    }

    /// <summary>Mapea el prefijo de la ruta a la clave del módulo (null = ruta no controlada).</summary>
    public static string? MapearModulo(string? path)
    {
        var p = (path ?? "/").TrimEnd('/').ToLowerInvariant();
        if (p.Length == 0 || p == "/index") return ModulosPortal.Expedientes; // raíz = lista de expedientes

        static bool Bajo(string ruta, string prefijo) =>
            ruta == prefijo || ruta.StartsWith(prefijo + "/", StringComparison.Ordinal);

        return p switch
        {
            _ when Bajo(p, "/tableros")      => ModulosPortal.Tableros,
            _ when Bajo(p, "/calendario")    => ModulosPortal.Calendario,
            _ when Bajo(p, "/expedientes")   => ModulosPortal.Expedientes,
            _ when Bajo(p, "/reuniones")     => ModulosPortal.Reuniones,
            _ when Bajo(p, "/tickets")       => ModulosPortal.Tickets,
            _ when Bajo(p, "/contactos")     => ModulosPortal.Contactos,
            _ when Bajo(p, "/instituciones") => ModulosPortal.Instituciones,
            _ when Bajo(p, "/usuarios")      => ModulosPortal.Usuarios,
            _ when Bajo(p, "/accesos")       => ModulosPortal.Accesos,
            _                                => null, // /cuenta, /asistencia, /error, estáticos…
        };
    }
}
