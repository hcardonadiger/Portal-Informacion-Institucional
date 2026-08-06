using Diger.TramitesEstado.Application.Informes;
using Diger.TramitesEstado.Application.Informes.Queries;

namespace Diger.TramitesEstado.Web.Pages.Tableros;

[Authorize]
public sealed class DigitalizacionModel(ISender sender, IInstitucionRepository institucionRepo, ICurrentUserService currentUser, IInformeService informeSvc) : PageModel
{
    public DigitalizacionDashboardDto Data { get; private set; } = default!;
    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];
    public string? InstitucionId { get; private set; }
    public DateOnly? Desde { get; private set; }
    public DateOnly? Hasta { get; private set; }
    public EstadoTramite? Estado { get; private set; }

    public async Task OnGetAsync(string? institucionId, DateOnly? desde, DateOnly? hasta, string? estado, CancellationToken ct)
    {
        InstitucionId = institucionId;
        Desde = desde;
        Hasta = hasta;
        Estado = Enum.TryParse<EstadoTramite>(estado, ignoreCase: true, out var est) ? est : null;

        var insts = await institucionRepo.GetAllActivasAsync(ct);
        Instituciones = currentUser.EsGlobal ? insts : insts.Where(i => currentUser.InstitucionesAsignadas.Contains(i.Id)).ToList();
        Data = await sender.Send(new GetDigitalizacionDashboardQuery(institucionId, Desde, Hasta, Estado), ct);
    }

    /// <summary>Descarga un Excel con todos los trámites y su porcentaje de avance
    /// (mismo cálculo ponderado del detalle/seguimiento). Respeta el filtro de institución.</summary>
    public async Task<IActionResult> OnGetExcelAsync(string? institucionId, DateOnly? desde, DateOnly? hasta, CancellationToken ct)
    {
        var dto   = await sender.Send(new GetInformeInstitucionQuery(institucionId, desde, hasta), ct);
        var bytes = informeSvc.GenerarExcel(dto);
        var nombre = $"digitalizacion_tramites_{dto.InstitucionNombre.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
    }
}
