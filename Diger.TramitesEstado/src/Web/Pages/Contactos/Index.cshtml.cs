using System.Text;

namespace Diger.TramitesEstado.Web.Pages.Contactos;

[Authorize]
public sealed class IndexModel(ISender sender, IInstitucionRepository institucionRepo) : PageModel
{
    public IReadOnlyList<ContactoDto> Contactos { get; private set; } = [];
    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];
    public PagedResult<ContactoDto> Resultado { get; private set; } = PagedResult<ContactoDto>.Empty(Paginacion.TamanoDefecto);

    [BindProperty(SupportsGet = true)] public string? Buscar      { get; set; }
    [BindProperty(SupportsGet = true)] public string? Institucion { get; set; }
    [BindProperty(SupportsGet = true)] public int?    Page        { get; set; }

    public int TotalContactos    => Contactos.Count;
    public int TotalInstituciones => Contactos.Select(c => c.Institucion).Distinct().Count();
    public int TotalManuales      => Contactos.Count(c => c.Origen == OrigenContacto.Manual);

    public async Task OnGetAsync(CancellationToken ct)
    {
        Instituciones = await institucionRepo.GetAllActivasAsync(ct);
        Contactos = await sender.Send(new GetContactosQuery(Buscar, Institucion), ct);

        var (_, page, size) = Paginacion.Normalizar(null, Page, null);
        var items = Contactos.Skip((page - 1) * size).Take(size).ToList();
        Resultado = new PagedResult<ContactoDto>(items, Contactos.Count, page, size);
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        if (!User.IsInRole(nameof(RolUsuario.Administrador)) && !User.IsInRole(nameof(RolUsuario.Coordinador)))
            return Forbid();
        await sender.Send(new EliminarContactoCommand(id), ct);
        TempData["SuccessMsg"] = "Contacto eliminado.";
        return RedirectToPage(new { Buscar, Institucion });
    }

    public async Task<IActionResult> OnGetExportAsync(CancellationToken ct)
    {
        var contactos = await sender.Send(new GetContactosQuery(Buscar, Institucion), ct);

        var sb = new StringBuilder();
        sb.AppendLine("Nombre,Cargo,Institución,Correo,Teléfono,Notas,Origen");
        foreach (var c in contactos)
            sb.AppendLine(string.Join(",",
                Q(c.Nombre), Q(c.Cargo), Q(c.Institucion), Q(c.Correo), Q(c.Telefono), Q(c.Notas), Q(c.Origen.ToString())));

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"contactos_{DateTime.Now:yyyyMMdd}.csv");

        static string Q(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
    }
}
