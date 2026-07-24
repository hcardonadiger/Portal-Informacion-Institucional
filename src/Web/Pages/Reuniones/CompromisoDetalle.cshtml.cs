using Diger.TramitesEstado.Application.Reuniones.Queries.GetCompromisoDetalle;
using Diger.TramitesEstado.Application.Reuniones.Commands.AgregarComentarioCompromiso;
using Diger.TramitesEstado.Application.Reuniones.Commands.CambiarEstadoCompromiso;
using Diger.TramitesEstado.Web.Common;

namespace Diger.TramitesEstado.Web.Pages.Reuniones;

public sealed class CompromisoDetalleModel(
    ISender sender,
    IWebHostEnvironment env) : PageModel
{
    public CompromisoDetalleDto Compromiso { get; private set; } = default!;
    public bool EsAdmin => User.IsInRole(nameof(RolUsuario.Administrador));
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        try
        {
            Compromiso = await sender.Send(new GetCompromisoDetalleQuery(id), ct);
            return Page();
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

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
