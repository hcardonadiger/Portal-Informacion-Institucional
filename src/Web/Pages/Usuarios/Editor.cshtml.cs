using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Web.Pages.Usuarios;

[Authorize(Policy = "PuedeAdministrarUsuarios")]
public sealed class EditorModel(ISender sender, IInstitucionRepository institucionRepo, IAreaRepository areaRepo, IUnidadRepository unidadRepo) : PageModel
{
    public Guid? UsuarioId { get; private set; }
    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];
    public IReadOnlyList<TemaAdminDto> Temas { get; private set; } = [];

    [BindProperty] public string     Nombre { get; set; } = string.Empty;
    [BindProperty] public string     Correo { get; set; } = string.Empty;
    [BindProperty] public string     Rol    { get; set; } = nameof(Diger.TramitesEstado.Domain.Enums.RolUsuario.Empleado);
    [BindProperty] public bool       Activo { get; set; } = true;
    [BindProperty] public string?    CertificadoThumbprint { get; set; }
    [BindProperty] public string?    Password { get; set; }        // solo al crear
    [BindProperty] public string?    NuevaPassword { get; set; }   // restablecer
    [BindProperty] public List<AsignacionInput> Asignaciones { get; set; } = []; // alcance jerárquico
    [BindProperty] public List<int>  TemaIds { get; set; } = []; // temas que atiende

    public class AsignacionInput {
        public string InstitucionId { get; set; } = string.Empty;
        public string? AreaId { get; set; } 
        public string? UnidadId { get; set; }
    }

    public string JerarquiaJson { get; set; } = "[]";

    public string? Error { get; set; }
    public string[] Roles => Enum.GetNames<Diger.TramitesEstado.Domain.Enums.RolUsuario>();

    private async Task CargarCatalogosAsync(CancellationToken ct)
    {
        Instituciones = await institucionRepo.GetAllActivasAsync(ct);
        Temas = await sender.Send(new GetTemasQuery(), ct);

        var areas = await areaRepo.GetAllAsync(ct);
        var unidades = await unidadRepo.GetAllAsync(ct);
        
        var jerarquia = Instituciones.Select(i => new {
            Id = i.Id,
            Nombre = i.Nombre,
            Areas = areas.Where(a => a.InstitucionId == i.Id).Select(a => new {
                Id = a.Id,
                Nombre = a.Nombre,
                Unidades = unidades.Where(u => u.AreaId == a.Id).Select(u => new {
                    Id = u.Id,
                    Nombre = u.Nombre
                })
            })
        });
        JerarquiaJson = System.Text.Json.JsonSerializer.Serialize(jerarquia);
    }

    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        await CargarCatalogosAsync(ct);
        if (id is null) return Page();
        try
        {
            var u = await sender.Send(new GetUsuarioByIdQuery(id.Value), ct);
            UsuarioId = u.Id; Nombre = u.Nombre; Correo = u.Correo; Rol = u.Rol; Activo = u.Activo; CertificadoThumbprint = u.CertificadoThumbprint;
            Asignaciones = u.Asignaciones.Select(a => new AsignacionInput {
                InstitucionId = a.InstitucionId,
                AreaId = a.AreaId,
                UnidadId = a.UnidadId
            }).ToList();
            TemaIds        = u.TemaIds.ToList();
            return Page();
        }
        catch (NotFoundException) { return NotFound(); }
    }

    public async Task<IActionResult> OnPostAsync(Guid? id, CancellationToken ct)
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
            Guid destinoId;
            if (id is null)
            {
                if (string.IsNullOrWhiteSpace(Password) || Password.Length < 8)
                {
                    Error = "La contraseña inicial debe tener al menos 8 caracteres.";
                    return Page();
                }
                destinoId = await sender.Send(new CrearUsuarioCommand(Nombre, Correo, Password, CertificadoThumbprint), ct);
            }
            else
            {
                await sender.Send(new ActualizarUsuarioCommand(id.Value, Nombre, Correo, Activo, CertificadoThumbprint), ct);
                destinoId = id.Value;
            }
            // Alcance jerárquico
            var asignacionesDto = Asignaciones.Select(a => new Diger.TramitesEstado.Application.Usuarios.Common.AsignacionDto(a.InstitucionId, a.AreaId, a.UnidadId)).ToList();
            await sender.Send(new Diger.TramitesEstado.Application.Usuarios.Commands.AsignarInstitucionesUsuario.AsignarJerarquiaUsuarioCommand(destinoId, Rol, asignacionesDto), ct);
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

    public async Task<IActionResult> OnPostRestablecerAsync(Guid id, CancellationToken ct)
    {
        UsuarioId = id;
        if (string.IsNullOrWhiteSpace(NuevaPassword) || NuevaPassword.Length < 8)
        {
            // recargar datos para re-render
            await CargarCatalogosAsync(ct);
            var u = await sender.Send(new GetUsuarioByIdQuery(id), ct);
            Nombre = u.Nombre; Correo = u.Correo; Rol = u.Rol; Activo = u.Activo;
            Asignaciones = u.Asignaciones.Select(a => new AsignacionInput {
                InstitucionId = a.InstitucionId,
                AreaId = a.AreaId,
                UnidadId = a.UnidadId
            }).ToList(); 
            TemaIds = u.TemaIds.ToList();
            Error = "La nueva contraseña debe tener al menos 8 caracteres.";
            return Page();
        }
        await sender.Send(new RestablecerPasswordUsuarioCommand(id, NuevaPassword), ct);
        TempData["SuccessMsg"] = "Contraseña restablecida.";
        return RedirectToPage("/Usuarios/Index");
    }
}
