using Diger.TramitesEstado.Application.Recursos.Commands.ActualizarRecurso;
using Diger.TramitesEstado.Application.Recursos.Commands.CrearRecurso;
using Diger.TramitesEstado.Application.Recursos.Commands.EliminarRecurso;
using Diger.TramitesEstado.Application.Recursos.Commands.RegistrarDescarga;
using Diger.TramitesEstado.Application.Recursos.Queries.GetRecursos;
using Diger.TramitesEstado.Web.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Diger.TramitesEstado.Web.Pages.Recursos;

[Authorize]
public sealed class IndexModel(
    ISender sender,
    IWebHostEnvironment env) : PageModel
{
    public IReadOnlyList<RecursoDto> Recursos { get; private set; } = [];
    public string? Q { get; private set; }
    public string CategoriaSeleccionada { get; private set; } = "Todos";
    public string? Error { get; set; }

    public bool EsAdmin => User.IsInRole(nameof(RolUsuario.Administrador));

    public async Task<IActionResult> OnGetAsync(string? q, string? categoria, CancellationToken ct)
    {
        Q = q;
        CategoriaSeleccionada = string.IsNullOrWhiteSpace(categoria) ? "Todos" : categoria.Trim();
        Recursos = await sender.Send(new GetRecursosQuery(q, CategoriaSeleccionada), ct);
        return Page();
    }

    public async Task<IActionResult> OnGetDescargarAsync(int id, CancellationToken ct)
    {
        var recursos = await sender.Send(new GetRecursosQuery(), ct);
        var rec = recursos.FirstOrDefault(r => r.Id == id);
        if (rec is null) return NotFound();

        // Incrementar contador de descargas
        await sender.Send(new RegistrarDescargaRecursoCommand(id), ct);

        // Resolver ruta física del archivo local
        var relPath = rec.ArchivoUrl.TrimStart('/');
        var fullPath = Path.Combine(env.ContentRootPath, "App_Data", relPath.Replace("uploads/", "uploads\\"));
        if (!System.IO.File.Exists(fullPath))
        {
            // Intentar en wwwroot o ruta alternativa
            fullPath = Path.Combine(env.WebRootPath, relPath.Replace('/', '\\'));
        }

        if (!System.IO.File.Exists(fullPath))
        {
            TempData["ErrorMsg"] = "El archivo físico solicitado no se encuentra disponible en el servidor.";
            return RedirectToPage();
        }

        return PhysicalFile(fullPath, "application/octet-stream", rec.ArchivoNombre);
    }

    public async Task<IActionResult> OnPostCrearAsync(
        [FromForm] string titulo,
        [FromForm] string? descripcion,
        [FromForm] string categoria,
        IFormFile archivo,
        CancellationToken ct)
    {
        if (!EsAdmin) return Forbid();

        if (archivo is null || archivo.Length == 0)
        {
            Error = "Debe adjuntar un archivo para el recurso.";
            Recursos = await sender.Send(new GetRecursosQuery(), ct);
            return Page();
        }

        try
        {
            var guardados = await AdjuntoStorage.GuardarAsync([archivo], env, ct, carpeta: "recursos");
            if (guardados.Count == 0)
            {
                Error = "Error al procesar el archivo adjunto.";
                Recursos = await sender.Send(new GetRecursosQuery(), ct);
                return Page();
            }

            var adj = guardados[0];
            await sender.Send(new CrearRecursoCommand(
                titulo,
                descripcion,
                categoria,
                adj.Nombre,
                adj.Url,
                adj.Tamano
            ), ct);

            TempData["SuccessMsg"] = "Recurso publicado correctamente.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Recursos = await sender.Send(new GetRecursosQuery(), ct);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostActualizarAsync(
        [FromForm] int id,
        [FromForm] string titulo,
        [FromForm] string? descripcion,
        [FromForm] string categoria,
        IFormFile? archivo,
        CancellationToken ct)
    {
        if (!EsAdmin) return Forbid();

        try
        {
            string? nombre = null;
            string? url = null;
            long? tamano = null;

            if (archivo is { Length: > 0 })
            {
                var guardados = await AdjuntoStorage.GuardarAsync([archivo], env, ct, carpeta: "recursos");
                if (guardados.Count > 0)
                {
                    nombre = guardados[0].Nombre;
                    url = guardados[0].Url;
                    tamano = guardados[0].Tamano;
                }
            }

            await sender.Send(new ActualizarRecursoCommand(
                id,
                titulo,
                descripcion,
                categoria,
                nombre,
                url,
                tamano
            ), ct);

            TempData["SuccessMsg"] = "Recurso actualizado correctamente.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Recursos = await sender.Send(new GetRecursosQuery(), ct);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostEliminarAsync([FromForm] int id, CancellationToken ct)
    {
        if (!EsAdmin) return Forbid();

        try
        {
            await sender.Send(new EliminarRecursoCommand(id), ct);
            TempData["SuccessMsg"] = "Recurso eliminado correctamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMsg"] = ex.Message;
        }
        return RedirectToPage();
    }
}
