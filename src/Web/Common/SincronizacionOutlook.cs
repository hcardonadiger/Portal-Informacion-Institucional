using Diger.TramitesEstado.Application.Reuniones.Outlook;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Diger.TramitesEstado.Web.Common;

/// <summary>
/// Lo que comparten las páginas que cambian una reunión ya enviada a Outlook (el editor y la
/// asistencia): la dirección de la ficha que va en la cita, y la sincronización que nunca convierte
/// un cambio ya guardado en un error.
/// </summary>
public static class SincronizacionOutlook
{
    /// <summary>
    /// La ficha (Acta) de la reunión, tal como va en el cuerpo de la cita. Sale de
    /// <see cref="CorreoOptions.UrlBase"/> y no de la petición: el cuerpo entra en la huella, así
    /// que el mismo guardado hecho por otro host (cert.*, http en vez de https, un nombre interno)
    /// cambiaba la huella, provocaba un PATCH y un «reunión actualizada» a cada invitado, con un
    /// enlace a ese host. Sin <c>UrlBase</c> válida se usa la petición, como antes (el arranque ya
    /// advierte que falta).
    /// </summary>
    public static string UrlFicha(PageModel pagina, CorreoOptions correo, int reunionId)
    {
        if (correo.UrlBaseValida() is not { } urlBase)
            return pagina.Url.PageLink("/Reuniones/Acta", values: new { id = reunionId })!;

        // UrlBase ya incluye la carpeta virtual de IIS: se le quita a la ruta, como en el QR.
        var ruta = pagina.Url.Page("/Reuniones/Acta", new { id = reunionId }) ?? "";
        var pathBase = pagina.Request.PathBase.Value ?? "";
        if (pathBase.Length > 0 && ruta.StartsWith(pathBase, StringComparison.OrdinalIgnoreCase))
            ruta = ruta[pathBase.Length..];
        return urlBase + ruta;
    }

    /// <summary>
    /// Tras un cambio ya guardado: actualiza la cita si cambió para los invitados. Devuelve
    /// <c>false</c> si algo falló; nunca lanza (salvo la cancelación de la petición).
    /// </summary>
    /// <remarks>El comando ya absorbe los fallos de Graph, pero no todo: si el guardado sacó la
    /// reunión del alcance de quien edita (NotFoundException), o falla su propio guardado del
    /// estado, la excepción llegaría al handler con el cambio ya confirmado — y se vería como si
    /// el cambio hubiera fallado. LoggingBehavior ya registra la excepción.</remarks>
    public static async Task<bool> SincronizarAsync(
        PageModel pagina, ISender sender, CorreoOptions correo, int reunionId, CancellationToken ct)
    {
        try
        {
            await sender.Send(new SincronizarReunionOutlookCommand(reunionId, UrlFicha(pagina, correo, reunionId)), ct);
            return true;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }
}
