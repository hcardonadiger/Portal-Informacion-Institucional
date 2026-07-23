using System.Text;
using Diger.TramitesEstado.Web.Common;

namespace Diger.TramitesEstado.Web.Pages.Reuniones;

[Authorize]
public sealed class AsistenciaModel(ISender sender) : PageModel
{
    public bool EsAdmin => User.IsInRole(nameof(RolUsuario.Administrador));
    public AsistenciaAdminDto Data { get; private set; } = default!;
    public string PublicUrl { get; private set; } = "";
    public string QrDataUri { get; private set; } = "";

    // Directorio para la sección "Pre-registro" (contactos de las instituciones participantes)
    public IReadOnlyList<ContactoDto> DirectorioPreregistro { get; private set; } = [];

    // Directorio para "Agregar del directorio" (adición manual al acta)
    public IReadOnlyList<ContactoDto> DirectorioManual { get; private set; } = [];

    [BindProperty(SupportsGet = true)] public bool TodosLosContactos   { get; set; }
    [BindProperty(SupportsGet = true)] public bool VerTodosPreregistro { get; set; }

    private string BuildPublicUrl(Guid token) =>
        $"{Request.Scheme}://{Request.Host}{Url.Page("/Asistencia/Registro", new { token })}";

    private async Task CargarAsync(int id, CancellationToken ct)
    {
        Data      = await sender.Send(new GetAsistenciaQuery(id), ct);
        PublicUrl = BuildPublicUrl(Data.Token);
        QrDataUri = QrImagen.DataUri(PublicUrl);

        // Correos ya en la lista (para excluirlos del directorio de pre-registro)
        var correosRegistrados = Data.Asistentes
            .Where(a => a.Correo != null).Select(a => a.Correo!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Directorio para pre-registro: contactos de instituciones participantes aún no en la lista
        IReadOnlyList<ContactoDto> contactosInstitucion = [];
        if (VerTodosPreregistro)
        {
            contactosInstitucion = await sender.Send(new GetContactosQuery(), ct);
        }
        else if (Data.InstitucionesNombres.Count > 0)
        {
            contactosInstitucion = await sender.Send(new GetContactosQuery(Instituciones: Data.InstitucionesNombres), ct);
        }
        else if (!string.IsNullOrWhiteSpace(Data.Institucion))
        {
            contactosInstitucion = await sender.Send(new GetContactosQuery(null, Data.Institucion), ct);
        }

        DirectorioPreregistro = contactosInstitucion
            .Where(c => c.Correo == null || !correosRegistrados.Contains(c.Correo))
            .ToList();

        // Directorio para adición manual: respeta TodosLosContactos
        if (TodosLosContactos)
        {
            DirectorioManual = await sender.Send(new GetContactosQuery(), ct);
        }
        else if (Data.InstitucionesNombres.Count > 0)
        {
            DirectorioManual = await sender.Send(new GetContactosQuery(Instituciones: Data.InstitucionesNombres), ct);
        }
        else if (!string.IsNullOrWhiteSpace(Data.Institucion))
        {
            DirectorioManual = await sender.Send(new GetContactosQuery(null, Data.Institucion), ct);
        }
    }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        try { await CargarAsync(id, ct); return Page(); }
        catch (NotFoundException) { return NotFound(); }
    }

    public async Task<IActionResult> OnPostToggleAsync(int id, bool abrir, CancellationToken ct)
    {
        if (!EsAdmin) return Forbid();
        await sender.Send(new CambiarRegistroAsistenciaCommand(id, abrir), ct);
        TempData["SuccessMsg"] = abrir ? "Registro abierto." : "Registro cerrado.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRegenerarAsync(int id, CancellationToken ct)
    {
        if (!EsAdmin) return Forbid();
        await sender.Send(new RegenerarTokenAsistenciaCommand(id), ct);
        TempData["SuccessMsg"] = "Se generó un nuevo enlace. El anterior dejó de funcionar.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, int asistenteId, CancellationToken ct)
    {
        if (!EsAdmin) return Forbid();
        await sender.Send(new EliminarAsistenteCommand(id, asistenteId), ct);
        TempData["SuccessMsg"] = "Registro eliminado.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAgregarDirectorioAsync(int id, List<int> contactoIds, CancellationToken ct)
    {
        if (!EsAdmin) return Forbid();
        var n = await sender.Send(new AgregarAsistentesDirectorioCommand(id, contactoIds ?? []), ct);
        TempData["SuccessMsg"] = n == 0
            ? "No se agregaron contactos (¿ya estaban en la lista?)."
            : $"{n} contacto(s) agregado(s).";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPreregistrarAsync(int id, List<int> contactoIds, CancellationToken ct)
    {
        if (!EsAdmin) return Forbid();
        var n = await sender.Send(new PreregistrarAsistentesCommand(id, contactoIds ?? []), ct);
        TempData["SuccessMsg"] = n == 0
            ? "No se pre-registraron contactos (¿ya estaban en la lista?)."
            : $"{n} invitado(s) pre-registrado(s). Confirmarán su asistencia al llegar o por QR.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostConfirmarAsync(int id, int asistenteId, bool asistio, CancellationToken ct)
    {
        if (!EsAdmin) return Forbid();
        await sender.Send(new ConfirmarAsistenciaCommand(id, asistenteId, asistio), ct);
        TempData["SuccessMsg"] = asistio ? "Asistencia confirmada." : "Marcado como ausente.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnGetExportAsync(int id, CancellationToken ct)
    {
        try { Data = await sender.Send(new GetAsistenciaQuery(id), ct); }
        catch (NotFoundException) { return NotFound(); }

        var sb = new StringBuilder();
        sb.AppendLine("Nombre,Cargo,Institución,Departamento,Correo,Teléfono,Estado,Auto-registro,Registrado");
        foreach (var a in Data.Asistentes)
        {
            var estado = a.EsPreregistro
                ? (a.Confirmado == true ? "Confirmado" : a.Confirmado == false ? "Ausente" : "Invitado")
                : (a.AutoRegistro ? "QR walk-in" : "Manual");
            sb.AppendLine(string.Join(",",
                Q(a.Nombre), Q(a.Cargo), Q(a.Institucion), Q(a.Departamento), Q(a.Correo), Q(a.Telefono),
                Q(estado), Q(a.AutoRegistro ? "Sí" : "No"), Q(a.RegistradoEl?.ToString("yyyy-MM-dd HH:mm"))));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"asistencia_{id}_{DateTime.Now:yyyyMMdd}.csv");

        static string Q(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
    }
}
