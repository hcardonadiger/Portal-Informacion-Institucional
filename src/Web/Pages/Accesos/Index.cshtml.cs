namespace Diger.TramitesEstado.Web.Pages.Accesos;

[Authorize(Roles = "Administrador")]
public sealed class IndexModel(ISender sender) : PageModel
{
    public IReadOnlyList<(string Clave, string Nombre)> Modulos => ModulosPortal.Configurables;
    public HashSet<string> JefeInstitucion { get; private set; } = new();
    public HashSet<string> JefeArea { get; private set; } = new();
    public HashSet<string> JefeUnidad { get; private set; } = new();
    public HashSet<string> Empleado { get; private set; } = new();
    public HashSet<string> Consultor { get; private set; } = new();

    private async Task CargarAsync(CancellationToken ct)
    {
        var accesos = await sender.Send(new GetAccesosQuery(), ct);
        JefeInstitucion = accesos.FirstOrDefault(a => a.Rol == RolUsuario.JefeInstitucion)?.Modulos.ToHashSet() ?? new();
        JefeArea        = accesos.FirstOrDefault(a => a.Rol == RolUsuario.JefeArea)?.Modulos.ToHashSet() ?? new();
        JefeUnidad      = accesos.FirstOrDefault(a => a.Rol == RolUsuario.JefeUnidad)?.Modulos.ToHashSet() ?? new();
        Empleado        = accesos.FirstOrDefault(a => a.Rol == RolUsuario.Empleado)?.Modulos.ToHashSet() ?? new();
        Consultor       = accesos.FirstOrDefault(a => a.Rol == RolUsuario.Consultor)?.Modulos.ToHashSet() ?? new();
    }

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    public async Task<IActionResult> OnPostAsync(
        List<string>? jefeInstitucion, List<string>? jefeArea, List<string>? jefeUnidad, 
        List<string>? empleado, List<string>? consultor, CancellationToken ct)
    {
        await sender.Send(new GuardarAccesosCommand(RolUsuario.JefeInstitucion, jefeInstitucion ?? []), ct);
        await sender.Send(new GuardarAccesosCommand(RolUsuario.JefeArea, jefeArea ?? []), ct);
        await sender.Send(new GuardarAccesosCommand(RolUsuario.JefeUnidad, jefeUnidad ?? []), ct);
        await sender.Send(new GuardarAccesosCommand(RolUsuario.Empleado, empleado ?? []), ct);
        await sender.Send(new GuardarAccesosCommand(RolUsuario.Consultor, consultor ?? []), ct);
        TempData["SuccessMsg"] = "Accesos actualizados.";
        return RedirectToPage();
    }
}
