namespace Diger.TramitesEstado.Web.Pages.Accesos;

[Authorize(Roles = "Administrador")]
public sealed class IndexModel(ISender sender) : PageModel
{
    public IReadOnlyList<(string Clave, string Nombre)> Modulos => ModulosPortal.Configurables;
    public HashSet<string> Coordinador { get; private set; } = new();
    public HashSet<string> Tecnico { get; private set; } = new();

    private async Task CargarAsync(CancellationToken ct)
    {
        var accesos = await sender.Send(new GetAccesosQuery(), ct);
        Coordinador = accesos.First(a => a.Rol == RolUsuario.Coordinador).Modulos.ToHashSet();
        Tecnico     = accesos.First(a => a.Rol == RolUsuario.Tecnico).Modulos.ToHashSet();
    }

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    public async Task<IActionResult> OnPostAsync(List<string>? coordinador, List<string>? tecnico, CancellationToken ct)
    {
        await sender.Send(new GuardarAccesosCommand(RolUsuario.Coordinador, coordinador ?? []), ct);
        await sender.Send(new GuardarAccesosCommand(RolUsuario.Tecnico, tecnico ?? []), ct);
        TempData["SuccessMsg"] = "Accesos actualizados.";
        return RedirectToPage();
    }
}
