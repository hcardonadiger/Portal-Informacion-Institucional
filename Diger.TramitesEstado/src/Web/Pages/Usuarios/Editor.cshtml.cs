using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Web.Pages.Usuarios;

[Authorize(Policy = "PuedeAdministrarUsuarios")]
public sealed class EditorModel(ISender sender, IInstitucionRepository institucionRepo) : PageModel
{
    public int? UsuarioId { get; private set; }
    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];
    public IReadOnlyList<TemaAdminDto> Temas { get; private set; } = [];

    [BindProperty] public string     Nombre { get; set; } = string.Empty;
    [BindProperty] public string     Correo { get; set; } = string.Empty;
    [BindProperty] public RolUsuario Rol    { get; set; } = RolUsuario.Tecnico;
    [BindProperty] public bool       Activo { get; set; } = true;
    [BindProperty] public string?    Password { get; set; }        // solo al crear
    [BindProperty] public string?    NuevaPassword { get; set; }   // restablecer
    [BindProperty] public List<int>  InstitucionIds { get; set; } = []; // alcance
    [BindProperty] public List<int>  TemaIds { get; set; } = []; // temas que atiende

    public string? Error { get; set; }
    public RolUsuario[] Roles => Enum.GetValues<RolUsuario>();

    private async Task CargarCatalogosAsync(CancellationToken ct)
    {
        Instituciones = await institucionRepo.GetAllActivasAsync(ct);
        Temas = await sender.Send(new GetTemasQuery(), ct);
    }

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken ct)
    {
        await CargarCatalogosAsync(ct);
        if (id is null) return Page();
        try
        {
            var u = await sender.Send(new GetUsuarioByIdQuery(id.Value), ct);
            UsuarioId = u.Id; Nombre = u.Nombre; Correo = u.Correo; Rol = u.Rol; Activo = u.Activo;
            InstitucionIds = u.Instituciones.ToList();
            TemaIds        = u.TemaIds.ToList();
            return Page();
        }
        catch (NotFoundException) { return NotFound(); }
    }

    public async Task<IActionResult> OnPostAsync(int? id, CancellationToken ct)
    {
        UsuarioId = id;
        await CargarCatalogosAsync(ct);

        if (string.IsNullOrWhiteSpace(Nombre) || string.IsNullOrWhiteSpace(Correo))
        {
            Error = "Nombre y correo son obligatorios.";
            return Page();
        }

        try
        {
            int destinoId;
            if (id is null)
            {
                if (string.IsNullOrWhiteSpace(Password) || Password.Length < 8)
                {
                    Error = "La contraseña inicial debe tener al menos 8 caracteres.";
                    return Page();
                }
                destinoId = await sender.Send(new CrearUsuarioCommand(Nombre, Correo, Password, Rol), ct);
            }
            else
            {
                await sender.Send(new ActualizarUsuarioCommand(id.Value, Nombre, Correo, Rol, Activo), ct);
                destinoId = id.Value;
            }
            // Alcance institucional (irrelevante para Administrador: acceso global)
            await sender.Send(new AsignarInstitucionesUsuarioCommand(destinoId, InstitucionIds ?? []), ct);
            // Temas de ticket que atiende el especialista
            await sender.Send(new AsignarTemasUsuarioCommand(destinoId, TemaIds ?? []), ct);

            TempData["SuccessMsg"] = id is null ? "Usuario creado." : "Usuario actualizado.";
            return RedirectToPage("/Usuarios/Index");
        }
        catch (DomainException ex)
        {
            Error = ex.Message;
            return Page();
        }
    }

    public async Task<IActionResult> OnPostRestablecerAsync(int id, CancellationToken ct)
    {
        UsuarioId = id;
        if (string.IsNullOrWhiteSpace(NuevaPassword) || NuevaPassword.Length < 8)
        {
            // recargar datos para re-render
            await CargarCatalogosAsync(ct);
            var u = await sender.Send(new GetUsuarioByIdQuery(id), ct);
            Nombre = u.Nombre; Correo = u.Correo; Rol = u.Rol; Activo = u.Activo;
            InstitucionIds = u.Instituciones.ToList(); TemaIds = u.TemaIds.ToList();
            Error = "La nueva contraseña debe tener al menos 8 caracteres.";
            return Page();
        }
        await sender.Send(new RestablecerPasswordUsuarioCommand(id, NuevaPassword), ct);
        TempData["SuccessMsg"] = "Contraseña restablecida.";
        return RedirectToPage("/Usuarios/Index");
    }
}
