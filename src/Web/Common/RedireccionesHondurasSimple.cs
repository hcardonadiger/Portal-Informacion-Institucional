namespace Diger.TramitesEstado.Web.Common;

/// <summary>
/// Las siete páginas que el 3 de septiembre de 2026 se fueron de <c>/Siger/</c> a
/// <c>/HondurasSimple/</c> al separar el trabajo del portal ciudadano del inventario.
///
/// Existe porque una dirección publicada no se puede retirar: hay enlaces en correos, en
/// favoritos y en actas de reunión que apuntan a <c>/Siger/Conciliacion</c>. Sin esto, el día
/// del despliegue esos enlaces contestan 404 y el usuario concluye que la pantalla se
/// eliminó, no que se mudó.
///
/// Es un 301 y no un 302 a propósito: la mudanza es definitiva, y así el navegador deja de
/// pedir la dirección vieja en vez de preguntar por ella para siempre.
///
/// <b>Tiene fecha de caducidad.</b> Cuando los registros del servidor dejen de mostrar
/// tráfico hacia estas rutas, este archivo se borra entero y se quita su línea de
/// <c>Program.cs</c>. Está aparte justamente para que borrarlo sea una decisión de un minuto
/// y no una arqueología.
/// </summary>
public static class RedireccionesHondurasSimple
{
    private static readonly Dictionary<string, string> Mudanzas =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["/Siger/Editor"]       = "/HondurasSimple/Editor",
            ["/Siger/Archivo"]      = "/HondurasSimple/Archivo",
            ["/Siger/CapturaLote"]  = "/HondurasSimple/CapturaLote",
            ["/Siger/Completitud"]  = "/HondurasSimple/Completitud",
            ["/Siger/Llenado"]      = "/HondurasSimple/Llenado",
            ["/Siger/Conciliacion"] = "/HondurasSimple/Conciliacion",
            ["/Siger/Publicacion"]  = "/HondurasSimple/Publicacion",
        };

    /// <summary>
    /// Redirige las rutas viejas conservando la cadena de consulta: la mitad de estas
    /// pantallas se comparte con filtros puestos (<c>?tab=...&amp;buscar=...</c>) y llegar a la
    /// pantalla sin el filtro que le prometieron a uno no es mucho mejor que un 404.
    /// </summary>
    public static IApplicationBuilder UseRedireccionesHondurasSimple(this IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path.HasValue &&
                Mudanzas.TryGetValue(ctx.Request.Path.Value.TrimEnd('/'), out var destino))
            {
                ctx.Response.Redirect(destino + ctx.Request.QueryString, permanent: true);
                return;
            }

            await next();
        });
}
