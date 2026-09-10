namespace Diger.TramitesEstado.Web.Common;

/// <summary>
/// Resuelve la ruta física de un archivo subido y decide con qué tipo se sirve.
///
/// <para><b>Por qué existe.</b> Hasta el 2026-08-26 <c>Program.cs</c> publicaba
/// <c>App_Data/uploads</c> como archivos estáticos en <c>/uploads</c>, y lo hacía <b>antes</b> de
/// <c>UseAuthentication</c>: cualquiera con la URL bajaba el archivo sin sesión. No era teoría —
/// un <c>fetch</c> con <c>credentials:'omit'</c> devolvió 200 y el contenido. Los nombres son GUID,
/// que es oscuridad y no seguridad, y alcanzaba a todos los adjuntos del portal: tickets,
/// reuniones, expedientes y compromisos.</para>
///
/// <para>Ahora cada módulo sirve lo suyo por un handler que <b>primero resuelve la entidad con la
/// consulta normal</b> —y por lo tanto pasa por el filtro de alcance— y recién después toca el
/// disco. Este ayudante concentra las dos partes que no deben escribirse dos veces: traducir la
/// ruta guardada a una ruta física sin salirse de la carpeta, y elegir el tipo de contenido.</para>
/// </summary>
public static class ArchivosProtegidos
{
    /// <summary>
    /// Ruta física del archivo, o <c>null</c> si no existe o si la ruta guardada apunta fuera de
    /// la carpeta de subidas.
    /// </summary>
    /// <param name="urlRelativa">Lo que quedó guardado en la base, con o sin el prefijo
    /// <c>/uploads/</c> — se acepta de las dos formas porque los módulos lo guardaron distinto.</param>
    public static string? Resolver(IWebHostEnvironment env, string? urlRelativa)
    {
        if (string.IsNullOrWhiteSpace(urlRelativa)) return null;

        var raiz = Path.GetFullPath(Path.Combine(env.ContentRootPath, "App_Data", "uploads"));

        var rel = urlRelativa.TrimStart('/', '\\');
        if (rel.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            rel = rel["uploads/".Length..];

        var ruta = Path.GetFullPath(Path.Combine(raiz, rel.Replace('/', Path.DirectorySeparatorChar)));

        // Cinturón y tirantes: la ruta la genera el portal al guardar, pero llega desde la base y
        // hay filas cargadas por scripts. Sin esta comprobación, un «../../appsettings.json» en esa
        // columna se serviría con toda naturalidad. Se compara con el separador incluido para que
        // una carpeta hermana llamada «uploads_viejo» tampoco pase.
        if (!ruta.StartsWith(raiz + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return null;

        return File.Exists(ruta) ? ruta : null;
    }

    /// <summary>
    /// Tipo con el que se sirve el archivo.
    ///
    /// <para>Las imágenes van con el suyo porque hay vistas que las pintan en un
    /// <c>&lt;img&gt;</c> —las fotos del acta— y con <c>application/octet-stream</c> el navegador
    /// las descargaría en vez de mostrarlas. <b>Todo lo demás sale como binario a propósito</b>:
    /// que el navegador nunca intente interpretar un archivo que subió un tercero.</para>
    /// </summary>
    public static string TipoContenido(string nombreORuta) =>
        Path.GetExtension(nombreORuta).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png"            => "image/png",
            ".webp"           => "image/webp",
            ".gif"            => "image/gif",
            _                 => "application/octet-stream"
        };
}
