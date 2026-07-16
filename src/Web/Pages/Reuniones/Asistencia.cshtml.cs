using System.Text;
using Diger.TramitesEstado.Web.Common;

namespace Diger.TramitesEstado.Web.Pages.Reuniones;

[Authorize(Policy = "PuedeGestionarReuniones")]
public sealed class AsistenciaModel(ISender sender) : PageModel
{
    public AsistenciaAdminDto Data { get; private set; } = default!;
    public string PublicUrl { get; private set; } = "";
    public string QrDataUri { get; private set; } = "";
    public IReadOnlyList<ContactoDto> Directorio { get; private set; } = [];
    
    [BindProperty(SupportsGet = true)] public bool TodosLosContactos { get; set; }

    private string BuildPublicUrl(Guid token) =>
        $"{Request.Scheme}://{Request.Host}{Url.Page("/Asistencia/Registro", new { token })}";

    private async Task CargarAsync(int id, CancellationToken ct)
    {
        Data = await sender.Send(new GetAsistenciaQuery(id), ct);
        PublicUrl = BuildPublicUrl(Data.Token);
        QrDataUri = QrImagen.DataUri(PublicUrl);
        // El directorio ofrece contactos de las instituciones convocadas, o todos si se solicita.
        if (TodosLosContactos)
        {
            Directorio = await sender.Send(new GetContactosQuery(), ct);
        }
        else
        {
            if (Data.InstitucionesNombres.Count > 0)
                Directorio = await sender.Send(new GetContactosQuery(Instituciones: Data.InstitucionesNombres), ct);
            else if (!string.IsNullOrWhiteSpace(Data.Institucion))
                Directorio = await sender.Send(new GetContactosQuery(null, Data.Institucion), ct);
        }
    }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        try { await CargarAsync(id, ct); return Page(); }
        catch (NotFoundException) { return NotFound(); }
    }

    public async Task<IActionResult> OnPostToggleAsync(int id, bool abrir, CancellationToken ct)
    {
        await sender.Send(new CambiarRegistroAsistenciaCommand(id, abrir), ct);
        TempData["SuccessMsg"] = abrir ? "Registro abierto." : "Registro cerrado.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRegenerarAsync(int id, CancellationToken ct)
    {
        await sender.Send(new RegenerarTokenAsistenciaCommand(id), ct);
        TempData["SuccessMsg"] = "Se generó un nuevo enlace. El anterior dejó de funcionar.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, int asistenteId, CancellationToken ct)
    {
        await sender.Send(new EliminarAsistenteCommand(id, asistenteId), ct);
        TempData["SuccessMsg"] = "Asistente eliminado.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAgregarDirectorioAsync(int id, List<int> contactoIds, CancellationToken ct)
    {
        var n = await sender.Send(new AgregarAsistentesDirectorioCommand(id, contactoIds ?? []), ct);
        TempData["SuccessMsg"] = n == 0 ? "No se agregaron contactos (¿ya estaban en la lista?)." : $"{n} contacto(s) agregado(s).";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnGetExportAsync(int id, CancellationToken ct)
    {
        try { Data = await sender.Send(new GetAsistenciaQuery(id), ct); }
        catch (NotFoundException) { return NotFound(); }

        var sb = new StringBuilder();
        sb.AppendLine("Nombre,Cargo,Institución,Departamento,Correo,Teléfono,Auto-registro,Registrado");
        foreach (var a in Data.Asistentes)
            sb.AppendLine(string.Join(",",
                Q(a.Nombre), Q(a.Cargo), Q(a.Institucion), Q(a.Departamento), Q(a.Correo), Q(a.Telefono),
                Q(a.AutoRegistro ? "Sí" : "No"), Q(a.RegistradoEl?.ToString("yyyy-MM-dd HH:mm"))));

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"asistencia_{id}_{DateTime.Now:yyyyMMdd}.csv");

        static string Q(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
    }
}
