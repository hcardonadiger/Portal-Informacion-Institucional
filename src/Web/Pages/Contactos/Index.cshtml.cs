using System.Text;
using Diger.TramitesEstado.Application.Contactos.Commands.DarDeBajaContacto;
using Diger.TramitesEstado.Application.Contactos.Commands.ReactivarContacto;

namespace Diger.TramitesEstado.Web.Pages.Contactos;

[Authorize]
[Permission("Contactos", AccionModulo.Ver, "Ver el directorio de contactos")]
public sealed class IndexModel(ISender sender, IInstitucionRepository institucionRepo) : PageModel
{
    private IReadOnlyList<ContactoDto> _todos = [];

    public IReadOnlyList<ContactoDto> Contactos { get; private set; } = [];
    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];
    public PagedResult<ContactoDto> Resultado { get; private set; } = PagedResult<ContactoDto>.Empty(Paginacion.TamanoDefecto);

    [BindProperty(SupportsGet = true)] public string? Buscar      { get; set; }
    [BindProperty(SupportsGet = true)] public string? Institucion { get; set; }
    [BindProperty(SupportsGet = true)] public string? EstadoFiltro { get; set; }

    public int TotalContactos     => _todos.Count;
    public int TotalInstituciones => _todos.Select(c => c.Institucion).Distinct().Count();
    public int TotalManuales      => _todos.Count(c => c.Origen == OrigenContacto.Manual);
    public int TotalInactivos     => _todos.Count(c => !c.Activo);

    public async Task OnGetAsync(int? pg, CancellationToken ct)
    {
        EstadoFiltro ??= "Activos";
        Instituciones = await institucionRepo.GetAllActivasAsync(ct);
        _todos = await sender.Send(new GetContactosQuery(Buscar, Institucion, MostrarInactivos: true), ct);
        Contactos = EstadoFiltro switch
        {
            "Inactivos" => _todos.Where(c => !c.Activo).ToList(),
            "Todos"     => _todos.ToList(),
            _           => _todos.Where(c => c.Activo).ToList()
        };

        var (_, p, size) = Paginacion.Normalizar(null, pg, 20);
        var items = Contactos.Skip((p - 1) * size).Take(size).ToList();
        Resultado = new PagedResult<ContactoDto>(items, Contactos.Count, p, size);
    }

    [Permission("Contactos", AccionModulo.Eliminar, "Eliminar contactos")]
    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        // El chequeo de rol por nombre que había acá lo sustituye el [Permission] de arriba.
        await sender.Send(new EliminarContactoCommand(id), ct);
        TempData["SuccessMsg"] = "Contacto eliminado.";
        return RedirectToPage(new { Buscar, Institucion, EstadoFiltro });
    }

    // Dar de baja y reactivar estaba restringido a Administrador y JefeInstitucion, más
    // estrecho que "crear y editar contactos". Se conserva esa distinción con un submódulo
    // propio en vez de diluirla en Contactos.Editar — mismo patrón que Usuarios.Contrasenas.
    [Permission("Contactos.Estado", AccionModulo.Editar, "Dar de baja y reactivar contactos")]
    public async Task<IActionResult> OnPostDarDeBajaAsync(int id, CancellationToken ct)
    {
        await sender.Send(new DarDeBajaContactoCommand(id), ct);
        TempData["SuccessMsg"] = "Contacto dado de baja.";
        return RedirectToPage(new { Buscar, Institucion, EstadoFiltro });
    }

    [Permission("Contactos.Estado", AccionModulo.Editar, "Dar de baja y reactivar contactos")]
    public async Task<IActionResult> OnPostReactivarAsync(int id, CancellationToken ct)
    {
        await sender.Send(new ReactivarContactoCommand(id), ct);
        TempData["SuccessMsg"] = "Contacto reactivado.";
        return RedirectToPage(new { Buscar, Institucion, EstadoFiltro });
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
