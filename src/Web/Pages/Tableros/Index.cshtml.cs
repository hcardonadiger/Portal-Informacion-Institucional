namespace Diger.TramitesEstado.Web.Pages.Tableros;

/// <summary>
/// Inicio del portal: solo indicadores que cualquier unidad lee como suyos.
/// Los filtros por período y por estado de trámite que vivían acá se fueron con
/// el bloque de digitalización: no tocaban tickets, acuerdos ni capacitación, así
/// que filtraban sin cambiar nada de lo que queda a la vista.
/// </summary>
[Authorize]
[Permission("Tableros", AccionModulo.Ver, "Ver tableros")]
public sealed class IndexModel(ISender sender, IUsuarioRepository usuarioRepo, ICurrentUserService currentUser) : PageModel
{
    public ResumenDto Data { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken ct)
    {
        // El técnico ve los KPIs de tickets limitados a sus temas o sus tickets asignados.
        // "No es jefatura" lo decide la capacidad EsSupervisor del rol (tabla Roles).
        var esTecnico = !currentUser.EsGlobal && !currentUser.EsSupervisor;

        IReadOnlyList<int>? temaIds = null;
        Guid? tecnicoId = null;
        if (esTecnico && currentUser.UserId is Guid uid)
        {
            temaIds = await usuarioRepo.GetTemaIdsAsync(uid, ct);
            tecnicoId = uid;
        }

        Data = await sender.Send(new GetResumenQuery(temaIds, tecnicoId), ct);
    }
}
