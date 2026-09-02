namespace Diger.TramitesEstado.Web.Pages.Accesos;

/// <summary>
/// CRUD del catálogo de roles. Sustituye al enum RolUsuario: acá se define el alcance de
/// datos (NivelAlcance, que alimenta los filtros RLS de AppDbContext) y las cuatro
/// capacidades que antes estaban hardcodeadas por nombre de rol.
///
/// Granularidad por handler: ver la lista requiere Accesos.Roles.Ver, guardar exige
/// Accesos.Roles.Editar y eliminar Accesos.Roles.Eliminar — así se puede delegar la
/// consulta del catálogo sin entregar la capacidad de modificarlo.
/// </summary>
[Permission("Accesos.Roles", AccionModulo.Ver, "Ver roles")]
public sealed class RolesModel(ISender sender) : PageModel
{
    public IReadOnlyList<RolListItemDto> Roles { get; private set; } = [];

    /// <summary>Código del rol en edición; null = alta de rol nuevo.</summary>
    public string? EnEdicion { get; private set; }

    public string? Error { get; set; }

    [BindProperty] public string       Codigo           { get; set; } = string.Empty;
    [BindProperty] public string       Nombre           { get; set; } = string.Empty;
    [BindProperty] public string?      Descripcion      { get; set; }
    [BindProperty] public string?      Color            { get; set; }
    [BindProperty] public NivelAlcance NivelAlcance     { get; set; } = NivelAlcance.Unidad;
    [BindProperty] public bool         EsAdministrador  { get; set; }
    [BindProperty] public bool         EsSoloLectura    { get; set; }
    [BindProperty] public bool         EsSupervisor     { get; set; }
    [BindProperty] public bool         EsTecnicoSoporte { get; set; }
    [BindProperty] public bool         Activo           { get; set; } = true;

    public static readonly (NivelAlcance Nivel, string Etiqueta)[] Niveles =
    [
        (NivelAlcance.Global,      "Global — ve todo el portal"),
        (NivelAlcance.Institucion, "Institución — ve todo lo de su institución"),
        (NivelAlcance.Area,        "Área — ve lo de su área"),
        (NivelAlcance.Unidad,      "Unidad — ve lo de su unidad"),
    ];

    private async Task CargarAsync(CancellationToken ct) =>
        Roles = await sender.Send(new GetRolesQuery(), ct);

    public async Task OnGetAsync(string? editar, CancellationToken ct)
    {
        await CargarAsync(ct);

        if (string.IsNullOrWhiteSpace(editar)) return;

        var rol = Roles.FirstOrDefault(r => string.Equals(r.Codigo, editar, StringComparison.OrdinalIgnoreCase));
        if (rol is null) return;

        EnEdicion        = rol.Codigo;
        Codigo           = rol.Codigo;
        Nombre           = rol.Nombre;
        Descripcion      = rol.Descripcion;
        Color            = rol.Color;
        NivelAlcance     = rol.NivelAlcance;
        EsAdministrador  = rol.EsAdministrador;
        EsSoloLectura    = rol.EsSoloLectura;
        EsSupervisor     = rol.EsSupervisor;
        EsTecnicoSoporte = rol.EsTecnicoSoporte;
        Activo           = rol.Activo;
    }

    [Permission("Accesos.Roles", AccionModulo.Editar, "Crear y editar roles")]
    public async Task<IActionResult> OnPostGuardarAsync(string? editar, CancellationToken ct)
    {
        EnEdicion = editar;

        try
        {
            if (string.IsNullOrWhiteSpace(editar))
            {
                await sender.Send(new CrearRolCommand(
                    Codigo, Nombre, NivelAlcance, Descripcion, Color,
                    EsAdministrador, EsSoloLectura, EsSupervisor, EsTecnicoSoporte), ct);

                TempData["SuccessMsg"] = "Rol creado.";
            }
            else
            {
                await sender.Send(new ActualizarRolCommand(
                    editar, Nombre, NivelAlcance, Descripcion, Color,
                    EsAdministrador, EsSoloLectura, EsSupervisor, EsTecnicoSoporte, Activo), ct);

                TempData["SuccessMsg"] = "Rol actualizado.";
            }

            return RedirectToPage();
        }
        catch (Exception ex) when (ex is DomainException or ArgumentException)
        {
            // Rol.Crear valida el código con ArgumentException/DomainException según el caso;
            // ambas son errores de captura, no fallas del sistema.
            Error = ex.Message;
            await CargarAsync(ct);
            return Page();
        }
    }

    [Permission("Accesos.Roles", AccionModulo.Eliminar, "Eliminar roles")]
    public async Task<IActionResult> OnPostEliminarAsync(string codigo, CancellationToken ct)
    {
        try
        {
            await sender.Send(new EliminarRolCommand(codigo), ct);
            TempData["SuccessMsg"] = "Rol eliminado.";
            return RedirectToPage();
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
            await CargarAsync(ct);
            return Page();
        }
    }
}
