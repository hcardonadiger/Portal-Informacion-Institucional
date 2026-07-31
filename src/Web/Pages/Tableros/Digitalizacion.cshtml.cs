using Diger.TramitesEstado.Application.Informes;
using Diger.TramitesEstado.Application.Informes.Queries;

namespace Diger.TramitesEstado.Web.Pages.Tableros;

[Authorize]
public sealed class DigitalizacionModel(ISender sender, IInstitucionRepository institucionRepo, ICurrentUserService currentUser, IInformeService informeSvc) : PageModel
{
    public DigitalizacionDashboardDto Data { get; private set; } = default!;
    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];
    public string? InstitucionId { get; private set; }

    public async Task OnGetAsync(string? institucionId, CancellationToken ct)
    {
        InstitucionId = institucionId;
        var insts = await institucionRepo.GetAllActivasAsync(ct);
        Instituciones = currentUser.EsGlobal ? insts : insts.Where(i => currentUser.InstitucionesAsignadas.Contains(i.Id)).ToList();
        Data = await sender.Send(new GetDigitalizacionDashboardQuery(institucionId), ct);
    }

    /// <summary>Descarga un Excel con todos los trámites y su porcentaje de avance
    /// (mismo cálculo ponderado del detalle/seguimiento). Respeta el filtro de institución.</summary>
    public async Task<IActionResult> OnGetExcelAsync(string? institucionId, CancellationToken ct)
    {
        var dto   = await sender.Send(new GetInformeInstitucionQuery(institucionId, null, null), ct);
        var bytes = informeSvc.GenerarExcel(dto);
        var nombre = $"digitalizacion_tramites_{dto.InstitucionNombre.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
    }
}
