using Diger.TramitesEstado.Application.Reuniones.Queries.GetCompromisoDetalle;
using Diger.TramitesEstado.Application.Reuniones.Commands.AgregarComentarioCompromiso;
using Diger.TramitesEstado.Application.Reuniones.Commands.CambiarEstadoCompromiso;
using Diger.TramitesEstado.Web.Common;

namespace Diger.TramitesEstado.Web.Pages.Reuniones;

[Authorize]
[Permission("Reuniones", AccionModulo.Ver, "Ver reuniones y compromisos")]
public sealed class CompromisoDetalleModel(
    ISender sender,
    IWebHostEnvironment env,
    AccesoModulosService acceso) : PageModel
{
    public CompromisoDetalleDto Compromiso { get; private set; } = default!;
    public bool EsAdmin { get; private set; }
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        try
        {
            Compromiso = await sender.Send(new GetCompromisoDetalleQuery(id), ct);
            EsAdmin = await acceso.PuedeEditarAsync("Reuniones", ct);
            return Page();
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Sirve el archivo adjunto de un comentario del compromiso.
    ///
    /// <para>Resuelve el compromiso con la misma consulta que pinta la pantalla, así que hereda su
    /// alcance. Antes el enlace iba directo a <c>/uploads/…</c>, servido sin sesión — ver
    /// <see cref="ArchivosProtegidos"/>.</para>
    /// </summary>
    public async Task<IActionResult> OnGetAdjuntoAsync(int id, int comentarioId, CancellationToken ct)
    {
        try { Compromiso = await sender.Send(new GetCompromisoDetalleQuery(id), ct); }
        catch (NotFoundException) { return NotFound(); }

        var comentario = Compromiso.Comentarios.FirstOrDefault(c => c.Id == comentarioId);
        if (comentario is null) return NotFound();

        var ruta = ArchivosProtegidos.Resolver(env, comentario.ArchivoUrl);
        if (ruta is null) return NotFound();

        var nombre = comentario.ArchivoNombre ?? "adjunto";
        return PhysicalFile(ruta, ArchivosProtegidos.TipoContenido(nombre), nombre);
    }

    [Permission("Reuniones", AccionModulo.Editar, "Crear y editar reuniones")]
    public async Task<IActionResult> OnPostComentarAsync(int id, string? comentario, List<IFormFile>? archivos, CancellationToken ct)
    {
        try
        {
            Compromiso = await sender.Send(new GetCompromisoDetalleQuery(id), ct);
        }
        catch
        {
            // ignora si id no existe
        }

        try
        {
            string? archivoNombre = null;
            string? archivoUrl = null;
            long? archivoTamano = null;

            if (archivos is { Count: > 0 })
            {
                var subidos = await AdjuntoStorage.GuardarAsync(archivos, env, ct, "compromisos");
                if (subidos.Count > 0)
                {
                    archivoNombre = subidos[0].Nombre;
                    archivoUrl = subidos[0].Url;
                    archivoTamano = subidos[0].Tamano;
                }
            }

            await sender.Send(new AgregarComentarioCompromisoCommand(
                id, comentario, archivoNombre, archivoUrl, archivoTamano, AutoEnviar: false), ct);

            TempData["SuccessMsg"] = "Avance o evidencia registrada correctamente.";
            return RedirectToPage("/Reuniones/CompromisoDetalle", new { id });
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException != null ? $" (Detalle interno: {ex.InnerException.Message})" : "";
            Error = $"[{ex.GetType().Name}] {ex.Message}{innerMsg}";
            return Page();
        }
    }

    [Permission("Reuniones", AccionModulo.Editar, "Crear y editar reuniones")]
    public async Task<IActionResult> OnPostEstadoAsync(int id, EstadoCompromiso nuevoEstado, string? nota, CancellationToken ct)
    {
        try
        {
            Compromiso = await sender.Send(new GetCompromisoDetalleQuery(id), ct);
        }
        catch
        {
            // ignora si id no existe
        }

        try
        {
            await sender.Send(new CambiarEstadoCompromisoCommand(id, nuevoEstado, nota), ct);
            TempData["SuccessMsg"] = $"Estado actualizado a {CompromisoUi.Label(nuevoEstado)}.";
            return RedirectToPage("/Reuniones/CompromisoDetalle", new { id });
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException != null ? $" (Detalle interno: {ex.InnerException.Message})" : "";
            Error = $"[{ex.GetType().Name}] {ex.Message}{innerMsg}";
            return Page();
        }
    }

    [Permission("Reuniones", AccionModulo.Editar, "Crear y editar reuniones")]
    public async Task<IActionResult> OnPostEnviarRecordatorioAsync(int id, string? mensaje, CancellationToken ct)
    {
        try
        {
            await sender.Send(new Diger.TramitesEstado.Application.Notificaciones.Commands.EnviarRecordatorioManual.EnviarRecordatorioCompromisoCommand(id, mensaje), ct);
            TempData["SuccessMsg"] = "Notificación de recordatorio enviada al responsable del compromiso.";
            return RedirectToPage("/Reuniones/CompromisoDetalle", new { id });
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            return Page();
        }
    }
}
